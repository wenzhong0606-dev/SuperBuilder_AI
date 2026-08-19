namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// QueryPlan Decision Gate 最终决策。
/// 
/// Phase 2.4
/// </summary>
public enum QueryPlanDecisionType
{
	/// <summary>
	/// 拒绝继续执行。
	/// QueryPlan 不允许进入 SQL Builder。
	/// </summary>
	Reject = 0,

	/// <summary>
	/// 需要进一步确认。
	/// QueryPlan 当前不允许无条件自动执行。
	/// </summary>
	Confirm = 1,

	/// <summary>
	/// 允许继续执行。
	/// QueryPlan 可以进入 SQL Builder。
	/// </summary>
	Proceed = 2
}