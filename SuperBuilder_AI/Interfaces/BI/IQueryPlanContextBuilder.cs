using SuperBuilder_AI.Models.BI;


namespace SuperBuilder_AI.Interfaces.BI;

/// <summary>
/// QueryPlan验证上下文构建器。
///
/// 职责:
///
/// QueryPlan
///      ↓
/// QueryPlanValidationContext
///
/// 用于后续:
///
/// QueryPlanMetadataValidator
///
/// QuerySemanticValidator
///
/// </summary>
public interface IQueryPlanContextBuilder
{
	/// <summary>
	/// 根据 QueryPlan 构建验证上下文。
	/// </summary>
	Task<QueryPlanValidationContext> BuildAsync(
		QueryPlan plan);
}