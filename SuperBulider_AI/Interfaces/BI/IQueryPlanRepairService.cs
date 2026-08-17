using SuperBulider_AI.Models.BI;


namespace SuperBulider_AI.Interfaces.BI;

/// <summary>
/// QueryPlan自动修复服务接口。
///
/// Phase 2.2.5
///
/// 职责:
///
/// 根据 QueryPlan Validation 错误
///
/// 自动修复:
///
/// - Column引用错误
/// - Metric字段错误
/// - Filter字段错误
///
/// 第一阶段:
///
/// Metadata Semantic Repair
///
/// 不负责:
///
/// - SQL生成
/// - SQL执行
/// - Qwen推理
///
/// </summary>
public interface IQueryPlanRepairService
{
	/// <summary>
	/// 修复QueryPlan。
	/// </summary>
	/// <param name="request">
	/// 包含:
	///
	/// QueryPlan
	/// ValidationContext
	/// ValidationErrors
	///
	/// </param>
	Task<QueryPlanRepairResult> RepairAsync(
		QueryPlanRepairRequest request);
}