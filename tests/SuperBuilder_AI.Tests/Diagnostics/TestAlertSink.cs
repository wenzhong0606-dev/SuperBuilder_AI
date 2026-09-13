using System.Collections.Generic;
using System.Linq;
using SuperBuilder_AI.Api.Diagnostics;

namespace SuperBuilder_AI.Tests.Diagnostics;

/// <summary>
/// 测试用告警接收端（OBS-01）。在内存收集所有 Raise 的告警，供单元测验证
/// 「触发 + 恢复」闭环，不依赖任何外部系统。
/// </summary>
public sealed class TestAlertSink : IAlertSink
{
	private readonly List<Alert> _alerts = new();

	public IReadOnlyList<Alert> Snapshot() => _alerts.ToArray();

	public void Raise(Alert alert) => _alerts.Add(alert);

	public void Clear() => _alerts.Clear();

	/// <summary>按规则筛选已接收告警（按时间升序）。</summary>
	public IReadOnlyList<Alert> OfRule(string rule) =>
		_alerts.Where(a => a.Rule == rule).ToArray();
}
