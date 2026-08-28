using System;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// QueryPlan 编排管线。
///
/// 从 BIConversationService 抽取而来，负责：
///
/// QueryIntent
///     ↓
/// QueryPlan 构建
///     ↓
/// Metadata 关系完整性验证
///     ↓
/// 语义验证 + 自动修复
///     ↓
/// Confidence
///     ↓
/// Decision Gate
///     ↓
/// Explainability
///
/// 纯结构化重构：步骤与早期返回条件与 BIConversationService 原实现完全一致，
/// 仅将实现从对话服务平移到本管线，逻辑与输出保持不变。
/// </summary>
public sealed class QueryPlanPipeline : IQueryPlanPipeline
{
	private readonly IQueryPlanBuilder
		_queryPlanBuilder;

	private readonly IQueryPlanContextBuilder
		_queryPlanContextBuilder;

	private readonly QueryPlanMetadataValidator
		_queryPlanMetadataValidator;

	private readonly IQueryPlanValidationPipeline
		_validationPipeline;

	private readonly IQueryPlanConfidenceService
		_queryPlanConfidenceService;

	private readonly IQueryPlanDecisionGate
		_queryPlanDecisionGate;

	private readonly IQueryPlanExplainabilityService
		_queryPlanExplainabilityService;

	/// <summary>
	/// 创建 QueryPlan 编排管线。
	/// </summary>
	public QueryPlanPipeline(
		IQueryPlanBuilder queryPlanBuilder,
		IQueryPlanContextBuilder queryPlanContextBuilder,
		QueryPlanMetadataValidator queryPlanMetadataValidator,
		IQueryPlanValidationPipeline validationPipeline,
		IQueryPlanConfidenceService queryPlanConfidenceService,
		IQueryPlanDecisionGate queryPlanDecisionGate,
		IQueryPlanExplainabilityService queryPlanExplainabilityService)
	{
		_queryPlanBuilder =
			queryPlanBuilder
			?? throw new ArgumentNullException(
				nameof(queryPlanBuilder));

		_queryPlanContextBuilder =
			queryPlanContextBuilder
			?? throw new ArgumentNullException(
				nameof(queryPlanContextBuilder));

		_queryPlanMetadataValidator =
			queryPlanMetadataValidator
			?? throw new ArgumentNullException(
				nameof(queryPlanMetadataValidator));

		_validationPipeline =
			validationPipeline
			?? throw new ArgumentNullException(
				nameof(validationPipeline));

		_queryPlanConfidenceService =
			queryPlanConfidenceService
			?? throw new ArgumentNullException(
				nameof(queryPlanConfidenceService));

		_queryPlanDecisionGate =
			queryPlanDecisionGate
			?? throw new ArgumentNullException(
				nameof(queryPlanDecisionGate));

		_queryPlanExplainabilityService =
			queryPlanExplainabilityService
			?? throw new ArgumentNullException(
				nameof(queryPlanExplainabilityService));
	}

	/// <inheritdoc />
	public async Task<QueryPlanPipelineResult> RunAsync(
		string question,
		QueryIntent intent)
	{
		/*
         * Step 2
         *
         * 构建 QueryPlan
         */
		var plan =
			await _queryPlanBuilder
				.BuildAsync(intent);


		/*
         * Step 3
         *
         * 构建 QueryPlan Validation Context
         */
		var validationContext =
			await _queryPlanContextBuilder
				.BuildAsync(plan);


		/*
         * Step 4
         *
         * Metadata关系完整性验证
         */
		try
		{
			_queryPlanMetadataValidator
				.Validate(
					plan,
					validationContext);
		}
		catch (Exception ex)
		{
			return new QueryPlanPipelineResult
			{
				EarlyResponse =
					new BIResponse
					{
						Success = false,

						Question = question,

						ErrorMessage =
							ex.Message
					}
			};
		}


		/*
         * Step 5
         *
         * QueryPlan语义验证+自动修复
         */
		var semanticValidation =
			await _validationPipeline
				.ValidateAsync(
					plan,
					validationContext,
					question);

		plan =
			semanticValidation.Plan;


		/*
         * Step 5.1
         *
         * Validation 最终失败。
         */
		if (!semanticValidation.ValidationResult.IsValid)
		{
			var validationExplanation =
				_queryPlanExplainabilityService
					.Explain(
						question,
						plan,
						semanticValidation.ValidationResult,
						semanticValidation.RepairTrace,
						null,
						null);

			return new QueryPlanPipelineResult
			{
				EarlyResponse =
					new BIResponse
					{
						Success = false,

						Question = question,

						ErrorMessage =
							string.Join(
								"\n",
								semanticValidation.ValidationResult.Errors
									.Select(x => x.Message)),

						Explanation =
							validationExplanation
					}
			};
		}


		/*
         * Step 5.2
         *
         * QueryPlan Confidence
         */
		var confidence =
			await _queryPlanConfidenceService
				.EvaluateAsync(
					plan,
					semanticValidation,
					semanticValidation.RepairTrace,
					question);


		/*
         * Step 5.3
         *
         * QueryPlan Decision Gate
         */
		var decision =
			_queryPlanDecisionGate
				.Evaluate(
					confidence);


		/*
         * Step 5.3.1
         *
         * QueryPlan Explainability。
         */
		var explanation =
			_queryPlanExplainabilityService
				.Explain(
					question,
					plan,
					semanticValidation.ValidationResult,
					semanticValidation.RepairTrace,
					confidence,
					decision);


		/*
         * Step 5.4
         *
         * Decision Gate 阻断。
         */
		if (!decision.ShouldExecute)
		{
			return new QueryPlanPipelineResult
			{
				EarlyResponse =
					new BIResponse
					{
						Success = false,

						Question = question,

						ErrorMessage =
							decision.Reason
							??
							"QueryPlan 未通过 Decision Gate，禁止进入 SQL Builder。",

						Explanation =
							explanation
					}
			};
		}


		return new QueryPlanPipelineResult
		{
			Plan = plan,

			SemanticValidation = semanticValidation,

			Confidence = confidence,

			Decision = decision,

			Explanation = explanation
		};
	}
}
