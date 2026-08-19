using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.BI;


namespace SuperBuilder_AI.Interfaces.BI;


/// <summary>
/// QueryPlan验证与自动修复Pipeline。
///
/// Phase 2.2.5
/// </summary>
public interface IQueryPlanValidationPipeline
{

	Task<QueryPlanValidationPipelineResult>
		ValidateAsync(
			QueryPlan plan,
			QueryPlanValidationContext context,
			string question);

}