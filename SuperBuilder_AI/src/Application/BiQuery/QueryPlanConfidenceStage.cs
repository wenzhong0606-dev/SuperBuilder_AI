using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.BI.Planning;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 阶段 6（原 Step 5.2）：QueryPlan Confidence 评估。
/// 委托 <see cref="IQueryPlanConfidenceService"/>，把结果写回上下文。
/// </summary>
public sealed class QueryPlanConfidenceStage : IQueryPlanStage
{
	private readonly IQueryPlanConfidenceService _queryPlanConfidenceService;

	/// <summary>创建 Confidence 阶段。</summary>
	public QueryPlanConfidenceStage(
		IQueryPlanConfidenceService queryPlanConfidenceService)
	{
		_queryPlanConfidenceService = queryPlanConfidenceService
			?? throw new System.ArgumentNullException(nameof(queryPlanConfidenceService));
	}

	/// <inheritdoc />
	public string StageName => "Confidence";

	/// <inheritdoc />
	public async Task ExecuteAsync(
		QueryPlanPipelineContext ctx,
		CancellationToken ct = default)
	{
		ctx.Confidence =
			await _queryPlanConfidenceService.EvaluateAsync(
				ctx.Plan!,
				ctx.SemanticValidation!,
				ctx.SemanticValidation!.RepairTrace,
				ctx.Question);
	}
}
