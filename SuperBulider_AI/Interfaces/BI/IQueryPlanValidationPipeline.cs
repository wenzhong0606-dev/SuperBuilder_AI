using SuperBulider_AI.Models.AI;
using SuperBulider_AI.Models.BI;


namespace SuperBulider_AI.Interfaces.BI;


/// <summary>
/// QueryPlan验证与自动修复Pipeline。
///
/// Phase 2.2.5
/// </summary>
public interface IQueryPlanValidationPipeline
{

	Task<QuerySemanticValidationResult>
		ValidateAsync(
			QueryPlan plan,
			QueryPlanValidationContext context,
			string question);

}