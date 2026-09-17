using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Interfaces.BI.Planning;

/// <summary>
/// QueryPlan 管线在阶段间传递的可变上下文（M5-03）。
///
/// 各阶段读取前置阶段产出的字段、写入自身产出；
/// 任一层级在验证失败 / 安全拦截时设置 <see cref="EarlyResponse"/> 触发短路。
/// 字段与 <see cref="QueryPlanPipeline"/> 原 RunAsync 的局部变量一一对应。
/// </summary>
public sealed class QueryPlanPipelineContext
{
	/// <summary>
	/// 构造管线上下文。
	/// </summary>
	public QueryPlanPipelineContext(
		string question,
		QueryIntent intent,
		long? requestedDataSourceId,
		IReadOnlyCollection<long>? authorizedDataSourceIds,
		QueryPlanLearningContext? learning = null)
	{
		Question = question;
		Intent = intent;
		RequestedDataSourceId = requestedDataSourceId;
		AuthorizedDataSourceIds = authorizedDataSourceIds;
		Learning = learning;
	}

	/// <summary>用户原始问题。</summary>
	public string Question { get; }

	/// <summary>已理解的查询意图。</summary>
	public QueryIntent Intent { get; }

	/// <summary>可选数据源约束（透传给 Builder）。</summary>
	public long? RequestedDataSourceId { get; }

	/// <summary>授权数据源集合（透传给 Builder）。</summary>
	public IReadOnlyCollection<long>? AuthorizedDataSourceIds { get; }

	/// <summary>
	/// 本次查询命中的学习规则上下文（Phase 4）。
	///
	/// 由 BIConversationService 在 Step 0.5 命中学习规则后写入，透传给 Confidence 阶段，
	/// 使「用了学习规则」成为可审计的置信度正证据。未命中时为 null。
	/// </summary>
	public QueryPlanLearningContext? Learning { get; }

	/// <summary>Step 2 构建出的查询计划。</summary>
	public QueryPlan? Plan { get; set; }

	/// <summary>Step 3 构建出的验证上下文。</summary>
	public QueryPlanValidationContext? ValidationContext { get; set; }

	/// <summary>Step 5 语义验证 + 自动修复结果。</summary>
	public QueryPlanValidationPipelineResult? SemanticValidation { get; set; }

	/// <summary>Step 5.2 Confidence 评估结果。</summary>
	public QueryPlanConfidence? Confidence { get; set; }

	/// <summary>Step 5.3 Decision Gate 决策。</summary>
	public QueryPlanDecision? Decision { get; set; }

	/// <summary>Step 5.3.1 Explainability 聚合结果。</summary>
	public QueryPlanExplanation? Explanation { get; set; }

	/// <summary>
	/// 某阶段在验证失败 / 安全拦截时提前结束设置；
	/// 管线在每阶段后检查，若非空则短路返回该 BIResponse。
	/// </summary>
	public BIResponse? EarlyResponse { get; set; }
}
