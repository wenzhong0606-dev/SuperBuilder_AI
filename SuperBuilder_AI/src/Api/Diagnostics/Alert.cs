using System;

namespace SuperBuilder_AI.Api.Diagnostics;

/// <summary>告警级别。</summary>
public enum AlertSeverity
{
	/// <summary>警告（如错误率偏高、积压上升）。</summary>
	Warning,
	/// <summary>严重（如错误率突破硬性阈值）。</summary>
	Critical,
}

/// <summary>
/// 一条可观测告警（OBS-01）。由 <see cref="AlertEvaluator"/> 依据指标快照产出，
/// 经 <see cref="IAlertSink"/> 发出；同一规则从触发到恢复会各发一次（恢复为 Warning 级）。
/// </summary>
/// <param name="Rule">规则标识（如 <c>route_error_rate_high</c>）。</param>
/// <param name="Severity">级别。</param>
/// <param name="Signal">触发信号（如 <c>POST /api/ask</c>）。</param>
/// <param name="Detail">人类可读说明。</param>
/// <param name="RaisedAt">触发时间（UTC）。</param>
public sealed record Alert(
	string Rule,
	AlertSeverity Severity,
	string Signal,
	string Detail,
	DateTimeOffset RaisedAt);
