using SuperBulider_AI.Models.AI;

namespace SuperBulider_AI.Models.BI;

/// <summary>
/// QueryPlan 修复请求上下文。
///
/// Phase 2.3.1
///
/// 用于 QueryPlan Repair Loop。
///
/// 包含:
///
/// 1. 当前失败的 QueryPlan
/// 2. 语义验证结果
/// 3. 验证上下文
/// 4. 用户原始问题
/// 5. 当前修复次数
///
/// 流程:
///
/// QueryPlan
///      |
///      v
/// QuerySemanticValidationResult
///      |
///      v
/// QueryPlanRepairRequest
///      |
///      v
/// QueryPlanRepairService
///      |
///      v
/// QueryPlanRepairResult
///
/// </summary>
public class QueryPlanRepairRequest
{
	/// <summary>
	/// 当前需要修复的查询计划。
	///
	/// Repair必须基于当前Plan进行修改，
	/// 而不是重新猜测用户意图。
	/// </summary>
	public QueryPlan QueryPlan { get; set; } = default!;


	/// <summary>
	/// QueryPlan语义验证结果。
	///
	/// 包含:
	/// - 缺失字段
	/// - 不匹配Metric
	/// - 无效Filter
	/// - 表关系错误
	///
	/// Repair根据该结果决定修复策略。
	/// </summary>
	public QuerySemanticValidationResult ValidationResult { get; set; } = default!;


	/// <summary>
	/// QueryPlan验证上下文。
	///
	/// 提供:
	/// - Metadata信息
	/// - 当前查询上下文
	/// - 数据源信息
	///
	/// 用于辅助Repair决策。
	/// </summary>
	public QueryPlanValidationContext? ValidationContext { get; set; }


	/// <summary>
	/// 用户原始问题。
	///
	/// 示例:
	///
	/// "查询2025年销售额"
	///
	/// Repair时用于保持用户语义一致。
	/// </summary>
	public string OriginalQuestion { get; set; } = string.Empty;


	/// <summary>
	/// 当前Repair次数。
	///
	/// 防止:
	///
	/// Validation
	///    |
	/// Repair
	///    |
	/// Validation
	///    |
	/// 无限循环
	///
	/// </summary>
	public int RetryCount { get; set; }
}