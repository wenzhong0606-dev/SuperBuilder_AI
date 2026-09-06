using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Interfaces.BI.Planning;

/// <summary>
/// QueryPlan 决策审计记录输出端口（M5-09 AI Decision Audit）。
///
/// 实现可以是：丢弃（NoOp）、结构化日志（Log）、消息队列、数据库落盘等。
/// Pipeline 在每次运行结束时（无论成功或短路）向该端口提交一条
/// <see cref="QueryPlanDecisionAuditRecord"/>。
///
/// 默认实现为 <see cref="NoOpDecisionAuditSink"/>（零行为），
/// 保证未配置时与历史行为逐字节等价、对 Golden 契约免疫。
/// </summary>
public interface IDecisionAuditSink
{
	/// <summary>
	/// 提交一条审计记录。
	/// </summary>
	/// <param name="record">本次决策全过程的可审计记录。</param>
	/// <param name="ct">取消令牌。</param>
	Task RecordAsync(
		QueryPlanDecisionAuditRecord record,
		CancellationToken ct = default);
}
