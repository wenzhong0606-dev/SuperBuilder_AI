using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Interfaces.BI;

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

	/// <summary>
	/// 对 QueryPlan 进行置信度评估，并带上本次命中的学习规则上下文（Phase 4）。
	///
	/// <para>
	/// 命中学习规则时，实现应在证据中标记 <c>LearningApplied</c> 并把置信度保底至 Medium，
	/// 使「用了学习规则」可见、可审计 —— 与用户当轮显式表纠正同待遇。
	/// </para>
	///
	/// <para>
	/// 本重载以默认接口实现提供：默认忽略 <paramref name="learning"/> 并转调四参重载。
	/// 因此既有实现类与测试替身无需改动即可编译，未重写本方法的实现行为保持不变。
	/// </para>
	/// </summary>
	/// <param name="learning">
	/// 本次查询命中的学习规则上下文；为 null 或空集合时行为与四参重载一致。
	/// </param>
	Task<QueryPlanConfidence> EvaluateAsync(
		QueryPlan plan,
		QueryPlanValidationPipelineResult validationResult,
		QueryPlanRepairTrace? repairTrace,
		string question,
		QueryPlanLearningContext? learning,
		CancellationToken cancellationToken = default)
		=> EvaluateAsync(
			plan,
			validationResult,
			repairTrace,
			question,
			cancellationToken);
}