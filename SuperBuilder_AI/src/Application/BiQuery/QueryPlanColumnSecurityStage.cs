using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 阶段（M5-05）：列级安全净化。
///
/// 在 Context 阶段之后、MetadataIntegrity 之前执行：
/// 解析当前身份的列安全上下文，将未授权受限列从 Plan 剔除
/// （不进入 Plan / SQL / 结果）。若主投影被全部清空，则置 EarlyResponse
/// 以 403 拒绝（与原 Step 4 / 5.1 提前返回语义一致）。
/// </summary>
public sealed class QueryPlanColumnSecurityStage : IQueryPlanStage
{
	private readonly IColumnSecurityContextResolver _resolver;
	private readonly IColumnSecurityPolicy _policy;

	/// <summary>创建列级安全阶段。</summary>
	public QueryPlanColumnSecurityStage(
		IColumnSecurityContextResolver resolver,
		IColumnSecurityPolicy policy)
	{
		_resolver = resolver ?? throw new System.ArgumentNullException(nameof(resolver));
		_policy = policy ?? throw new System.ArgumentNullException(nameof(policy));
	}

	/// <inheritdoc />
	public string StageName => "ColumnSecurity";

	/// <inheritdoc />
	public async Task ExecuteAsync(
		QueryPlanPipelineContext ctx,
		CancellationToken ct = default)
	{
		if (ctx.Plan is null) return;

		var secCtx = await _resolver.ResolveAsync(ctx.Plan, ct);
		var result = await _policy.ApplyAsync(ctx.Plan, secCtx, ctx.ValidationContext, ct);

		if (result.RequiresRejection)
		{
			ctx.EarlyResponse = new BIResponse
			{
				Success = false,
				Question = ctx.Question,
				ErrorMessage = "查询所请求的列均属受限字段且无访问授权，已在执行前阻断。",
				Explanation = null
			};
		}
	}
}
