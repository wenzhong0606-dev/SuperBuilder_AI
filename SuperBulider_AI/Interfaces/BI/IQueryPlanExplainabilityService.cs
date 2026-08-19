using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Interfaces.BI;

/// <summary>
/// QueryPlan Explainability 服务。
///
/// Phase 2.5。
///
/// 负责消费 QueryPlan Pipeline 已经产生的：
///
/// QueryPlan
/// Validation
/// RepairTrace
/// Confidence
/// Decision
///
/// 并生成统一的 QueryPlanExplanation。
///
/// 本服务不参与：
/// - QueryPlan 构建
/// - Validation
/// - Repair
/// - Confidence 计算
/// - Decision Gate
/// - SQL 构建
/// - SQL 执行
/// </summary>
public interface IQueryPlanExplainabilityService
{
	/// <summary>
	/// 创建 QueryPlan Explainability。
	/// </summary>
	QueryPlanExplanation Explain(
		string? question,
		QueryPlan? plan,
		QuerySemanticValidationResult? validationResult,
		QueryPlanRepairTrace? repairTrace,
		QueryPlanConfidence? confidence,
		QueryPlanDecision? decision);
}