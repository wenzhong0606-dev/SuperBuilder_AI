using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.BI.Planning;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 阶段 8（原 Step 5.3.1）：Explainability 聚合。
/// 委托 <see cref="IQueryPlanExplainabilityService"/> 生成统一解释，写回上下文。
///
/// 该阶段在 Decision Gate 之后运行，其产出（Explanation）被管线最终化阶段
/// 同时用于“成功结果”与“Decision Gate 阻断提前返回”（原 Step 5.4）。
/// </summary>
public sealed class QueryPlanExplainabilityStage : IQueryPlanStage
{
	private readonly IQueryPlanExplainabilityService _explainabilityService;

	/// <summary>创建 Explainability 阶段。</summary>
	public QueryPlanExplainabilityStage(
		IQueryPlanExplainabilityService explainabilityService)
	{
		_explainabilityService = explainabilityService
			?? throw new System.ArgumentNullException(nameof(explainabilityService));
	}

	/// <inheritdoc />
	public string StageName => "Explainability";

	/// <inheritdoc />
	public Task ExecuteAsync(
		QueryPlanPipelineContext ctx,
		CancellationToken ct = default)
	{
		ctx.Explanation =
			_explainabilityService.Explain(
				ctx.Question,
				ctx.Plan,
				ctx.SemanticValidation!.ValidationResult,
				ctx.SemanticValidation.RepairTrace,
				ctx.Confidence,
				ctx.Decision);

		return Task.CompletedTask;
	}
}
