using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.BI.Planning;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 阶段 7（原 Step 5.3）：QueryPlan Decision Gate。
/// 委托 <see cref="IQueryPlanDecisionGate"/> 评估，把决策写回上下文。
/// 本阶段不直接短路——阻断判定在管线最终化阶段（原 Step 5.4）结合 Explanation 处理。
/// </summary>
public sealed class QueryPlanDecisionGateStage : IQueryPlanStage
{
	private readonly IQueryPlanDecisionGate _queryPlanDecisionGate;

	/// <summary>创建 Decision Gate 阶段。</summary>
	public QueryPlanDecisionGateStage(
		IQueryPlanDecisionGate queryPlanDecisionGate)
	{
		_queryPlanDecisionGate = queryPlanDecisionGate
			?? throw new System.ArgumentNullException(nameof(queryPlanDecisionGate));
	}

	/// <inheritdoc />
	public string StageName => "DecisionGate";

	/// <inheritdoc />
	public Task ExecuteAsync(
		QueryPlanPipelineContext ctx,
		CancellationToken ct = default)
	{
		ctx.Decision =
			_queryPlanDecisionGate.Evaluate(ctx.Confidence!);

		return Task.CompletedTask;
	}
}
