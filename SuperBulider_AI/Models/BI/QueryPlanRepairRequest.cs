using SuperBulider_AI.Models.Metadata;


namespace SuperBulider_AI.Models.BI;

/// <summary>
/// QueryPlan自动修复请求。
///
/// Phase 2.2.5
///
/// 用于:
///
/// QueryPlan验证失败后
/// 将:
///
/// QueryPlan
/// Validation Error
/// Metadata Context
///
/// 提交给Repair Service。
///
/// </summary>
public class QueryPlanRepairRequest
{
	/// <summary>
	/// 当前查询计划。
	///
	/// 可能包含错误字段、
	/// 错误Metric、
	/// 错误Filter。
	/// </summary>
	public QueryPlan Plan
	{
		get;
		set;
	} = null!;



	/// <summary>
	/// QueryPlan验证上下文。
	///
	/// 包含:
	///
	/// MetadataTable
	/// MetadataColumn
	/// MetadataSemantic
	///
	/// </summary>
	public QueryPlanValidationContext Context
	{
		get;
		set;
	} = null!;



	/// <summary>
	/// 验证产生的问题。
	///
	/// 来源:
	///
	/// QuerySemanticValidator
	/// </summary>
	public List<SemanticValidationError> Errors
	{
		get;
		set;
	} = new();



	/// <summary>
	/// 原始用户问题。
	///
	/// 提供给AI Repair使用。
	/// </summary>
	public string Question
	{
		get;
		set;
	} = string.Empty;
}