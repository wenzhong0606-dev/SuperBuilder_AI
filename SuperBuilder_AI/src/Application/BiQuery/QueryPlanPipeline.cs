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
	private readonly IDecisionAuditSink _auditSink;

	/// <summary>
	/// 创建 QueryPlan 编排管线。
	/// </summary>
	/// <param name="stages">
	/// 有序阶段集合（执行顺序即 DI 注册顺序）。
	/// 任一阶段设置 <see cref="QueryPlanPipelineContext.EarlyResponse"/> 即短路返回。
	/// </param>
	/// <param name="auditSink">
	/// 决策审计 Sink（M5-09）。默认注入 <see cref="NoOpDecisionAuditSink"/> 时零行为变更。
	/// </param>
	public QueryPlanPipeline(
		IEnumerable<IQueryPlanStage> stages,
		IDecisionAuditSink auditSink)
	{
		_stages = stages
			?? throw new ArgumentNullException(nameof(stages));
		_auditSink = auditSink
			?? throw new ArgumentNullException(nameof(auditSink));
	}

	/// <inheritdoc />
	public async Task<QueryPlanPipelineResult> RunAsync(
		string question,
		QueryIntent intent,
		long? requestedDataSourceId = null,
		IReadOnlyCollection<long>? authorizedDataSourceIds = null)
	{
		// 请求级关联标识：同一次用户请求跨多次 Pipeline 运行共享。
		var correlationId = Guid.NewGuid().ToString();
		QueryPlanPipelineContext? ctx = null;
		var outcome = AuditOutcome.EarlyResponse;
		string? errorMessage = null;

		try
		{
			ctx = new QueryPlanPipelineContext(
				question,
				intent,
				requestedDataSourceId,
				authorizedDataSourceIds);

			foreach (var stage in _stages)
			{
				try
				{
					await stage.ExecuteAsync(ctx);
				}
				catch (InvalidOperationException ex) when (IsQueryPlanValidationFailure(ex))
				{
					// 元数据未映射到用户问题（如相关业务尚未接入、字段不在语义层）。
					// 按产品原则：不应以 500 中断执行，而是优雅反馈，引导用户绑定数据源或指定其他 metadata，
					// 通过补全语义层实现「越用越聪明」。
					ctx.EarlyResponse = BuildMetadataNotMappedResponse(question, ex.Message);
				}

				if (ctx.EarlyResponse is not null)
				{
					outcome = AuditOutcome.EarlyResponse;
					errorMessage = ctx.EarlyResponse.ErrorMessage;
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
				outcome = MapDecisionOutcome(decision.Decision);
				errorMessage = decision.Reason;
				return new QueryPlanPipelineResult
				{
					EarlyResponse = new BIResponse
					{
						Success = false,

						Question = question,

						ErrorMessage = decision.Reason
							?? "QueryPlan 未通过 Decision Gate，禁止进入 SQL Builder。",

						Explanation = ctx.Explanation
					},

					// M9-05：纯观测——Decision Gate 以 Reject 阻断时置位。
					WasRejected = decision.IsReject
				};
			}

			outcome = AuditOutcome.Executed;
			return new QueryPlanPipelineResult
			{
				Plan = ctx.Plan!,

				SemanticValidation = ctx.SemanticValidation!,

				Confidence = ctx.Confidence!,

				Decision = ctx.Decision!,

				Explanation = ctx.Explanation!,

				// M9-05：纯观测——修复闭环实际执行过（TotalAttempts > 0）时置位。
				WasRepaired = ctx.SemanticValidation?.RepairTrace?.TotalAttempts > 0
			};
		}
		finally
		{
			// M5-09：无论成功或短路，均在返回前采集一次审计记录。
			// 审计为旁路，任何故障（Sink 抛异常）绝不影响主流程返回结果。
			if (ctx is not null)
			{
				try
				{
					await _auditSink.RecordAsync(
						DecisionAuditRecordBuilder.Build(
							ctx,
							outcome,
							correlationId,
							errorMessage),
						CancellationToken.None);
				}
				catch
				{
					// 审计旁路故障吞掉，保持主流程语义不变。
				}
			}
		}
	}

	/// <summary>
	/// 将 Decision Gate 决策态映射为审计结果分类。
	/// 仅阻断态（!ShouldExecute）会进入此映射；可执态一律记为 Executed。
	/// </summary>
	private static AuditOutcome MapDecisionOutcome(
		QueryPlanDecisionType decision)
	{
		return decision switch
		{
			QueryPlanDecisionType.Reject => AuditOutcome.Rejected,
			QueryPlanDecisionType.RequireApproval => AuditOutcome.RequiresApproval,
			QueryPlanDecisionType.AskClarification => AuditOutcome.AskClarification,
			_ => AuditOutcome.Executed
		};
	}

	/// <summary>
	/// 判定是否为 QueryPlan 验证失败抛出的异常。
	/// 该异常由 <see cref="QueryPlanBuilder.BuildAsync"/> 在最终验证不通过时抛出，
	/// 其消息前缀为 “QueryPlan验证失败”（见 <c>QueryPlanValidationResult.ToErrorMessage</c>）。
	/// </summary>
	private static bool IsQueryPlanValidationFailure(InvalidOperationException ex)
	{
		return ex.Message.StartsWith("QueryPlan验证失败", StringComparison.Ordinal);
	}

	/// <summary>
	/// 构造「元数据未映射」的优雅降级响应。
	/// 面向用户：告知未找到对应业务映射并给出可执行建议（绑定数据源 / 指定其他 metadata / 换用已接入术语），
	/// 而非以 500 中断执行；通过补全语义层即可逐步覆盖更多问题。
	/// </summary>
	private static BIResponse BuildMetadataNotMappedResponse(
		string question,
		string detail)
	{
		const string guidance =
			"未找到与您的问题对应的业务元数据映射，因此无法生成可执行的查询。" +
			"您可以：① 在对应数据源中绑定相关业务表（或修正字段语义）；② 换用已接入的业务术语重新提问。" +
			"随着元数据不断完善，我将能回答更多问题。";

		return new BIResponse
		{
			Success = false,

			Question = question,

			ErrorMessage = guidance + "\n\n" + detail
		};
	}
}
