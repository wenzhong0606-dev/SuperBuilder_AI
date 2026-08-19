using SuperBulider_AI.Models.BI;

namespace SuperBulider_AI.Interfaces.BI;

/// <summary>
/// QueryPlan Decision Gate。
///
/// Phase 2.4
/// QueryPlan Confidence & Decision Gate
///
/// 职责：
///
/// QueryPlanConfidence
///        ↓
/// Decision Gate
///        ↓
/// Reject / Confirm / Proceed
///
/// Decision Gate 不重新计算 Confidence。
/// </summary>
public interface IQueryPlanDecisionGate
{
	/// <summary>
	/// 根据 QueryPlan Confidence 生成最终 Decision。
	/// </summary>
	/// <param name="confidence">
	/// QueryPlan Confidence 评估结果。
	/// </param>
	/// <returns>
	/// QueryPlan Decision。
	/// </returns>
	QueryPlanDecision Evaluate(
		QueryPlanConfidence confidence);
}