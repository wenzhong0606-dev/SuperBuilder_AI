using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Interfaces.BI;

/// <summary>
/// QueryPlan 自动修复服务。
///
/// Phase 2.3.1
///
/// 负责:
///
/// QueryPlan
///      +
/// ValidationResult
///      +
/// Metadata Context
///
///        ↓
///
/// Repair
///
///        ↓
///
/// 新 QueryPlan
///
///
/// 注意:
/// 不负责:
/// - SQL生成
/// - SQL执行
/// - Metadata检索
///
/// 只负责修复查询计划语义问题。
/// </summary>
public interface IQueryPlanRepairService
{
	/// <summary>
	/// 根据语义验证失败结果，
	/// 自动修复 QueryPlan。
	///
	/// 流程:
	///
	/// QueryPlan
	///      |
	///      v
	/// ValidationResult
	///      |
	///      v
	/// Repair
	///      |
	///      v
	/// Repaired QueryPlan
	///
	/// </summary>
	/// <param name="request">
	/// QueryPlan修复上下文
	/// </param>
	/// <returns>
	/// 修复结果
	/// </returns>
	Task<QueryPlanRepairResult> RepairAsync(
		QueryPlanRepairRequest request);
}