using SuperBulider_AI.Models.BI;

namespace SuperBulider_AI.Interfaces.BI;

/// <summary>
/// QueryPlan Confidence 评估服务。
///
/// Phase 2.4
/// QueryPlan Confidence & Decision Gate
///
/// 职责：
/// 1. 消费已经完成的 Validation 结果。
/// 2. 消费已经完成的 Repair Trace。
/// 3. 消费 QueryPlan 本身。
/// 4. 生成可解释的 QueryPlan Confidence。
///
/// 注意：
///
/// 本接口不负责：
/// - QueryPlan 构建
/// - QueryPlan Validation
/// - QueryPlan Repair
/// - SQL 生成
/// - SQL 执行
/// - Decision Gate
///
/// Confidence Service 只负责：
///
/// Evidence
///     ↓
/// Confidence Evaluation
/// </summary>
public interface IQueryPlanConfidenceService
{
	/// <summary>
	/// 对 QueryPlan 进行置信度评估。
	/// </summary>
	/// <param name="plan">
	/// 已经完成 QueryPlan 构建的查询计划。
	/// </param>
	/// <param name="validationResult">
	/// QueryPlan Validation Pipeline 的最终结果。
	/// </param>
	/// <param name="repairTrace">
	/// QueryPlan Repair Loop 的完整 Trace。
	/// 如果初始 Validation 已通过，可以为 null。
	/// </param>
	/// <param name="question">
	/// 用户原始问题。
	/// 用于补充 Confidence 的语义上下文。
	/// </param>
	/// <param name="cancellationToken">
	/// 异步取消令牌。
	/// </param>
	/// <returns>
	/// QueryPlan Confidence 评估结果。
	/// </returns>
	Task<QueryPlanConfidence> EvaluateAsync(
		QueryPlan plan,
		QueryPlanValidationPipelineResult validationResult,
		QueryPlanRepairTrace? repairTrace,
		string question,
		CancellationToken cancellationToken = default);
}