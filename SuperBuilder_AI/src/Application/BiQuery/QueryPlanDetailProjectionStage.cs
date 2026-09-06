using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.BI.Planning;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 阶段 4（原 Step 5 前半）：最终 Metadata 投影兜底。
///
/// 调用 <see cref="DetailQueryProjectionPolicy.Apply"/>，确保新增字段与软删除条件
/// 同样经过后续验证 / Confidence / Decision Gate，而非在 SQL Builder 前临时绕过门禁。
/// 本阶段无状态、不触发提前返回。
/// </summary>
public sealed class QueryPlanDetailProjectionStage : IQueryPlanStage
{
	/// <inheritdoc />
	public string StageName => "DetailProjection";

	/// <inheritdoc />
	public Task ExecuteAsync(
		QueryPlanPipelineContext ctx,
		CancellationToken ct = default)
	{
		DetailQueryProjectionPolicy.Apply(
			ctx.Plan!,
			ctx.ValidationContext!,
			ctx.Question);

		return Task.CompletedTask;
	}
}
