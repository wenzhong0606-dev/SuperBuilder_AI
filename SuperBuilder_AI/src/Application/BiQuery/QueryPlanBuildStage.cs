using System;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.AI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 阶段 1（原 Step 2）：构建 QueryPlan。
/// 把 QueryIntent + 数据源约束交给 <see cref="IQueryPlanBuilder"/>。
/// 不触发提前返回（构建失败按原语义向上传播）。
/// </summary>
public sealed class QueryPlanBuildStage : IQueryPlanStage
{
	private readonly IQueryPlanBuilder _queryPlanBuilder;

	/// <summary>创建构建阶段。</summary>
	public QueryPlanBuildStage(IQueryPlanBuilder queryPlanBuilder)
	{
		_queryPlanBuilder = queryPlanBuilder
			?? throw new ArgumentNullException(nameof(queryPlanBuilder));
	}

	/// <inheritdoc />
	public string StageName => "Build";

	/// <inheritdoc />
	public async Task ExecuteAsync(
		QueryPlanPipelineContext ctx,
		CancellationToken ct = default)
	{
		ctx.Plan = await _queryPlanBuilder.BuildAsync(
			ctx.Intent,
			ctx.RequestedDataSourceId,
			ctx.AuthorizedDataSourceIds);
	}
}
