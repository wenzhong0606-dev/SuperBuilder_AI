using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 默认 Ask 审计 Sink：丢弃所有记录，零副作用（M6-05）。
///
/// 与未启用审计时的历史行为逐字节等价。控制器默认注入此实现，
/// 保证默认路径不采集、不输出任何审计数据。
/// </summary>
public sealed class NoOpAskAuditSink : IAskAuditSink
{
	/// <inheritdoc />
	public Task RecordAsync(
		AskAuditRecord record,
		CancellationToken ct = default)
	{
		// 显式无操作：不采集、不分配、不抛异常。
		return Task.CompletedTask;
	}
}
