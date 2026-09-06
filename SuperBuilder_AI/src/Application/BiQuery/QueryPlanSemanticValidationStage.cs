using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 阶段 5（原 Step 5 后半 + Step 5.1）：QueryPlan 语义验证 + 自动修复。
///
/// 委托 <see cref="IQueryPlanValidationPipeline"/> 执行验证 / 修复 / 复验，
/// 把结果写回上下文（含修复后的 Plan）。
///
/// 若最终验证仍无效，按原语义生成内联 Explanation 并置 EarlyResponse 短路返回
/// （Success=false，ErrorMessage 为错误聚合，Explanation 为该步内联解释）；
/// 此路径在 Explainability 阶段之前返回，与原 Step 5.1 一致。
/// </summary>
public sealed class QueryPlanSemanticValidationStage : IQueryPlanStage
{
	private readonly IQueryPlanValidationPipeline _validationPipeline;
	private readonly IQueryPlanExplainabilityService _explainabilityService;

	/// <summary>创建语义验证阶段。</summary>
	public QueryPlanSemanticValidationStage(
		IQueryPlanValidationPipeline validationPipeline,
		IQueryPlanExplainabilityService explainabilityService)
	{
		_validationPipeline = validationPipeline
			?? throw new System.ArgumentNullException(nameof(validationPipeline));
		_explainabilityService = explainabilityService
			?? throw new System.ArgumentNullException(nameof(explainabilityService));
	}

	/// <inheritdoc />
	public string StageName => "SemanticValidation";

	/// <inheritdoc />
	public async Task ExecuteAsync(
		QueryPlanPipelineContext ctx,
		CancellationToken ct = default)
	{
		var semanticValidation =
			await _validationPipeline.ValidateAsync(
				ctx.Plan!,
				ctx.ValidationContext!,
				ctx.Question);

		ctx.SemanticValidation = semanticValidation;
		ctx.Plan = semanticValidation.Plan;

		if (!semanticValidation.ValidationResult.IsValid)
		{
			var validationExplanation =
				_explainabilityService.Explain(
					ctx.Question,
					ctx.Plan,
					semanticValidation.ValidationResult,
					semanticValidation.RepairTrace,
					null,
					null);

			ctx.EarlyResponse = new BIResponse
			{
				Success = false,

				Question = ctx.Question,

				ErrorMessage =
					string.Join(
						"\n",
						semanticValidation.ValidationResult.Errors
							.Select(x => x.Message)),

				Explanation = validationExplanation
			};
		}
	}
}
