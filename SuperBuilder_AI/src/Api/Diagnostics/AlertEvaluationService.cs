using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SuperBuilder_AI.Middleware;

namespace SuperBuilder_AI.Api.Diagnostics;

/// <summary>
/// 告警评估主机服务（OBS-01）。周期性调用 <see cref="AlertEvaluator"/> 评估指标，
/// 向 <see cref="IAlertSink"/> 发出告警；当某规则从上周期触发变为本周期未触发时，
/// 额外发出一条「恢复」告警，确保「触发 + 恢复」闭环可被观测与验证。
/// </summary>
public sealed class AlertEvaluationService : BackgroundService
{
	private readonly IAlertSink _sink;
	private readonly RequestMetricsCollector _metrics;
	private readonly ScanBacklogGauge _backlog;
	private readonly AlertThresholds _thresholds;
	private readonly ILogger<AlertEvaluationService> _logger;
	private readonly TimeSpan _interval;

	private readonly object _gate = new();
	private HashSet<string> _firedRules = new();
	private IReadOnlyList<Alert> _lastAlerts = Array.Empty<Alert>();

	/// <summary>当前处于触发态的告警快照（供 <c>/metrics</c> 暴露）。</summary>
	public IReadOnlyList<Alert> LastAlerts
	{
		get { lock (_gate) return _lastAlerts; }
	}

	public AlertEvaluationService(
		IAlertSink sink,
		RequestMetricsCollector metrics,
		ScanBacklogGauge backlog,
		AlertThresholds thresholds,
		ILogger<AlertEvaluationService> logger,
		TimeSpan? interval = null)
	{
		_sink = sink;
		_metrics = metrics;
		_backlog = backlog;
		_thresholds = thresholds;
		_logger = logger;
		_interval = interval ?? TimeSpan.FromMinutes(1);
	}

	/// <summary>
	/// 执行一次评估与分发（公开以便单元测验证「触发 + 恢复」）。线程安全。
	/// </summary>
	public void CycleOnce()
	{
		var now = DateTimeOffset.UtcNow;
		IReadOnlyList<Alert> alerts;
		try
		{
			alerts = AlertEvaluator.Evaluate(_metrics, _backlog, _thresholds);
		}
		catch (Exception ex)
		{
			_logger?.LogWarning(ex, "告警评估异常（忽略本轮）。");
			return;
		}

		var currentRules = alerts.Select(a => a.Rule).ToHashSet();

		// 恢复：上周期触发、本周期未触发的规则
		IEnumerable<string> recovered;
		HashSet<string> prev;
		lock (_gate) { prev = _firedRules; }
		recovered = prev.Except(currentRules);
		foreach (var rule in recovered)
		{
			_sink.Raise(new Alert(
				rule, AlertSeverity.Warning, "recovered",
				$"{rule} 已恢复：相关错误率/积压已回落至阈值以下。",
				now));
		}

		// 触发
		foreach (var a in alerts) _sink.Raise(a);

		lock (_gate)
		{
			_firedRules = currentRules;
			_lastAlerts = alerts;
		}
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			try { CycleOnce(); }
			catch (Exception ex) { _logger?.LogWarning(ex, "告警周期执行异常（忽略）。"); }

			try { await Task.Delay(_interval, stoppingToken); }
			catch (OperationCanceledException) { break; }
		}
	}
}
