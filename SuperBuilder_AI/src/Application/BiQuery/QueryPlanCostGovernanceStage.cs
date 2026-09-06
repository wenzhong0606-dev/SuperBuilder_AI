using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SuperBuilder_AI.Application.Common.Options;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 阶段（DecisionGate 之后、Explainability 之前）：查询成本治理（M5-06）。
///
/// 委托 <see cref="IQueryCostClassifier"/> 评估成本信号、<see cref="ICostGovernancePolicy"/>
/// 产出裁决：拒绝 → 改写 Decision 为 Reject 并置 EarlyResponse（403）短路；
/// 降级 → 注入防御性 Limit 且改写 Decision 为 LimitedExecution（沿用 ShouldExecute 通路）。
/// 默认守卫均关闭时本阶段为 no-op（零默认行为变更）。
/// </summary>
public sealed class QueryPlanCostGovernanceStage : IQueryPlanStage
{
	private readonly IQueryCostClassifier _classifier;
	private readonly ICostGovernancePolicy _policy;
	private readonly ICostGovernanceContextResolver _resolver;
	private readonly IModelCostTelemetry _telemetry;
	private readonly CostGovernanceOptions _options;

	/// <summary>创建成本治理阶段。</summary>
	public QueryPlanCostGovernanceStage(
		IQueryCostClassifier classifier,
		ICostGovernancePolicy policy,
		ICostGovernanceContextResolver resolver,
		IModelCostTelemetry telemetry,
		IOptions<CostGovernanceOptions> options)
	{
		_classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
		_policy = policy ?? throw new ArgumentNullException(nameof(policy));
		_resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
		_telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
		_options = options?.Value ?? new CostGovernanceOptions();
	}

	/// <inheritdoc />
	public string StageName => "CostGovernance";

	/// <inheritdoc />
	public async Task ExecuteAsync(
		QueryPlanPipelineContext ctx,
		CancellationToken ct = default)
	{
		if (ctx.Plan is null)
			return;

		var context = await _resolver.ResolveAsync(ctx, ct);
		if (context.Bypass)
			return;

		var assessment = await _classifier.AssessAsync(ctx.Plan, ctx.Confidence, ct);
		var verdict = _policy.Evaluate(assessment, ctx.Decision, context);

		// M5-08：非放行裁决写入成本遥测（NoOp / Log 由 TelemetryMode 决定；默认零行为）。
		if (verdict.Action != CostGovernanceAction.NoAction)
			await _telemetry.RecordAsync(assessment, verdict, context, ct);

		if (verdict.Action == CostGovernanceAction.NoAction)
			return;

		if (verdict.Action == CostGovernanceAction.Reject)
		{
			ctx.Decision = new QueryPlanDecision
			{
				Decision = QueryPlanDecisionType.Reject,
				Confidence = ctx.Confidence ?? new QueryPlanConfidence(),
				Reason = verdict.Reason ?? "查询成本超过治理阈值，已在执行前拒绝。"
			};
			ctx.EarlyResponse = new BIResponse
			{
				Success = false,
				Question = ctx.Question,
				ErrorMessage = verdict.Reason,
				Explanation = ctx.Explanation
			};
			return;
		}

		// 降级：注入防御性行数上限（若已设置则取较小值），并标记为受限执行。
		if (verdict.AppliedLimit is { } cap)
		{
			var current = ctx.Plan.Limit;
			ctx.Plan.Limit = current.HasValue ? Math.Min(current.Value, cap) : cap;
		}

		ctx.Decision = new QueryPlanDecision
		{
			Decision = QueryPlanDecisionType.LimitedExecution,
			Confidence = ctx.Confidence ?? new QueryPlanConfidence(),
			Reason = verdict.Reason ?? "查询成本较高，已降级为受限执行。"
		};
	}
}
