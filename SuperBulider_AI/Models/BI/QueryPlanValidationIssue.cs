namespace SuperBulider_AI.Models.BI;

/// <summary>
/// 查询计划验证问题。
///
/// 用于描述 QueryPlan 在验证过程中发现的：
///
/// 1. 错误
/// 2. 警告
///
/// QueryPlanValidationIssue 不负责修复问题，
/// 只负责保存诊断信息。
/// </summary>
public class QueryPlanValidationIssue
{
	/// <summary>
	/// 问题代码。
	///
	/// 例如：
	///
	/// PLAN_NULL
	/// DATASOURCE_INVALID
	/// TABLE_NOT_FOUND
	/// FIELD_NOT_FOUND
	/// FIELD_TABLE_MISMATCH
	/// METRIC_INVALID
	/// DIMENSION_INVALID
	/// FILTER_INVALID
	/// ORDER_INVALID
	/// JOIN_INVALID
	/// AGGREGATION_INVALID
	/// LIMIT_INVALID
	/// </summary>
	public string Code { get; set; } = string.Empty;

	/// <summary>
	/// 问题描述。
	/// </summary>
	public string Message { get; set; } = string.Empty;

	/// <summary>
	/// 对应的 QueryPlan 属性或业务对象。
	///
	/// 例如：
	///
	/// Tables
	/// Fields
	/// Metrics
	/// Filters
	/// Joins
	/// </summary>
	public string? Property { get; set; }

	/// <summary>
	/// 是否为错误。
	///
	/// true：
	/// 当前 QueryPlan 不能继续执行。
	///
	/// false：
	/// 当前问题属于警告，不阻止执行。
	/// </summary>
	public bool IsError { get; set; }

	/// <summary>
	/// 创建错误问题。
	/// </summary>
	public static QueryPlanValidationIssue Error(
		string code,
		string message,
		string? property = null)
	{
		return new QueryPlanValidationIssue
		{
			Code = code,
			Message = message,
			Property = property,
			IsError = true
		};
	}

	/// <summary>
	/// 创建警告问题。
	/// </summary>
	public static QueryPlanValidationIssue Warning(
		string code,
		string message,
		string? property = null)
	{
		return new QueryPlanValidationIssue
		{
			Code = code,
			Message = message,
			Property = property,
			IsError = false
		};
	}
}