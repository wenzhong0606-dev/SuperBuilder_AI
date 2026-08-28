using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Interfaces.BI.Planning;

/// <summary>
/// QueryPlan 编排管线端口。
///
/// 负责将 QueryIntent 构建为 QueryPlan，
/// 并完成：
///
/// 1. Metadata 关系完整性验证
/// 2. 语义验证 + 自动修复（Validate / Repair / ReValidate）
/// 3. Confidence 评估
/// 4. Decision Gate
/// 5. Explainability 聚合
///
/// 该职责从 BIConversationService 抽取而来，行为保持不变。
/// </summary>
public interface IQueryPlanPipeline
{
	/// <summary>
	/// 执行 QueryPlan 构建 + 验证 + Confidence + Decision Gate + Explainability 的串联。
	/// </summary>
	/// <param name="question">用户原始问题。</param>
	/// <param name="intent">已理解的查询意图。</param>
	/// <returns>
	/// 成功时返回 Plan / Confidence / Decision / Explanation；
	/// 当验证失败或 Decision Gate 阻断而提前结束时，EarlyResponse 非空，调用方应直接返回它。
	/// </returns>
	Task<QueryPlanPipelineResult> RunAsync(
		string question,
		QueryIntent intent);
}

/// <summary>
/// QueryPlanPipeline 的执行结果。
/// </summary>
public sealed class QueryPlanPipelineResult
{
	/// <summary>
	/// 最终查询计划（已通过 Decision Gate）。
	/// </summary>
	public QueryPlan Plan { get; set; } = null!;

	/// <summary>
	/// 语义验证 + 自动修复管线结果。
	/// </summary>
	public QueryPlanValidationPipelineResult SemanticValidation { get; set; } = null!;

	/// <summary>
	/// Confidence 评估结果。
	/// </summary>
	public QueryPlanConfidence Confidence { get; set; } = null!;

	/// <summary>
	/// Decision Gate 决策。
	/// </summary>
	public QueryPlanDecision Decision { get; set; } = null!;

	/// <summary>
	/// Explainability 聚合结果。
	/// </summary>
	public QueryPlanExplanation Explanation { get; set; } = null!;

	/// <summary>
	/// 当管线在验证失败或 Decision Gate 阻断时提前结束时设置；
	/// 调用方应直接将其作为 BIResponse 返回。
	/// </summary>
	public BIResponse? EarlyResponse { get; set; }
}
