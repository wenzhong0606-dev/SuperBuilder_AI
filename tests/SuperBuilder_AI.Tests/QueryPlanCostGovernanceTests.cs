using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SuperBuilder_AI.Application.Common.Options;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Services.BI;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M5-06 查询成本治理：分类器 / 策略 / 管线阶段短路验证。
/// 全部使用假对象（不依赖 LLM / 真实元数据 / 数据库）。
/// </summary>
public class QueryPlanCostGovernanceTests
{
	// =========================================================
	// IQueryCostClassifier（结构性信号抽取）
	// =========================================================

	[Fact]
	public async Task Classifier_UnboundedNoLimit_DetectsUnbounded()
	{
		var plan = new QueryPlan { Joins = new List<QueryJoin>() };
		var a = await new StructuralQueryCostClassifier()
			.AssessAsync(plan, new QueryPlanConfidence());

		Assert.True(a.IsUnbounded);
		Assert.Null(a.RequestedLimit);
		Assert.Equal(0, a.JoinCount);
		Assert.Equal(1, a.TableCount);
		Assert.Equal(ModelCostTier.Low, a.ModelCostTier);
	}

	[Fact]
	public async Task Classifier_WithPlanLimit_IsNotUnbounded()
	{
		var plan = new QueryPlan { Limit = 50 };
		var a = await new StructuralQueryCostClassifier()
			.AssessAsync(plan, new QueryPlanConfidence());

		Assert.False(a.IsUnbounded);
		Assert.Equal(50, a.RequestedLimit);
	}

	[Fact]
	public async Task Classifier_WithJoins_CountsTablesAndJoins()
	{
		var plan = new QueryPlan
		{
			Joins = new List<QueryJoin> { new(), new(), new(), new() }
		};
		var a = await new StructuralQueryCostClassifier()
			.AssessAsync(plan, new QueryPlanConfidence());

		Assert.Equal(4, a.JoinCount);
		Assert.Equal(5, a.TableCount);
	}

	// =========================================================
	// ICostGovernancePolicy（阈值裁决）
	// =========================================================

	[Fact]
	public void Policy_DefaultAllOff_NoAction()
	{
		var policy = new ThresholdCostGovernancePolicy(Options.Create(new CostGovernanceOptions()));
		var a = new QueryCostAssessment { IsUnbounded = true, JoinCount = 9 };

		var v = policy.Evaluate(a, null, new CostGovernanceContext(0, false));

		Assert.Equal(CostGovernanceAction.NoAction, v.Action);
	}

	[Fact]
	public void Policy_UnboundedGuardOn_DegradeWithAppliedLimit()
	{
		var policy = new ThresholdCostGovernancePolicy(Options.Create(
			new CostGovernanceOptions { EnableUnboundedGuard = true, UnboundedDefaultLimit = 100 }));
		var a = new QueryCostAssessment { IsUnbounded = true };

		var v = policy.Evaluate(a, null, new CostGovernanceContext(0, false));

		Assert.Equal(CostGovernanceAction.Degrade, v.Action);
		Assert.Equal(100, v.AppliedLimit);
	}

	[Fact]
	public void Policy_JoinWarn_DegradeWithoutLimit()
	{
		var policy = new ThresholdCostGovernancePolicy(Options.Create(
			new CostGovernanceOptions { EnableJoinGuard = true, WarnJoins = 3 }));
		var a = new QueryCostAssessment { JoinCount = 4 };

		var v = policy.Evaluate(a, null, new CostGovernanceContext(0, false));

		Assert.Equal(CostGovernanceAction.Degrade, v.Action);
		Assert.Null(v.AppliedLimit);
	}

	[Fact]
	public void Policy_JoinMax_Rejects()
	{
		var policy = new ThresholdCostGovernancePolicy(Options.Create(
			new CostGovernanceOptions { EnableJoinGuard = true, MaxJoins = 6 }));
		var a = new QueryCostAssessment { JoinCount = 7 };

		var v = policy.Evaluate(a, null, new CostGovernanceContext(0, false));

		Assert.Equal(CostGovernanceAction.Reject, v.Action);
	}

	[Fact]
	public void Policy_Bypass_IgnoresThresholds()
	{
		var policy = new ThresholdCostGovernancePolicy(Options.Create(
			new CostGovernanceOptions { EnableJoinGuard = true, MaxJoins = 1 }));
		var a = new QueryCostAssessment { JoinCount = 20 };

		var v = policy.Evaluate(a, null, new CostGovernanceContext(0, true));

		Assert.Equal(CostGovernanceAction.NoAction, v.Action);
	}

	[Fact]
	public void Policy_ModelCostGuardOn_HighTier_Rejects()
	{
		var policy = new ThresholdCostGovernancePolicy(Options.Create(
			new CostGovernanceOptions
			{
				EnableModelCostGuard = true,
				RejectModelCostTier = ModelCostTier.High
			}));
		var a = new QueryCostAssessment { ModelCostTier = ModelCostTier.High };

		var v = policy.Evaluate(a, null, new CostGovernanceContext(0, false));

		Assert.Equal(CostGovernanceAction.Reject, v.Action);
	}

	[Fact]
	public void Policy_ModelCostGuardDisabled_HighTier_NoAction()
	{
		var policy = new ThresholdCostGovernancePolicy(Options.Create(
			new CostGovernanceOptions
			{
				EnableModelCostGuard = false,
				RejectModelCostTier = ModelCostTier.High
			}));
		var a = new QueryCostAssessment { ModelCostTier = ModelCostTier.High };

		var v = policy.Evaluate(a, null, new CostGovernanceContext(0, false));

		Assert.Equal(CostGovernanceAction.NoAction, v.Action);
	}

	// =========================================================
	// QueryPlanCostGovernanceStage（管线集成）
	// =========================================================

	private static QueryPlanPipelineContext BuildContext(
		QueryPlan plan,
		QueryPlanDecision? decision = null) =>
		new QueryPlanPipelineContext("q", new QueryIntent(), null, null)
		{
			Plan = plan,
			Confidence = new QueryPlanConfidence(),
			Decision = decision ?? new QueryPlanDecision
			{
				Decision = QueryPlanDecisionType.Allow,
				Confidence = new QueryPlanConfidence()
			}
		};

	private static QueryPlanCostGovernanceStage BuildStage(CostGovernanceOptions opts) =>
		new QueryPlanCostGovernanceStage(
			new StructuralQueryCostClassifier(),
			new ThresholdCostGovernancePolicy(Options.Create(opts)),
			new FakeResolver(new CostGovernanceContext(0, false)));

	[Fact]
	public async Task Stage_DefaultAllOff_KeepsDecisionAndPlan()
	{
		var ctx = BuildContext(new QueryPlan());
		await BuildStage(new CostGovernanceOptions()).ExecuteAsync(ctx);

		Assert.Null(ctx.EarlyResponse);
		Assert.True(ctx.Decision!.IsAllow);
		Assert.Null(ctx.Plan!.Limit);
	}

	[Fact]
	public async Task Stage_UnboundedEnabled_InjectsLimitAndLimitedExecution()
	{
		var ctx = BuildContext(new QueryPlan());
		await BuildStage(new CostGovernanceOptions
		{
			EnableUnboundedGuard = true,
			UnboundedDefaultLimit = 200
		}).ExecuteAsync(ctx);

		Assert.Null(ctx.EarlyResponse);
		Assert.True(ctx.Decision!.IsLimitedExecution);
		Assert.Equal(200, ctx.Plan!.Limit);
	}

	[Fact]
	public async Task Stage_BoundedQuery_UnboundedGuardNoOp()
	{
		// 计划已设 Limit=50 → IsUnbounded=false，无界守卫不触发（治理只针对无界查询）。
		var ctx = BuildContext(new QueryPlan { Limit = 50 });
		await BuildStage(new CostGovernanceOptions
		{
			EnableUnboundedGuard = true,
			UnboundedDefaultLimit = 200
		}).ExecuteAsync(ctx);

		Assert.Null(ctx.EarlyResponse);
		Assert.True(ctx.Decision!.IsAllow);
		Assert.Equal(50, ctx.Plan!.Limit);
	}

	[Fact]
	public async Task Stage_PolicyAppliedLimit_CappedToExistingViaMin()
	{
		// 自定义策略对「有界计划」返回降级上限 200，阶段应取 min(现有 50, 200) = 50。
		var ctx = BuildContext(new QueryPlan { Limit = 50 });
		var stage = new QueryPlanCostGovernanceStage(
			new StructuralQueryCostClassifier(),
			new FakeDegradePolicy(200),
			new FakeResolver(new CostGovernanceContext(0, false)));

		await stage.ExecuteAsync(ctx);

		Assert.True(ctx.Decision!.IsLimitedExecution);
		Assert.Equal(50, ctx.Plan!.Limit);
	}

	[Fact]
	public async Task Stage_JoinMax_RejectsWithEarlyResponse()
	{
		var ctx = BuildContext(new QueryPlan
		{
			Joins = new List<QueryJoin> { new(), new(), new() }
		});
		await BuildStage(new CostGovernanceOptions
		{
			EnableJoinGuard = true,
			MaxJoins = 2
		}).ExecuteAsync(ctx);

		Assert.NotNull(ctx.EarlyResponse);
		Assert.False(ctx.EarlyResponse!.Success);
		Assert.True(ctx.Decision!.IsReject);
	}

	private sealed class FakeResolver : ICostGovernanceContextResolver
	{
		private readonly CostGovernanceContext _ctx;
		public FakeResolver(CostGovernanceContext ctx) => _ctx = ctx;
		public Task<CostGovernanceContext> ResolveAsync(
			QueryPlanPipelineContext ctx,
			CancellationToken ct = default) => Task.FromResult(_ctx);
	}

	private sealed class FakeDegradePolicy : ICostGovernancePolicy
	{
		private readonly int? _appliedLimit;
		public FakeDegradePolicy(int? appliedLimit) => _appliedLimit = appliedLimit;
		public CostGovernanceVerdict Evaluate(
			QueryCostAssessment assessment,
			QueryPlanDecision? currentDecision,
			CostGovernanceContext context) =>
			new() { Action = CostGovernanceAction.Degrade, AppliedLimit = _appliedLimit,
				Reason = "capped" };
	}
}
