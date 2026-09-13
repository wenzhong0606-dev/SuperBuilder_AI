using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace SuperBuilder_AI.Api.Diagnostics;

/// <summary>
/// 默认告警接收端（OBS-01）。将告警写入日志，并在内存保留最近若干条供诊断；
/// 不依赖任何外部系统，保证可观测告警在无集成时仍可落地。
/// </summary>
public sealed class LoggingAlertSink : IAlertSink
{
	private readonly ILogger<LoggingAlertSink>? _logger;
	private readonly object _gate = new();
	private readonly List<Alert> _recent = new();
	private const int Capacity = 200;

	public LoggingAlertSink(ILogger<LoggingAlertSink>? logger = null) => _logger = logger;

	public void Raise(Alert alert)
	{
		try
		{
			var level = alert.Severity == AlertSeverity.Critical ? LogLevel.Critical : LogLevel.Warning;
			_logger?.Log(level,
				"ALERT [{Severity}] {Rule} signal={Signal} detail={Detail} raisedAt={RaisedAt:o}",
				alert.Severity, alert.Rule, alert.Signal, alert.Detail, alert.RaisedAt);

			lock (_gate)
			{
				_recent.Add(alert);
				if (_recent.Count > Capacity) _recent.RemoveAt(0);
			}
		}
		catch
		{
			// 告警接收失败绝不反向影响业务链路
		}
	}

	public IReadOnlyList<Alert> Snapshot()
	{
		lock (_gate) return _recent.ToArray();
	}

	public void Clear()
	{
		lock (_gate) _recent.Clear();
	}
}
