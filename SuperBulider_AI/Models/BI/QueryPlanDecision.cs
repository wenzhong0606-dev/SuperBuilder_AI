namespace SuperBulider_AI.Models.BI;

/// <summary>
/// QueryPlan Decision Gate 最终决策结果。
/// 
/// Phase 2.4
/// </summary>
public sealed class QueryPlanDecision
{
	/// <summary>
	/// Decision Gate 最终决策。
	/// </summary>
	public QueryPlanDecisionType Decision { get; set; }

	/// <summary>
	/// Decision 所依据的 Confidence。
	/// </summary>
	public QueryPlanConfidence Confidence { get; set; } = new();

	/// <summary>
	/// 是否允许 QueryPlan 进入 SQL Builder。
	/// </summary>
	public bool ShouldExecute { get; set; }

	/// <summary>
	/// 是否需要用户或上层流程进一步确认。
	/// </summary>
	public bool RequiresConfirmation { get; set; }

	/// <summary>
	/// Decision Gate 给出的主要原因。
	/// </summary>
	public string? Reason { get; set; }
}