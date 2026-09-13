using System;
using System.Collections.Generic;
using System.Linq;
using SuperBuilder_AI.Middleware;

namespace SuperBuilder_AI.Api.Diagnostics;

/// <summary>
/// 告警评估器（OBS-01，纯函数）。依据 <see cref="RequestMetricsCollector"/> 与
/// <see cref="ScanBacklogGauge"/> 的快照，对照 <see cref="AlertThresholds"/> 产出告警列表。
/// 不含状态（恢复判定由 <see cref="AlertEvaluationService"/> 基于上周期结果完成）。
/// </summary>
public static class AlertEvaluator
{
	/// <summary>
	/// 评估当前指标，返回应「触发」的告警（不含恢复）。无越界则返回空列表。
	/// </summary>
	public static IReadOnlyList<Alert> Evaluate(
		RequestMetricsCollector metrics,
		ScanBacklogGauge backlog,
		AlertThresholds t)
	{
		var alerts = new List<Alert>();
		if (metrics is null || backlog is null || t is null) return alerts;

		var now = DateTimeOffset.UtcNow;
		var routes = metrics.Snapshot();

		// 1) 路由错误率
		foreach (var r in routes)
		{
			if (r.Count < t.RouteMinSamples) continue;
			if (r.ErrorRate < t.RouteErrorRate) continue;
			var sev = r.ErrorRate >= t.CriticalErrorRate ? AlertSeverity.Critical : AlertSeverity.Warning;
			alerts.Add(new Alert(
				"route_error_rate_high", sev, r.Route,
				$"路由 {r.Route} 错误率 {r.ErrorRate:P1}（{r.Errors}/{r.Count}），阈值 {t.RouteErrorRate:P1}。",
				now));
		}

		// 2) 登录失败率（登录路由：失败率 = 1 − LoginSuccessRate = Unauthorized/Count）
		foreach (var r in routes)
		{
			if (!r.Route.Contains("login", StringComparison.OrdinalIgnoreCase)) continue;
			if (r.Count < t.LoginMinSamples) continue;
			var failureRate = r.Count == 0 ? 0 : (double)r.Unauthorized / r.Count;
			if (failureRate < t.LoginFailureRate) continue;
			alerts.Add(new Alert(
				"login_failure_high", AlertSeverity.Warning, r.Route,
				$"登录失败率 {failureRate:P1}（{r.Unauthorized}/{r.Count}），阈值 {t.LoginFailureRate:P1}。",
				now));
		}

		// 3) Ask 失败率
		var outcomes = metrics.OutcomeSnapshot().ToDictionary(x => x.Outcome, x => x.Count);
		var success = outcomes.GetValueOrDefault(SuperBuilder_AI.Interfaces.BI.IPipelineMetricsSink.OutcomeSuccess);
		var failure = outcomes.GetValueOrDefault(SuperBuilder_AI.Interfaces.BI.IPipelineMetricsSink.OutcomeFailure);
		var askTotal = success + failure;
		if (askTotal >= t.AskMinSamples)
		{
			var askFailureRate = (double)failure / askTotal;
			if (askFailureRate >= t.AskFailureRate)
				alerts.Add(new Alert(
					"ask_failure_high", AlertSeverity.Warning, "Ask",
					$"Ask 失败率 {askFailureRate:P1}（{failure}/{askTotal}），阈值 {t.AskFailureRate:P1}。",
					now));
		}

		// 4) 扫描积压
		var pending = backlog.Pending;
		if (pending >= t.ScanBacklog)
			alerts.Add(new Alert(
				"scan_backlog_high", AlertSeverity.Warning, "metadata-scan",
				$"待处理扫描任务 {pending}（峰值 {backlog.Peak}），阈值 {t.ScanBacklog}。",
				now));

		return alerts;
	}
}
