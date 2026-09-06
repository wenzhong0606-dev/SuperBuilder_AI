using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// QueryPlan 编排管线（M5-03 阶段化）。
///
/// 从 BIConversationService 抽取而来，把原先硬编码在 RunAsync 的 8 段链路
/// 抽离为有序的 <see cref="IQueryPlanStage"/> 集合：
///
/// Build → Context → MetadataIntegrity → DetailProjection →
/// SemanticValidation → Confidence → DecisionGate → Explainability
///
/// RunAsync 仅负责：构造上下文、按序执行阶段、每阶段后检查 EarlyResponse 短路、
/// 并最终处理 Decision Gate 阻断（Step 5.4）或返回成功结果。
///
/// 行为与重构前完全一致：步骤顺序、提前返回条件、解释文本生成点均不变。
/// </summary>
public sealed class QueryPlanPipeline : IQueryPlanPipeline
{
	private readonly IEnumerable<IQueryPlanStage> _stages;

	/// <summary>
	/// 创建 QueryPlan 编排管线。
	/// </summary>
	/// <param name="stages">
	/// 有序阶段集合（执行顺序即 DI 注册顺序）。
	/// 任一阶段设置 <see cref="QueryPlanPipelineContext.EarlyResponse"/> 即短路返回。
	/// </param>
	public QueryPlanPipeline(IEnumerable<IQueryPlanStage> stages)
	{
		_stages = stages
			?? throw new ArgumentNullException(nameof(stages));
	}

	/// <inheritdoc />
	public async Task<QueryPlanPipelineResult> RunAsync(
		string question,
		QueryIntent intent,
		long? requestedDataSourceId = null,
		IReadOnlyCollection<long>? authorizedDataSourceIds = null)
	{
		var ctx = new QueryPlanPipelineContext(
			question,
			intent,
			requestedDataSourceId,
			authorizedDataSourceIds);

		foreach (var stage in _stages)
		{
			await stage.ExecuteAsync(ctx);

			if (ctx.EarlyResponse is not null)
			{
				return new QueryPlanPipelineResult
				{
					EarlyResponse = ctx.EarlyResponse
				};
			}
		}

		/*
         * Step 5.4
         *
         * Decision Gate 阻断。Explainability 已在 Explainability 阶段产出，
         * 此处仅据此构造提前返回（与原 Step 5.3.1 + 5.4 等价）。
         */
		if (ctx.Decision is { } decision && !decision.ShouldExecute)
		{
			return new QueryPlanPipelineResult
			{
				EarlyResponse = new BIResponse
				{
					Success = false,

					Question = question,

					ErrorMessage = decision.Reason
						?? "QueryPlan 未通过 Decision Gate，禁止进入 SQL Builder。",

					Explanation = ctx.Explanation
				}
			};
		}

		return new QueryPlanPipelineResult
		{
			Plan = ctx.Plan!,

			SemanticValidation = ctx.SemanticValidation!,

			Confidence = ctx.Confidence!,

			Decision = ctx.Decision!,

			Explanation = ctx.Explanation!
		};
	}
}
