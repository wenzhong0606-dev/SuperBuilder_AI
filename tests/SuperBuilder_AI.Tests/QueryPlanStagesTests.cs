using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Services.BI;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M5-03：QueryPlanPipeline 阶段化测试。
///
/// 覆盖两条主线：
/// 1) 真实 <see cref="QueryPlanPipeline"/> 集成测试 —— 用假对象注入全部 7 个协作依赖，
///    验证 8 个阶段的有序编排 + 三处提前返回（Step 4 元数据异常 / Step 5.1 语义无效 /
///    Step 5.4 Decision Gate 阻断）与原 RunAsync 行为逐字节一致；
/// 2) 各 <see cref="IQueryPlanStage"/> 隔离单测。
///
/// 全部使用假对象，不触发 LLM / 真实元数据校验，属纯单元/集成测试。
/// </summary>
public class QueryPlanStagesTests
{
	#region 假对象

	private sealed class FakeBuilder : IQueryPlanBuilder
	{
		public QueryPlan PlanToReturn { get; set; } = new();
		public Task<QueryPlan> BuildAsync(QueryIntent intent, long? requestedDataSourceId = null, IReadOnlyCollection<long>? authorizedDataSourceIds = null)
			=> Task.FromResult(PlanToReturn);
		public Task<QueryPlan> BuildAsync(QueryIntent intent, QueryPlanSemanticResolution? resolution)
			=> Task.FromResult(PlanToReturn);
	}

	private sealed class FakeContextBuilder : IQueryPlanContextBuilder
	{
		public QueryPlanValidationContext CtxToReturn { get; set; } = new();
		public Task<QueryPlanValidationContext> BuildAsync(QueryPlan plan)
			=> Task.FromResult(CtxToReturn);
	}

	private sealed class NoOpValidator : QueryPlanMetadataValidator
	{
		public override void Validate(QueryPlan plan, QueryPlanValidationContext context) { }
	}

	private sealed class ThrowingValidator : QueryPlanMetadataValidator
	{
		public override void Validate(QueryPlan plan, QueryPlanValidationContext context)
			=> throw new System.InvalidOperationException("metadata boom");
	}

	private sealed class FakeValidationPipeline : IQueryPlanValidationPipeline
	{
		public QueryPlanValidationPipelineResult Result { get; set; } = new()
		{
			Plan = new QueryPlan(),
			ValidationResult = new QuerySemanticValidationResult(),
			RepairTrace = new QueryPlanRepairTrace()
		};
		public Task<QueryPlanValidationPipelineResult> ValidateAsync(QueryPlan plan, QueryPlanValidationContext context, string question)
			=> Task.FromResult(Result);
	}

	private sealed class FakeConfidence : IQueryPlanConfidenceService
	{
		public QueryPlanConfidence Confidence { get; set; } = new();
		public Task<QueryPlanConfidence> EvaluateAsync(QueryPlan plan, QueryPlanValidationPipelineResult validationResult, QueryPlanRepairTrace? repairTrace, string question, CancellationToken cancellationToken = default)
			=> Task.FromResult(Confidence);
	}

	private sealed class FakeDecisionGate : IQueryPlanDecisionGate
	{
		public QueryPlanDecision Decision { get; set; } = new() { Decision = QueryPlanDecisionType.Allow };
		public QueryPlanDecision Evaluate(QueryPlanConfidence confidence) => Decision;
	}

	private sealed class FakeExplain : IQueryPlanExplainabilityService
	{
		public QueryPlanExplanation Explanation { get; set; } = new();
		public QueryPlanExplanation Explain(string? question, QueryPlan? plan, QuerySemanticValidationResult? validationResult, QueryPlanRepairTrace? repairTrace, QueryPlanConfidence? confidence, QueryPlanDecision? decision)
			=> Explanation;
	}

	private static IQueryPlanPipeline BuildPipeline(
		IQueryPlanBuilder builder,
		IQueryPlanContextBuilder contextBuilder,
		QueryPlanMetadataValidator metadataValidator,
		IQueryPlanValidationPipeline validationPipeline,
		IQueryPlanConfidenceService confidence,
		IQueryPlanDecisionGate decisionGate,
		IQueryPlanExplainabilityService explain)
	{
		var stages = new IQueryPlanStage[]
		{
			new QueryPlanBuildStage(builder),
			new QueryPlanContextStage(contextBuilder),
			new QueryPlanMetadataIntegrityStage(metadataValidator),
			new QueryPlanDetailProjectionStage(),
			new QueryPlanSemanticValidationStage(validationPipeline, explain),
			new QueryPlanConfidenceStage(confidence),
			new QueryPlanDecisionGateStage(decisionGate),
			new QueryPlanExplainabilityStage(explain)
		};
		return new QueryPlanPipeline(
			stages,
			new NoOpDecisionAuditSink());
	}

	#endregion

	#region 真实管线集成测试

	[Fact]
	public async Task RunAsync_SuccessFlow_ReturnsPlanConfidenceDecisionExplanation()
	{
		var pipeline = BuildPipeline(
			new FakeBuilder(),
			new FakeContextBuilder(),
			new NoOpValidator(),
			new FakeValidationPipeline(),
			new FakeConfidence(),
			new FakeDecisionGate(),
			new FakeExplain());

		var result = await pipeline.RunAsync("q", new QueryIntent());

		Assert.Null(result.EarlyResponse);
		Assert.NotNull(result.Plan);
		Assert.NotNull(result.SemanticValidation);
		Assert.NotNull(result.Confidence);
		Assert.NotNull(result.Decision);
		Assert.NotNull(result.Explanation);
	}

	[Fact]
	public async Task RunAsync_MetadataIntegrityThrows_ReturnsEarlyResponseWithoutExplanation()
	{
		var pipeline = BuildPipeline(
			new FakeBuilder(),
			new FakeContextBuilder(),
			new ThrowingValidator(),
			new FakeValidationPipeline(),
			new FakeConfidence(),
			new FakeDecisionGate(),
			new FakeExplain());

		var result = await pipeline.RunAsync("q", new QueryIntent());

		Assert.NotNull(result.EarlyResponse);
		Assert.False(result.EarlyResponse!.Success);
		Assert.Equal("metadata boom", result.EarlyResponse.ErrorMessage);
		// Step 4 提前返回：不生成 Explanation（与重构前一致）。
		Assert.Null(result.EarlyResponse.Explanation);
	}

	[Fact]
	public async Task RunAsync_SemanticValidationInvalid_ReturnsEarlyResponseWithExplanation()
	{
		var validation = new FakeValidationPipeline
		{
			Result = new QueryPlanValidationPipelineResult
			{
				Plan = new QueryPlan(),
				ValidationResult = new QuerySemanticValidationResult
				{
					Errors = new List<SemanticValidationError>
					{
						new SemanticValidationError { Message = "bad column", Severity = SemanticValidationSeverity.Error }
					}
				},
				RepairTrace = new QueryPlanRepairTrace()
			}
		};

		var pipeline = BuildPipeline(
			new FakeBuilder(),
			new FakeContextBuilder(),
			new NoOpValidator(),
			validation,
			new FakeConfidence(),
			new FakeDecisionGate(),
			new FakeExplain());

		var result = await pipeline.RunAsync("q", new QueryIntent());

		Assert.NotNull(result.EarlyResponse);
		Assert.False(result.EarlyResponse!.Success);
		Assert.Contains("bad column", result.EarlyResponse.ErrorMessage);
		Assert.NotNull(result.EarlyResponse.Explanation);
	}

	[Fact]
	public async Task RunAsync_DecisionGateBlocks_ReturnsEarlyResponseWithExplanation()
	{
		var decision = new FakeDecisionGate
		{
			Decision = new QueryPlanDecision { Decision = QueryPlanDecisionType.Reject, Reason = "blocked by gate" }
		};

		var pipeline = BuildPipeline(
			new FakeBuilder(),
			new FakeContextBuilder(),
			new NoOpValidator(),
			new FakeValidationPipeline(),
			new FakeConfidence(),
			decision,
			new FakeExplain());

		var result = await pipeline.RunAsync("q", new QueryIntent());

		Assert.NotNull(result.EarlyResponse);
		Assert.False(result.EarlyResponse!.Success);
		Assert.Equal("blocked by gate", result.EarlyResponse.ErrorMessage);
		// Step 5.3.1 已生成 Explanation，Step 5.4 阻断时一并返回。
		Assert.NotNull(result.EarlyResponse.Explanation);
	}

	// M9-05：验证 WasRejected / WasRepaired 纯观测标志位的线程逻辑。

	[Fact]
	public async Task RunAsync_DecisionGateReject_SetsWasRejectedTrue()
	{
		var decision = new FakeDecisionGate
		{
			Decision = new QueryPlanDecision { Decision = QueryPlanDecisionType.Reject, Reason = "rejected" }
		};

		var pipeline = BuildPipeline(
			new FakeBuilder(),
			new FakeContextBuilder(),
			new NoOpValidator(),
			new FakeValidationPipeline(),
			new FakeConfidence(),
			decision,
			new FakeExplain());

		var result = await pipeline.RunAsync("q", new QueryIntent());

		Assert.True(result.WasRejected);
		Assert.False(result.WasRepaired);
	}

	[Fact]
	public async Task RunAsync_NonRejectDecision_DoesNotSetWasRejected()
	{
		var decision = new FakeDecisionGate
		{
			Decision = new QueryPlanDecision { Decision = QueryPlanDecisionType.Allow }
		};

		var pipeline = BuildPipeline(
			new FakeBuilder(),
			new FakeContextBuilder(),
			new NoOpValidator(),
			new FakeValidationPipeline(),
			new FakeConfidence(),
			decision,
			new FakeExplain());

		var result = await pipeline.RunAsync("q", new QueryIntent());

		Assert.False(result.WasRejected);
	}

	[Fact]
	public async Task RunAsync_RepairTraceHasAttempts_SetsWasRepairedTrue()
	{
		var validation = new FakeValidationPipeline
		{
			Result = new QueryPlanValidationPipelineResult
			{
				Plan = new QueryPlan(),
				ValidationResult = new QuerySemanticValidationResult(),
				RepairTrace = new QueryPlanRepairTrace { TotalAttempts = 2 }
			}
		};

		var pipeline = BuildPipeline(
			new FakeBuilder(),
			new FakeContextBuilder(),
			new NoOpValidator(),
			validation,
			new FakeConfidence(),
			new FakeDecisionGate(),
			new FakeExplain());

		var result = await pipeline.RunAsync("q", new QueryIntent());

		Assert.True(result.WasRepaired);
		Assert.False(result.WasRejected);
	}

	[Fact]
	public async Task RunAsync_NoRepairAttempts_DoesNotSetWasRepaired()
	{
		var validation = new FakeValidationPipeline
		{
			Result = new QueryPlanValidationPipelineResult
			{
				Plan = new QueryPlan(),
				ValidationResult = new QuerySemanticValidationResult(),
				RepairTrace = new QueryPlanRepairTrace { TotalAttempts = 0 }
			}
		};

		var pipeline = BuildPipeline(
			new FakeBuilder(),
			new FakeContextBuilder(),
			new NoOpValidator(),
			validation,
			new FakeConfidence(),
			new FakeDecisionGate(),
			new FakeExplain());

		var result = await pipeline.RunAsync("q", new QueryIntent());

		Assert.False(result.WasRepaired);
	}

	#endregion

	#region 阶段隔离单测

	[Fact]
	public async Task BuildStage_SetsPlanFromBuilder()
	{
		var plan = new QueryPlan();
		var stage = new QueryPlanBuildStage(new FakeBuilder { PlanToReturn = plan });
		var ctx = new QueryPlanPipelineContext("q", new QueryIntent(), null, null);

		await stage.ExecuteAsync(ctx);

		Assert.Same(plan, ctx.Plan);
		Assert.Null(ctx.EarlyResponse);
	}

	[Fact]
	public async Task ContextStage_SetsValidationContext()
	{
		var vc = new QueryPlanValidationContext();
		var stage = new QueryPlanContextStage(new FakeContextBuilder { CtxToReturn = vc });
		var ctx = new QueryPlanPipelineContext("q", new QueryIntent(), null, null)
		{
			Plan = new QueryPlan()
		};

		await stage.ExecuteAsync(ctx);

		Assert.Same(vc, ctx.ValidationContext);
		Assert.Null(ctx.EarlyResponse);
	}

	[Fact]
	public async Task MetadataIntegrityStage_NoOpValidator_NoEarlyResponse()
	{
		var stage = new QueryPlanMetadataIntegrityStage(new NoOpValidator());
		var ctx = new QueryPlanPipelineContext("q", new QueryIntent(), null, null)
		{
			Plan = new QueryPlan(),
			ValidationContext = new QueryPlanValidationContext()
		};

		await stage.ExecuteAsync(ctx);

		Assert.Null(ctx.EarlyResponse);
	}

	[Fact]
	public async Task MetadataIntegrityStage_ThrowingValidator_SetsEarlyResponse()
	{
		var stage = new QueryPlanMetadataIntegrityStage(new ThrowingValidator());
		var ctx = new QueryPlanPipelineContext("q", new QueryIntent(), null, null)
		{
			Plan = new QueryPlan(),
			ValidationContext = new QueryPlanValidationContext()
		};

		await stage.ExecuteAsync(ctx);

		Assert.NotNull(ctx.EarlyResponse);
		Assert.Equal("metadata boom", ctx.EarlyResponse!.ErrorMessage);
	}

	[Fact]
	public async Task DetailProjectionStage_EmptyPlan_NoEarlyResponse()
	{
		var stage = new QueryPlanDetailProjectionStage();
		var ctx = new QueryPlanPipelineContext("q", new QueryIntent(), null, null)
		{
			Plan = new QueryPlan(),
			ValidationContext = new QueryPlanValidationContext()
		};

		await stage.ExecuteAsync(ctx);

		Assert.Null(ctx.EarlyResponse);
	}

	[Fact]
	public async Task SemanticValidationStage_Valid_SetsPlanAndSemanticValidation()
	{
		var sv = new FakeValidationPipeline();
		var stage = new QueryPlanSemanticValidationStage(sv, new FakeExplain());
		var ctx = new QueryPlanPipelineContext("q", new QueryIntent(), null, null)
		{
			Plan = new QueryPlan(),
			ValidationContext = new QueryPlanValidationContext()
		};

		await stage.ExecuteAsync(ctx);

		Assert.Null(ctx.EarlyResponse);
		Assert.Same(sv.Result, ctx.SemanticValidation);
		Assert.Same(sv.Result.Plan, ctx.Plan);
	}

	[Fact]
	public async Task SemanticValidationStage_Invalid_SetsEarlyResponse()
	{
		var invalid = new FakeValidationPipeline
		{
			Result = new QueryPlanValidationPipelineResult
			{
				Plan = new QueryPlan(),
				ValidationResult = new QuerySemanticValidationResult
				{
					Errors = new List<SemanticValidationError>
					{
						new SemanticValidationError { Message = "x", Severity = SemanticValidationSeverity.Error }
					}
				},
				RepairTrace = new QueryPlanRepairTrace()
			}
		};
		var stage = new QueryPlanSemanticValidationStage(invalid, new FakeExplain());
		var ctx = new QueryPlanPipelineContext("q", new QueryIntent(), null, null)
		{
			Plan = new QueryPlan(),
			ValidationContext = new QueryPlanValidationContext()
		};

		await stage.ExecuteAsync(ctx);

		Assert.NotNull(ctx.EarlyResponse);
		Assert.Contains("x", ctx.EarlyResponse!.ErrorMessage);
		Assert.NotNull(ctx.EarlyResponse.Explanation);
	}

	[Fact]
	public async Task ConfidenceStage_SetsConfidence()
	{
		var conf = new FakeConfidence();
		var stage = new QueryPlanConfidenceStage(conf);
		var ctx = new QueryPlanPipelineContext("q", new QueryIntent(), null, null)
		{
			Plan = new QueryPlan(),
			SemanticValidation = new QueryPlanValidationPipelineResult()
		};

		await stage.ExecuteAsync(ctx);

		Assert.Same(conf.Confidence, ctx.Confidence);
		Assert.Null(ctx.EarlyResponse);
	}

	[Fact]
	public async Task DecisionGateStage_SetsDecision()
	{
		var gate = new FakeDecisionGate();
		var stage = new QueryPlanDecisionGateStage(gate);
		var ctx = new QueryPlanPipelineContext("q", new QueryIntent(), null, null)
		{
			Confidence = new QueryPlanConfidence()
		};

		await stage.ExecuteAsync(ctx);

		Assert.Same(gate.Decision, ctx.Decision);
		Assert.Null(ctx.EarlyResponse);
	}

	[Fact]
	public async Task ExplainabilityStage_SetsExplanation()
	{
		var explain = new FakeExplain();
		var stage = new QueryPlanExplainabilityStage(explain);
		var ctx = new QueryPlanPipelineContext("q", new QueryIntent(), null, null)
		{
			Plan = new QueryPlan(),
			SemanticValidation = new QueryPlanValidationPipelineResult(),
			Confidence = new QueryPlanConfidence(),
			Decision = new QueryPlanDecision()
		};

		await stage.ExecuteAsync(ctx);

		Assert.Same(explain.Explanation, ctx.Explanation);
		Assert.Null(ctx.EarlyResponse);
	}

	#endregion
}
