using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperBuilder_AI.Application.Common.Options;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Services.BI;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M5-08 Governance Policy Enablement：将 M5-05/06 的安全默认提升为可配置启用的真实治理。
/// 全部使用假对象（不依赖 LLM / 真实元数据 / 数据库）。
/// </summary>
public class QueryPlanGovernanceEnablementTests
{
	// =========================================================
	// 列级安全：真实分类器（DenyNothing ↔ PolicyDriven 切换）
	// =========================================================

	[Fact]
	public async Task ConfigurableClassifier_DefaultDenyNothing_NoRestriction()
	{
		var cls = new ConfigurableColumnClassifier(
			Options.Create(new ColumnSecurityOptions()),
			new DenyNothingColumnClassifier(),
			new PolicyDrivenColumnClassifier(Options.Create(new ColumnSecurityOptions())));
		var ctx = new ColumnSecurityContext(0, 1);

		Assert.False(await cls.IsRestrictedAsync(1, "Phone", null, ctx));
		Assert.False(await cls.IsRestrictedAsync(2, "Salary", null, ctx));
	}

	[Fact]
	public async Task ConfigurableClassifier_PolicyDriven_BuiltInPii_Restricts()
	{
		var opts = Options.Create(new ColumnSecurityOptions { ClassifierMode = ColumnClassifierMode.PolicyDriven });
		var cls = new ConfigurableColumnClassifier(opts, new DenyNothingColumnClassifier(), new PolicyDrivenColumnClassifier(opts));
		var ctx = new ColumnSecurityContext(0, 1);

		Assert.True(await cls.IsRestrictedAsync(1, "Phone", null, ctx));
		Assert.True(await cls.IsRestrictedAsync(2, "email", null, ctx)); // 大小写不敏感
		Assert.False(await cls.IsRestrictedAsync(3, "Amount", null, ctx));
	}

	[Fact]
	public async Task ConfigurableClassifier_PolicyDriven_ConfiguredNames_Restricts()
	{
		var opts = Options.Create(new ColumnSecurityOptions
		{
			ClassifierMode = ColumnClassifierMode.PolicyDriven,
			IncludeBuiltInPiiHeuristics = false,
			RestrictedColumnNames = new() { "Salary", "BankAccount" }
		});
		var cls = new ConfigurableColumnClassifier(opts, new DenyNothingColumnClassifier(), new PolicyDrivenColumnClassifier(opts));
		var ctx = new ColumnSecurityContext(0, 1);

		Assert.True(await cls.IsRestrictedAsync(1, "Salary", null, ctx));
		Assert.True(await cls.IsRestrictedAsync(2, "bankaccount", null, ctx));
		Assert.False(await cls.IsRestrictedAsync(3, "Amount", null, ctx));
		Assert.False(await cls.IsRestrictedAsync(4, "Phone", null, ctx)); // 内置 PII 已关闭
	}

	[Fact]
	public async Task ConfigurableClassifier_TenantOverride_EnablesPerTenant()
	{
		var opts = Options.Create(new ColumnSecurityOptions
		{
			ClassifierMode = ColumnClassifierMode.DenyNothing, // 全局关闭
			TenantOverrides = new()
			{
				[1001] = new TenantColumnSecurityOverride
				{
					Mode = ColumnClassifierMode.PolicyDriven,
					RestrictedColumnNames = new() { "TenantSecret" }
				}
			}
		});
		var cls = new ConfigurableColumnClassifier(opts, new DenyNothingColumnClassifier(), new PolicyDrivenColumnClassifier(opts));
		var tenantCtx = new ColumnSecurityContext(1001, 1);
		var otherCtx = new ColumnSecurityContext(999, 1);

		Assert.True(await cls.IsRestrictedAsync(1, "TenantSecret", null, tenantCtx));
		Assert.False(await cls.IsRestrictedAsync(2, "TenantSecret", null, otherCtx)); // 无覆盖 → 全局 DenyNothing
		Assert.True(await cls.IsRestrictedAsync(3, "Phone", null, tenantCtx)); // 内置 PII 仍适用
	}

	[Fact]
	public async Task PolicyDrivenClassifier_EmptyName_NotRestricted()
	{
		var opts = Options.Create(new ColumnSecurityOptions { ClassifierMode = ColumnClassifierMode.PolicyDriven });
		var cls = new PolicyDrivenColumnClassifier(opts);
		var ctx = new ColumnSecurityContext(0, 1);

		Assert.False(await cls.IsRestrictedAsync(1, null, null, ctx));
		Assert.False(await cls.IsRestrictedAsync(1, "   ", null, ctx));
	}

	// =========================================================
	// 成本治理：灰度主开关（Off = 整条治理关闭）
	// =========================================================

	[Fact]
	public void CostGovernance_MasterOff_DefaultNoAction()
	{
		var policy = new ThresholdCostGovernancePolicy(Options.Create(new CostGovernanceOptions()));
		var v = policy.Evaluate(
			new QueryCostAssessment { JoinCount = 50, IsUnbounded = true },
			null, new CostGovernanceContext(0, false));

		Assert.Equal(CostGovernanceAction.NoAction, v.Action);
	}

	[Fact]
	public void CostGovernance_MasterThreshold_JoinMaxRejects()
	{
		var policy = new ThresholdCostGovernancePolicy(Options.Create(new CostGovernanceOptions
		{
			Mode = CostGovernanceMode.Threshold,
			EnableJoinGuard = true,
			MaxJoins = 3
		}));
		var v = policy.Evaluate(new QueryCostAssessment { JoinCount = 5 }, null, new CostGovernanceContext(0, false));

		Assert.Equal(CostGovernanceAction.Reject, v.Action);
	}

	[Fact]
	public void CostGovernance_MasterThreshold_UnboundedDegrades()
	{
		var policy = new ThresholdCostGovernancePolicy(Options.Create(new CostGovernanceOptions
		{
			Mode = CostGovernanceMode.Threshold,
			EnableUnboundedGuard = true,
			UnboundedDefaultLimit = 150
		}));
		var v = policy.Evaluate(new QueryCostAssessment { IsUnbounded = true }, null, new CostGovernanceContext(0, false));

		Assert.Equal(CostGovernanceAction.Degrade, v.Action);
		Assert.Equal(150, v.AppliedLimit);
	}

	// =========================================================
	// 成本治理：管线阶段接入遥测
	// =========================================================

	[Fact]
	public async Task Stage_EmitsTelemetry_OnRejectVerdict()
	{
		var telemetry = new FakeTelemetry();
		var stage = new QueryPlanCostGovernanceStage(
			new StructuralQueryCostClassifier(),
			new FakeRejectPolicy(),
			new FakeCostResolver(new CostGovernanceContext(0, false)),
			telemetry,
			Options.Create(new CostGovernanceOptions { Mode = CostGovernanceMode.Threshold }));
		var ctx = BuildEnablementContext(new QueryPlan());

		await stage.ExecuteAsync(ctx);

		Assert.NotNull(ctx.EarlyResponse);
		Assert.True(ctx.Decision!.IsReject);
		Assert.Equal(1, telemetry.Records.Count);
	}

	[Fact]
	public async Task Stage_NoAction_DoesNotEmitTelemetry()
	{
		var telemetry = new FakeTelemetry();
		var stage = new QueryPlanCostGovernanceStage(
			new StructuralQueryCostClassifier(),
			new ThresholdCostGovernancePolicy(Options.Create(new CostGovernanceOptions
			{
				Mode = CostGovernanceMode.Threshold,
				EnableUnboundedGuard = true
			})),
			new FakeCostResolver(new CostGovernanceContext(0, false)),
			telemetry,
			Options.Create(new CostGovernanceOptions { Mode = CostGovernanceMode.Threshold }));
		var ctx = BuildEnablementContext(new QueryPlan { Limit = 50 }); // 有界 → 不触发

		await stage.ExecuteAsync(ctx);

		Assert.Null(ctx.EarlyResponse);
		Assert.True(ctx.Decision!.IsAllow);
		Assert.Equal(0, telemetry.Records.Count);
	}

	// =========================================================
	// 成本治理：遥测路由（None ↔ Log）
	// =========================================================

	[Fact]
	public async Task ConfigurableTelemetry_LogMode_DelegatesToLog()
	{
		var logger = new FakeLogger();
		var cfg = new ConfigurableModelCostTelemetry(
			Options.Create(new CostGovernanceOptions { TelemetryMode = CostTelemetryMode.Log }),
			new NoOpModelCostTelemetry(),
			new LogModelCostTelemetry(logger));

		await cfg.RecordAsync(
			new QueryCostAssessment { JoinCount = 9 },
			new CostGovernanceVerdict { Action = CostGovernanceAction.Reject, Reason = "too many joins" },
			new CostGovernanceContext(0, false));

		Assert.Equal(1, logger.Warnings.Count);
	}

	[Fact]
	public async Task ConfigurableTelemetry_NoneMode_NoLog()
	{
		var logger = new FakeLogger();
		var cfg = new ConfigurableModelCostTelemetry(
			Options.Create(new CostGovernanceOptions { TelemetryMode = CostTelemetryMode.None }),
			new NoOpModelCostTelemetry(),
			new LogModelCostTelemetry(logger));

		await cfg.RecordAsync(
			new QueryCostAssessment { JoinCount = 9 },
			new CostGovernanceVerdict { Action = CostGovernanceAction.Reject, Reason = "x" },
			new CostGovernanceContext(0, false));

		Assert.Equal(0, logger.Warnings.Count);
	}

	[Fact]
	public async Task LogTelemetry_NoAction_DoesNotLog()
	{
		var logger = new FakeLogger();
		var tel = new LogModelCostTelemetry(logger);

		await tel.RecordAsync(new QueryCostAssessment(), new CostGovernanceVerdict(), new CostGovernanceContext(0, false));

		Assert.Equal(0, logger.Warnings.Count);
	}

	// =========================================================
	// 测试假对象与辅助
	// =========================================================

	private static QueryPlanPipelineContext BuildEnablementContext(QueryPlan plan, QueryPlanDecision? decision = null) =>
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

	private sealed class FakeTelemetry : IModelCostTelemetry
	{
		public List<(QueryCostAssessment, CostGovernanceVerdict, CostGovernanceContext)> Records { get; } = new();
		public Task RecordAsync(QueryCostAssessment a, CostGovernanceVerdict v, CostGovernanceContext c, CancellationToken ct = default)
		{
			Records.Add((a, v, c));
			return Task.CompletedTask;
		}
	}

	private sealed class FakeRejectPolicy : ICostGovernancePolicy
	{
		public CostGovernanceVerdict Evaluate(QueryCostAssessment a, QueryPlanDecision? d, CostGovernanceContext c) =>
			new() { Action = CostGovernanceAction.Reject, Reason = "rejected" };
	}

	private sealed class FakeCostResolver : ICostGovernanceContextResolver
	{
		private readonly CostGovernanceContext _ctx;
		public FakeCostResolver(CostGovernanceContext ctx) => _ctx = ctx;
		public Task<CostGovernanceContext> ResolveAsync(QueryPlanPipelineContext ctx, CancellationToken ct = default) =>
			Task.FromResult(_ctx);
	}

	private sealed class FakeLogger : ILogger<LogModelCostTelemetry>
	{
		public List<string> Warnings { get; } = new();
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
		public bool IsEnabled(LogLevel logLevel) => true;
		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			if (logLevel >= LogLevel.Warning) Warnings.Add(formatter(state, exception));
		}
	}
}
