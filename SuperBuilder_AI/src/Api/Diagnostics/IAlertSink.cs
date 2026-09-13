using System.Collections.Generic;

namespace SuperBuilder_AI.Api.Diagnostics;

/// <summary>
/// 告警接收端（OBS-01 端口抽象）。
/// 生产默认实现为 <see cref="LoggingAlertSink"/>（仅记录日志）；测试用 <c>TestAlertSink</c>
/// 在内存中收集，用于验证「触发 + 恢复」闭环。
/// </summary>
public interface IAlertSink
{
	/// <summary>发出一条告警（触发或恢复）。实现须异常静默。</summary>
	void Raise(Alert alert);

	/// <summary>当前已接收的告警快照（按时间升序）。</summary>
	IReadOnlyList<Alert> Snapshot();

	/// <summary>清空（测试/运维用）。</summary>
	void Clear();
}
