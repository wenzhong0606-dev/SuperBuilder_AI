using SuperBulider_AI.Models.BI;


namespace SuperBulider_AI.Interfaces.BI;

/// <summary>
/// Query计划自动修复服务。
///
/// Phase 2.2.5
///
/// 输入:
///
/// QueryIntent
/// ValidationResult
///
/// 输出:
///
/// 修复后的 QueryIntent
///
/// 注意:
///
/// 不直接修改 QueryPlan。
/// QueryPlan 属于执行模型。
/// </summary>
public interface IQueryPlanRepairService
{

	/// <summary>
	/// 修复查询意图。
	/// </summary>
	Task<QueryIntent> RepairAsync(
		QueryIntent intent,
		QuerySemanticValidationResult validationResult);

}