namespace SuperBulider_AI.Models.BI;

/// <summary>
/// QueryPlan验证流水线结果。
///
/// Phase 2.2.5
///
/// 返回:
///
/// 修复后的QueryPlan
///
/// +
///
/// 最终ValidationResult
/// </summary>
public class QueryPlanValidationPipelineResult
{

	/// <summary>
	/// 最终QueryPlan
	/// </summary>
	public QueryPlan Plan
	{
		get;
		set;
	} = null!;



	/// <summary>
	/// 最终验证结果
	/// </summary>
	public QuerySemanticValidationResult ValidationResult
	{
		get;
		set;
	} = null!;

}