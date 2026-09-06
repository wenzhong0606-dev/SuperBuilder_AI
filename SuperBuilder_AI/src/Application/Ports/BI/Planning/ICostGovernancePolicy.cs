using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Interfaces.BI.Planning;

/// <summary>
/// 查询成本治理策略（M5-06）。
///
/// 消费 <see cref="IQueryCostClassifier"/> 的评估信号与治理上下文，
/// 依据可配置阈值产出 <see cref="CostGovernanceVerdict"/>（放行/降级/拒绝）。
/// </summary>
public interface ICostGovernancePolicy
{
	/// <summary>
	/// 评估成本治理裁决。
	/// </summary>
	CostGovernanceVerdict Evaluate(
		QueryCostAssessment assessment,
		QueryPlanDecision? currentDecision,
		CostGovernanceContext context);
}
