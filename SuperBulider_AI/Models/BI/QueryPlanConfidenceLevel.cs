namespace SuperBulider_AI.Models.BI;

/// <summary>
/// QueryPlan 置信度等级。
/// 
/// Phase 2.4
/// QueryPlan Confidence & Decision Gate
/// </summary>
public enum QueryPlanConfidenceLevel
{
	/// <summary>
	/// 低置信度。
	/// QueryPlan 不应自动进入 SQL Builder。
	/// </summary>
	Low = 0,

	/// <summary>
	/// 中等置信度。
	/// QueryPlan 可以进入受控确认流程，
	/// 但不应直接自动执行。
	/// </summary>
	Medium = 1,

	/// <summary>
	/// 高置信度。
	/// QueryPlan 满足自动进入 SQL Builder 的置信度条件。
	/// </summary>
	High = 2
}