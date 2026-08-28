using SuperBuilder_AI.Models.AI;

namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// QueryPlan 修复结果。
///
/// Phase 2.3.1
///
/// QueryPlan Repair Loop 输出模型。
///
/// 负责返回:
///
/// 1. 是否修复成功
/// 2. 修复后的 QueryPlan
/// 3. 修复动作记录
///
/// 流程:
///
/// QueryPlanRepairRequest
///
///        ↓
///
/// QueryPlanRepairService
///
///        ↓
///
/// QueryPlanRepairResult
///
///        ↓
///
/// QueryPlanValidationPipeline
///
///        ↓
///
/// Re-Validate
///
/// </summary>
public class QueryPlanRepairResult
{
	/// <summary>
	/// 是否成功完成修复。
	///
	/// true:
	///     RepairedPlan 可继续进入验证。
	///
	/// false:
	///     当前问题无法自动修复。
	/// </summary>
	public bool Success { get; set; }


	/// <summary>
	/// 修复后的 QueryPlan。
	///
	/// Repair成功后:
	///
	/// Validation
	///        ↓
	/// Repair
	///        ↓
	/// New QueryPlan
	///
	/// Pipeline 会使用该Plan重新验证。
	/// </summary>
	public QueryPlan? RepairedPlan { get; set; }


	/// <summary>
	/// 修复动作记录。
	///
	/// 用于:
	///
	/// 1. 调试
	/// 2. AI解释
	/// 3. 后续学习
	///
	/// 示例:
	///
	/// [
	///   "Replace field Amount with NetAmount",
	///   "Change table SalesOrderDetail to SalesOrder"
	/// ]
	///
	/// </summary>
	public List<string> RepairActions { get; set; }
		= new();


	/// <summary>
	/// 修复失败原因。
	///
	/// 当 Success=false 时使用。
	///
	/// 示例:
	///
	/// "No matching metadata field found"
	/// </summary>
	public string? FailureReason { get; set; }


	/// <summary>
	/// 修复前后的差异说明。
	///
	/// 用于日志和可观测性。
	/// </summary>
	public string? Explanation { get; set; }
}