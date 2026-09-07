using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Agent;
using SuperBuilder_AI.Interfaces.Agent.Runtime;
using SuperBuilder_AI.Models.Agent;
using SuperBuilder_AI.Models.Localization;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.Agent;
using SuperBuilder_AI.Services.Agent.Runtime;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M7-03 Agent Runtime 单测（手写种子 + SQLite 内存库，不依赖 Moq）。
/// 覆盖：权限 deny-by-default、审批闸门（挂起/通过/拒绝）、瞬态重试、状态机、租户隔离、受控工具诚实执行。
/// </summary>
public class AgentRuntimeTests
{
	private const long Tenant5 = 5;
	private const long Tenant6 = 6;

	private static SuperBIContext CreateContext(out SqliteConnection connection)
	{
		connection = new SqliteConnection("DataSource=:memory:");
		connection.Open();
		var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
		var ctx = new SuperBIContext(options);
		ctx.Database.EnsureCreated();
		return ctx;
	}

	/// <summary>插入一个含指定工具序列的 AgentPlan，返回其 Id。</summary>
	private static async Task<long> InsertPlan(SuperBIContext db, long tenantId, string code, params string[] tools)
	{
		var dsl = new AgentDsl
		{
			Version = AgentDslVersions.Current,
			Code = code,
			Name = code,
			UserRequest = code,
			SelectedTools = tools.Select((t, i) => new AgentToolSelection { Tool = t, Order = i + 1 }).ToList(),
		};
		var plan = new AgentPlan
		{
			TenantId = tenantId,
			Code = code,
			Name = code,
			Status = AgentStatuses.Draft,
			DslVersion = AgentDslVersions.Current,
			DslJson = new AgentDslSerializer().Serialize(dsl),
		};
		db.AgentPlans.Add(plan);
		await db.SaveChangesAsync();
		return plan.Id;
	}

	private static IAgentRuntime BuildRuntime(SuperBIContext db, IToolCatalog catalog, int maxAttempts = 3)
		=> new AgentRuntime(db, new AgentDslSerializer(), catalog, new ToolPermissionPolicy(), new RetryPolicy(maxAttempts));

	private static IToolCatalog RealCatalog()
		=> new ControlledToolCatalog(new ITool[]
		{
			new MetadataTool(), new SemanticTool(), new QueryTool(), new DashboardTool(),
			new ForecastTool(), new ReportTool(), new AlertTool(), new WorkflowTool(),
		});

	#region 权限 deny-by-default

	[Fact]
	public async Task StartRun_ReadTool_NotGranted_DeniedByDefault()
	{
		var ctx = CreateContext(out var conn);
		await using var _ = conn;
		await using var __ = ctx;

		await InsertPlan(ctx, Tenant5, "p-query", AgentTools.Query); // Query=Read，默认授权集仅含 Safe
		var runtime = BuildRuntime(ctx, RealCatalog());

		var run = await runtime.StartRunAsync(Tenant5, "p-query", null, null, CancellationToken.None);

		Assert.Equal(AgentRunStatuses.Failed, run.Status);
		Assert.Contains("权限不足", run.ResultSummary ?? "");
	}

	[Fact]
	public async Task StartRun_ReadTool_Granted_Executes()
	{
		var ctx = CreateContext(out var conn);
		await using var _ = conn;
		await using var __ = ctx;

		await InsertPlan(ctx, Tenant5, "p-query", AgentTools.Query);
		var runtime = BuildRuntime(ctx, RealCatalog());
		var granted = new HashSet<string> { AgentTools.Query };

		var run = await runtime.StartRunAsync(Tenant5, "p-query", null, granted, CancellationToken.None);

		Assert.Equal(AgentRunStatuses.Succeeded, run.Status);
		Assert.Single(run.GetStepLog());
		Assert.All(run.GetStepLog(), s => Assert.True(s.Succeeded));
	}

	#endregion

	#region 审批闸门

	[Fact]
	public async Task StartRun_WriteTool_Granted_HaltsAtApprovalPending()
	{
		var ctx = CreateContext(out var conn);
		await using var _ = conn;
		await using var __ = ctx;

		await InsertPlan(ctx, Tenant5, "p-wf", AgentTools.Workflow); // Workflow=Write
		var runtime = BuildRuntime(ctx, RealCatalog());
		var granted = new HashSet<string> { AgentTools.Workflow };

		var run = await runtime.StartRunAsync(Tenant5, "p-wf", null, granted, CancellationToken.None);

		Assert.Equal(AgentRunStatuses.ApprovalPending, run.Status);
		Assert.True(run.GetStepLog().Last().PendingApproval);
		Assert.Null(run.FinishedAt); // 挂起，未结束
	}

	[Fact]
	public async Task Approve_AfterApprovalPending_ResumesToSucceeded()
	{
		var ctx = CreateContext(out var conn);
		await using var _ = conn;
		await using var __ = ctx;

		await InsertPlan(ctx, Tenant5, "p-wf", AgentTools.Workflow);
		var runtime = BuildRuntime(ctx, RealCatalog());
		var granted = new HashSet<string> { AgentTools.Workflow };

		var run = await runtime.StartRunAsync(Tenant5, "p-wf", null, granted, CancellationToken.None);
		Assert.Equal(AgentRunStatuses.ApprovalPending, run.Status);

		var approved = await runtime.ApproveAsync(Tenant5, run.Id, CancellationToken.None);

		Assert.Equal(AgentRunStatuses.Succeeded, approved.Status);
		Assert.NotNull(approved.FinishedAt);
		// 该步最终真正执行（非挂起占位）
		var executed = approved.GetStepLog().Where(s => s.Tool == AgentTools.Workflow && !s.PendingApproval).ToList();
		Assert.Single(executed);
		Assert.All(executed, s => Assert.True(s.Succeeded && !s.PendingApproval));
	}

	[Fact]
	public async Task Reject_AfterApprovalPending_Terminates()
	{
		var ctx = CreateContext(out var conn);
		await using var _ = conn;
		await using var __ = ctx;

		await InsertPlan(ctx, Tenant5, "p-wf", AgentTools.Workflow);
		var runtime = BuildRuntime(ctx, RealCatalog());
		var granted = new HashSet<string> { AgentTools.Workflow };

		var run = await runtime.StartRunAsync(Tenant5, "p-wf", null, granted, CancellationToken.None);
		var rejected = await runtime.RejectAsync(Tenant5, run.Id, CancellationToken.None);

		Assert.Equal(AgentRunStatuses.Rejected, rejected.Status);
		Assert.NotNull(rejected.FinishedAt);
	}

	#endregion

	#region 重试（使用受支持工具 query 映射到假工具，绕过 DSL 白名单）

	[Fact]
	public async Task StartRun_TransientFailure_RetriesThenSucceeds()
	{
		var ctx = CreateContext(out var conn);
		await using var _ = conn;
		await using var __ = ctx;

		await InsertPlan(ctx, Tenant5, "p-rt", AgentTools.Query);
		var catalog = new FakeCatalog(new FakeTool(AgentTools.Query, ToolRisk.Read, requiresApproval: false, failTimes: 2));
		var runtime = BuildRuntime(ctx, catalog, maxAttempts: 3);

		var run = await runtime.StartRunAsync(Tenant5, "p-rt", null, new HashSet<string> { AgentTools.Query }, CancellationToken.None);

		Assert.Equal(AgentRunStatuses.Succeeded, run.Status);
		var step = run.GetStepLog().Single();
		Assert.True(step.Succeeded);
		Assert.Equal(3, step.Attempts); // 首次 + 2 次瞬时重试
	}

	[Fact]
	public async Task StartRun_PermanentFailure_ExhaustsRetries_ThenFails()
	{
		var ctx = CreateContext(out var conn);
		await using var _ = conn;
		await using var __ = ctx;

		await InsertPlan(ctx, Tenant5, "p-perm", AgentTools.Query);
		var catalog = new FakeCatalog(new FakeToolAlwaysFail(AgentTools.Query, ToolRisk.Read, requiresApproval: false));
		var runtime = BuildRuntime(ctx, catalog, maxAttempts: 2);

		var run = await runtime.StartRunAsync(Tenant5, "p-perm", null, new HashSet<string> { AgentTools.Query }, CancellationToken.None);

		Assert.Equal(AgentRunStatuses.Failed, run.Status);
		// 非瞬态异常：RetryPolicy 不重试，异常穿透至 AgentRuntime 的 catch 块，
		// 该块记录 Attempts=0（仅瞬态重试成功路径才带真实 Attempts 计数）。
		Assert.Equal(0, run.GetStepLog().Single().Attempts);
	}

	#endregion

	#region 状态机 / 隔离 / 诚实信封

	[Fact]
	public async Task StartRun_TwoSafeTools_AllSucceed()
	{
		var ctx = CreateContext(out var conn);
		await using var _ = conn;
		await using var __ = ctx;

		await InsertPlan(ctx, Tenant5, "p-safe", AgentTools.Metadata, AgentTools.Semantic); // 均为 Safe，默认授权
		var runtime = BuildRuntime(ctx, RealCatalog());

		var run = await runtime.StartRunAsync(Tenant5, "p-safe", null, null, CancellationToken.None);

		Assert.Equal(AgentRunStatuses.Succeeded, run.Status);
		Assert.Equal(2, run.GetStepLog().Count);
		Assert.All(run.GetStepLog(), s => Assert.True(s.Succeeded));
	}

	[Fact]
	public async Task StartRun_SupportedTool_NotInCatalog_FailsAsUnknown()
	{
		var ctx = CreateContext(out var conn);
		await using var _ = conn;
		await using var __ = ctx;

		await InsertPlan(ctx, Tenant5, "p-unk", AgentTools.Query); // 受支持但目录未注册
		var runtime = BuildRuntime(ctx, new FakeCatalog()); // 空目录

		var run = await runtime.StartRunAsync(Tenant5, "p-unk", null, new HashSet<string> { AgentTools.Query }, CancellationToken.None);

		Assert.Equal(AgentRunStatuses.Failed, run.Status);
		Assert.Contains("未知工具", run.ResultSummary ?? "");
	}

	[Fact]
	public async Task Approve_CrossTenant_Rejected()
	{
		var ctx = CreateContext(out var conn);
		await using var _ = conn;
		await using var __ = ctx;

		await InsertPlan(ctx, Tenant5, "p-wf", AgentTools.Workflow);
		var runtime = BuildRuntime(ctx, RealCatalog());
		var granted = new HashSet<string> { AgentTools.Workflow };

		var run = await runtime.StartRunAsync(Tenant5, "p-wf", null, granted, CancellationToken.None);
		// 以不同租户尝试审批 → 应拒绝（403）。
		await Assert.ThrowsAsync<AgentRuntimeException>(() => runtime.ApproveAsync(Tenant6, run.Id, CancellationToken.None));
	}

	[Fact]
	public async Task StartRun_ControlledTool_HonestEnvelope_NoFakeSuccess()
	{
		var ctx = CreateContext(out var conn);
		await using var _ = conn;
		await using var __ = ctx;

		await InsertPlan(ctx, Tenant5, "p-meta", AgentTools.Metadata); // Safe，默认授权
		var runtime = BuildRuntime(ctx, RealCatalog());

		var run = await runtime.StartRunAsync(Tenant5, "p-meta", null, null, CancellationToken.None);

		Assert.Equal(AgentRunStatuses.Succeeded, run.Status);
		var step = run.GetStepLog().Single();
		Assert.Equal("controlled", step.Mode);
		Assert.NotNull(step.Output);
		// 受控（pending）信封须诚实声明未接真实后端、不伪造成功：
		// 信封显式携带 "connected":false（ASCII，规避中文 \uXXXX 转义差异），作为稳健断言锚点。
		Assert.Contains("\"connected\":false", step.Output);
	}

	[Fact]
	public async Task Run_Persisted_ReadableBackFromStore()
	{
		var ctx = CreateContext(out var conn);
		await using var _ = conn;
		await using var __ = ctx;

		await InsertPlan(ctx, Tenant5, "p-safe", AgentTools.Metadata, AgentTools.Semantic);
		var runtime = BuildRuntime(ctx, RealCatalog());

		var run = await runtime.StartRunAsync(Tenant5, "p-safe", null, null, CancellationToken.None);
		Assert.Equal(AgentRunStatuses.Succeeded, run.Status);

		// 从存储重新读取，确认持久化（状态 + 步骤信封）。
		var reloaded = await ctx.AgentRuns.FirstOrDefaultAsync(r => r.Id == run.Id);
		Assert.NotNull(reloaded);
		Assert.Equal(AgentRunStatuses.Succeeded, reloaded!.Status);
		Assert.Equal(2, reloaded.GetStepLog().Count);
	}

	#endregion

	#region M7-04 live 工具接真实后端

	[Fact]
	public async Task StartRun_LiveMetadata_ReturnsRealTables_NotFakeSuccess()
	{
		var ctx = CreateContext(out var conn);
		await using var _ = conn;
		await using var __ = ctx;

		await SeedMetadataAsync(ctx, Tenant5);
		await InsertPlan(ctx, Tenant5, "p-meta-live", AgentTools.Metadata); // Safe，默认授权
		var runtime = BuildRuntime(ctx, LiveCatalog(ctx));

		var run = await runtime.StartRunAsync(Tenant5, "p-meta-live", null, null, CancellationToken.None);

		Assert.Equal(AgentRunStatuses.Succeeded, run.Status);
		var step = run.GetStepLog().Single();
		// live 模式：真实后端返回真实表结构，而非受控回执占位。
		Assert.Equal("live", step.Mode);
		Assert.NotNull(step.Output);
		Assert.Contains("\"connected\":true", step.Output);
		Assert.Contains("t_sales", step.Output);  // 真实表名（ASCII，规避 \uXXXX 转义）
		Assert.Contains("amount", step.Output);    // 真实字段名
	}

	[Fact]
	public async Task StartRun_LiveSemantic_ReturnsRealLabels_NotFakeSuccess()
	{
		var ctx = CreateContext(out var conn);
		await using var _ = conn;
		await using var __ = ctx;

		await SeedSemanticAsync(ctx, Tenant5);
		await InsertPlan(ctx, Tenant5, "p-sem-live", AgentTools.Semantic); // Safe，默认授权
		var runtime = BuildRuntime(ctx, LiveCatalog(ctx));

		var run = await runtime.StartRunAsync(Tenant5, "p-sem-live", null, null, CancellationToken.None);

		Assert.Equal(AgentRunStatuses.Succeeded, run.Status);
		var step = run.GetStepLog().Single();
		Assert.Equal("live", step.Mode);
		Assert.NotNull(step.Output);
		Assert.Contains("\"connected\":true", step.Output);
		Assert.Contains("SalesAmount", step.Output); // 真实标签值（ASCII）
	}

	[Fact]
	public void ToolsCatalog_HonestBackendStatus()
	{
		var all = ToolRegistry.GetAll().ToDictionary(d => d.Tool);
		// 已接真实后端的工具诚实标注 live（M7-04）。
		Assert.Equal("live", all[AgentTools.Metadata].BackendStatus);
		Assert.Equal("live", all[AgentTools.Semantic].BackendStatus);
		Assert.Equal("M7-04", all[AgentTools.Metadata].BackendMilestone);
		// 其余 Read/Write 工具诚实标注 pending（未接真实后端，绝不伪造成功）。
		foreach (var t in new[] { AgentTools.Query, AgentTools.Dashboard, AgentTools.Report, AgentTools.Forecast, AgentTools.Alert, AgentTools.Workflow })
			Assert.Equal("pending", all[t].BackendStatus);
		Assert.Equal(2, all.Values.Count(d => d.BackendStatus == "live"));
		Assert.Equal(6, all.Values.Count(d => d.BackendStatus == "pending"));
	}

	#endregion

	#region 测试辅具

	/// <summary>受控工具目录桩：承载一组假工具。</summary>
	private sealed class FakeCatalog : IToolCatalog
	{
		private readonly Dictionary<string, ITool> _map = new(StringComparer.OrdinalIgnoreCase);
		public FakeCatalog(params ITool[] tools)
		{
			foreach (var t in tools) _map[t.Tool] = t;
		}
		public ITool? Get(string tool) => _map.TryGetValue(tool, out var t) ? t : null;
		public IReadOnlyList<ITool> All => _map.Values.ToList();
	}

	/// <summary>可配置瞬态失败次数的假工具（用于验证重试）。</summary>
	private sealed class FakeTool : ITool
	{
		private int _calls;
		private readonly int _failTimes;
		public FakeTool(string tool, ToolRisk risk, bool requiresApproval, int failTimes = 0)
		{
			Tool = tool;
			Risk = risk;
			RequiresApproval = requiresApproval;
			_failTimes = failTimes;
		}
		public string Tool { get; }
		public ToolRisk Risk { get; }
		public bool RequiresApproval { get; }
		public Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct = default)
		{
			_calls++;
			if (_calls <= _failTimes)
				throw new TransientToolException($"transient #{_calls}");
			var outp = JsonSerializer.Serialize(new { tool = Tool, mode = "controlled", calls = _calls });
			return Task.FromResult(ToolResult.Ok(outp, "controlled", RequiresApproval));
		}
	}

	/// <summary>恒失败的假工具（用于验证不可重试错误直接失败）。</summary>
	private sealed class FakeToolAlwaysFail : ITool
	{
		public FakeToolAlwaysFail(string tool, ToolRisk risk, bool requiresApproval)
		{
			Tool = tool;
			Risk = risk;
			RequiresApproval = requiresApproval;
		}
		public string Tool { get; }
		public ToolRisk Risk { get; }
		public bool RequiresApproval { get; }
		public Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct = default)
			=> throw new InvalidOperationException("permanent failure (non-transient)");
	}

	/// <summary>M7-04 live 目录：metadata/semantic 接真实后端，其余保持受控信封。</summary>
	private static IToolCatalog LiveCatalog(SuperBIContext db)
		=> new ControlledToolCatalog(new ITool[]
		{
			new LiveMetadataTool(db), new LiveSemanticTool(db),
			new QueryTool(), new DashboardTool(), new ForecastTool(), new ReportTool(), new AlertTool(), new WorkflowTool(),
		});

	/// <summary>播种租户 + 数据源 + 元数据表/字段（满足 MetadataTable→DataSource 复合 FK）。</summary>
	private static async Task SeedMetadataAsync(SuperBIContext db, long tenantId)
	{
		db.Tenants.Add(new Tenant { Id = tenantId });
		db.DataSources.Add(new DataSource { Id = 1, TenantId = tenantId, Name = "ds", NormalizedName = "ds", DbType = "MYSQL", ConnectionString = "x", Enabled = true });
		await db.SaveChangesAsync();

		var table = new MetadataTable { TenantId = tenantId, DataSourceId = 1, TableName = "t_sales", TableComment = "销售表" };
		db.MetadataTables.Add(table);
		await db.SaveChangesAsync();

		db.MetadataColumns.Add(new MetadataColumn { MetadataTableId = table.Id, ColumnName = "amount", DataType = "decimal", Ordinal = 0, IsPrimaryKey = false });
		await db.SaveChangesAsync();
	}

	/// <summary>播种租户 + 一条语义标签。</summary>
	private static async Task SeedSemanticAsync(SuperBIContext db, long tenantId)
	{
		db.Tenants.Add(new Tenant { Id = tenantId });
		db.SemanticLabels.Add(new SemanticLabel
		{
			TenantId = tenantId,
			ConceptType = "Metric",
			ConceptId = 1,
			Culture = "zh-CN",
			LabelKind = "DisplayName",
			Value = "SalesAmount",
			SortOrder = 0,
		});
		await db.SaveChangesAsync();
	}

	#endregion
}
