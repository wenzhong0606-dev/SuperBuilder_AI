using System;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 阶段 2（原 Step 3）：构建 QueryPlan 验证上下文。
/// 把已构建的 QueryPlan 交给 <see cref="IQueryPlanContextBuilder"/>。
/// </summary>
public sealed class QueryPlanContextStage : IQueryPlanStage
{
	private readonly IQueryPlanContextBuilder _queryPlanContextBuilder;

	/// <summary>创建上下文构建阶段。</summary>
	public QueryPlanContextStage(IQueryPlanContextBuilder queryPlanContextBuilder)
	{
		_queryPlanContextBuilder = queryPlanContextBuilder
			?? throw new ArgumentNullException(nameof(queryPlanContextBuilder));
	}

	/// <inheritdoc />
	public string StageName => "Context";

	/// <inheritdoc />
	public async Task ExecuteAsync(
		QueryPlanPipelineContext ctx,
		CancellationToken ct = default)
	{
		ctx.ValidationContext =
			await _queryPlanContextBuilder.BuildAsync(ctx.Plan!);
	}
}
