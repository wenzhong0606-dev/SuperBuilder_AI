using System;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 阶段 3（原 Step 4）：Metadata 关系完整性验证。
///
/// 委托 <see cref="QueryPlanMetadataValidator"/> 验证；
/// 任何异常按原语义转为 EarlyResponse（Success=false，ErrorMessage=异常消息），
/// 令管线短路返回（不生成 Explanation）。
/// </summary>
public sealed class QueryPlanMetadataIntegrityStage : IQueryPlanStage
{
	private readonly QueryPlanMetadataValidator _queryPlanMetadataValidator;

	/// <summary>创建元数据完整性阶段。</summary>
	public QueryPlanMetadataIntegrityStage(
		QueryPlanMetadataValidator queryPlanMetadataValidator)
	{
		_queryPlanMetadataValidator = queryPlanMetadataValidator
			?? throw new ArgumentNullException(nameof(queryPlanMetadataValidator));
	}

	/// <inheritdoc />
	public string StageName => "MetadataIntegrity";

	/// <inheritdoc />
	public Task ExecuteAsync(
		QueryPlanPipelineContext ctx,
		CancellationToken ct = default)
	{
		try
		{
			_queryPlanMetadataValidator.Validate(
				ctx.Plan!,
				ctx.ValidationContext!);
		}
		catch (Exception ex)
		{
			ctx.EarlyResponse = new BIResponse
			{
				Success = false,

				Question = ctx.Question,

				ErrorMessage = ex.Message
			};
		}

		return Task.CompletedTask;
	}
}
