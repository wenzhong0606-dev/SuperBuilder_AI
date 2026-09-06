using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Interfaces.BI;

/// <summary>
/// Ask 审计记录输出端口（M6-05）。
///
/// 实现可以是：丢弃（NoOp）、结构化日志（Log）、消息队列、数据库落盘等。
/// 控制器在每次 Ask 调用结束时（无论成功或失败）向该端口提交一条
/// <see cref="AskAuditRecord"/>。
///
/// 默认实现为 <see cref="NoOpAskAuditSink"/>（零行为），
/// 保证未配置时与历史行为逐字节等价、对 Golden 契约免疫。
/// </summary>
public interface IAskAuditSink
{
	/// <summary>提交一条 Ask 审计记录。</summary>
	/// <param name="record">本次 Ask 调用的可审计记录（敏感字段已脱敏）。</param>
	/// <param name="ct">取消令牌。</param>
	Task RecordAsync(
		AskAuditRecord record,
		CancellationToken ct = default);
}
