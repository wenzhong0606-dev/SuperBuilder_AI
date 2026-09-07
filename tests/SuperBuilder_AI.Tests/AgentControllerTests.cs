using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Controllers;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.Agent;
using SuperBuilder_AI.Interfaces.Agent.Runtime;
using SuperBuilder_AI.Models.Agent;
using SuperBuilder_AI.Services.Agent;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// P9.3 AgentController 单测（手写种子 + SQLite 内存库，不依赖 Moq）。
/// 覆盖：租户作用域 CRUD、跨租户隔离、全局模板不可改/删、重复 Code 冲突、
/// 从描述生成 Agent（LLM 路径，含畸形返回 502）、工具目录、异常原因分析链路、编辑器蓝图。
/// </summary>
public class AgentControllerTests
{
	private const long Tenant5 = 5;

	/// <summary>假 Qwen 服务，用于隔离测试生成路径。</summary>
	private sealed class FakeQwen : IQwenService
	{
		private readonly string _response;
		public FakeQwen(string response) => _response = response;
		public Task<string> GenerateSqlAsync(string prompt) => Task.FromResult(_response);
	}

	private static SuperBIContext CreateContext(out SqliteConnection connection)
	{
		connection = new SqliteConnection("DataSource=:memory:");
		connection.Open();
		var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
		var ctx = new SuperBIContext(options);
		ctx.Database.EnsureCreated();
		return ctx;
	}

	private static AgentController Build(SuperBIContext db, string qwenResponse = "ignored") =>
		new(db, new AgentDslSerializer(), new AgentPlanner(new AgentDslSerializer(), new FakeQwen(qwenResponse)), new NullAgentRuntime());

	/// <summary>运行时桩：现有 CRUD/目录测试不触达运行端点，返回占位运行即可。</summary>
	private sealed class NullAgentRuntime : IAgentRuntime
	{
		public Task<AgentRun> StartRunAsync(long tenantId, string planCode, string? actor = null, IReadOnlySet<string>? grantedTools = null, CancellationToken cancellationToken = default)
			=> Task.FromResult(new AgentRun { TenantId = tenantId, PlanCode = planCode, Status = AgentRunStatuses.Queued });
		public Task<AgentRun> ApproveAsync(long tenantId, long runId, CancellationToken cancellationToken = default)
			=> Task.FromResult(new AgentRun { Id = runId, TenantId = tenantId, Status = AgentRunStatuses.Approved });
		public Task<AgentRun> RejectAsync(long tenantId, long runId, CancellationToken cancellationToken = default)
			=> Task.FromResult(new AgentRun { Id = runId, TenantId = tenantId, Status = AgentRunStatuses.Rejected });
	}

	[Fact]
	public async Task Create_Then_Get_Returns_Dsl()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var controller = Build(ctx);
		var result = await controller.CreatePlan(
			new AgentController.CreateAgentPlanRequest(Tenant5, "查询本月销售额"),
			CancellationToken.None) as Microsoft.AspNetCore.Mvc.CreatedAtActionResult;

		Assert.NotNull(result);
		Assert.Equal((int)HttpStatusCode.Created, result!.StatusCode);

		var get = await controller.GetByCode("query-benzu-xiaoshou-e", Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
		// Code 由意图 slug 生成；用返回体里的 code 再取一次更稳。
		var detail = Assert.IsType<AgentController.AgentDetail>(((Microsoft.AspNetCore.Mvc.CreatedAtActionResult)result).Value!);
		var get2 = await controller.GetByCode(detail.Code, Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
		Assert.NotNull(get2);
		var fetched = Assert.IsType<AgentController.AgentDetail>(get2!.Value);
		Assert.Equal(detail.Code, fetched.Code);
		Assert.Equal(AgentStatuses.Draft, fetched.Status);
		// DslJson 是完整的 AgentDsl 文档，应包含所选工具（ASCII 字段名）。
		Assert.Contains("query", fetched.DslJson);
	}

	[Fact]
	public async Task Create_AnomalyIntent_AttachesAnomalyChain()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var controller = Build(ctx);
		var result = await controller.CreatePlan(
			new AgentController.CreateAgentPlanRequest(Tenant5, "分析销售额为什么下降"),
			CancellationToken.None) as Microsoft.AspNetCore.Mvc.CreatedAtActionResult;
		Assert.NotNull(result);

		var detail = Assert.IsType<AgentController.AgentDetail>(result!.Value!);
		// 异常意图应附加 6 步归因链（销售额→同比→环比→区域→客户→产品→渠道）。
		Assert.Contains("anomalyChain", detail.DslJson);
		Assert.Contains("time", detail.DslJson);
		Assert.Contains("channel", detail.DslJson);
	}

	[Fact]
	public async Task Create_BuiltInTenantId_Rejected()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var result = await Build(ctx).CreatePlan(
			new AgentController.CreateAgentPlanRequest(0, "查询销售额"),
			CancellationToken.None) as Microsoft.AspNetCore.Mvc.BadRequestObjectResult;
		Assert.NotNull(result);
	}

	[Fact]
	public async Task Create_EmptyIntent_Rejected()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var result = await Build(ctx).CreatePlan(
			new AgentController.CreateAgentPlanRequest(Tenant5, "   "),
			CancellationToken.None) as Microsoft.AspNetCore.Mvc.BadRequestObjectResult;
		Assert.NotNull(result);
	}

	[Fact]
	public async Task Create_IntentWithoutTool_BadRequest()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		// 意图无任何可识别工具关键词 → planner 返回 Fail（确定性）。
		var result = await Build(ctx).CreatePlan(
			new AgentController.CreateAgentPlanRequest(Tenant5, "你好世界"),
			CancellationToken.None) as Microsoft.AspNetCore.Mvc.BadRequestObjectResult;
		Assert.NotNull(result);
	}

	[Fact]
	public async Task Create_DuplicateCode_Conflict()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var controller = Build(ctx);
		await controller.CreatePlan(new AgentController.CreateAgentPlanRequest(Tenant5, "查询销售额", "dup-agent"), CancellationToken.None);
		var dup = await controller.CreatePlan(new AgentController.CreateAgentPlanRequest(Tenant5, "查询利润", "dup-agent"), CancellationToken.None)
			as Microsoft.AspNetCore.Mvc.ConflictObjectResult;
		Assert.NotNull(dup);
	}

	[Fact]
	public async Task CrossTenant_Isolation_OtherTenantCannotSeeAgent()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var controller = Build(ctx);
		var created = await controller.CreatePlan(new AgentController.CreateAgentPlanRequest(Tenant5, "查询销售额"), CancellationToken.None)
			as Microsoft.AspNetCore.Mvc.CreatedAtActionResult;
		var code = Assert.IsType<AgentController.AgentDetail>(created!.Value!).Code;

		// 租户 7 作用域下按 code 查 → 404（跨租户不可见）。
		var other = await controller.GetByCode(code, 7, CancellationToken.None);
		Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(other);

		// 所属租户作用域下可见。
		var own = await controller.GetByCode(code, Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
		Assert.NotNull(own);
	}

	[Fact]
	public async Task Update_PreservesStatusAndCode()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		// 播种一个已发布的租户 Agent。
		ctx.AgentPlans.Add(new AgentPlan
		{
			TenantId = Tenant5,
			Code = "agentx",
			Name = "原名称",
			Status = AgentStatuses.Published,
			DslVersion = AgentDslVersions.Current,
			DslJson = "{}",
		});
		await ctx.SaveChangesAsync();

		var controller = Build(ctx);
		var result = await controller.Update(
			"agentx",
			new AgentController.UpdateAgentPlanRequest("重新查询利润"),
			Tenant5,
			CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
		Assert.NotNull(result);

		var detail = Assert.IsType<AgentController.AgentDetail>(result!.Value);
		Assert.Equal("agentx", detail.Code);               // Code 不可变
		Assert.Equal(AgentStatuses.Published, detail.Status); // Status 保持不变
		Assert.Contains("query", detail.DslJson);          // 新意图重新编排出查询工具
	}

	[Fact]
	public async Task Update_BuiltIn_Rejected()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		ctx.AgentPlans.Add(new AgentPlan
		{
			TenantId = 0,
			Code = "global-agent",
			Name = "全局 Agent",
			Status = AgentStatuses.Published,
			DslVersion = AgentDslVersions.Current,
			DslJson = "{}",
		});
		await ctx.SaveChangesAsync();

		var result = await Build(ctx).Update(
			"global-agent",
			new AgentController.UpdateAgentPlanRequest("重新查询"),
			Tenant5,
			CancellationToken.None) as Microsoft.AspNetCore.Mvc.BadRequestObjectResult;
		Assert.NotNull(result);
	}

	[Fact]
	public async Task Delete_TenantAgent_Removes()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var controller = Build(ctx);
		var created = await controller.CreatePlan(new AgentController.CreateAgentPlanRequest(Tenant5, "查询销售额"), CancellationToken.None)
			as Microsoft.AspNetCore.Mvc.CreatedAtActionResult;
		var code = Assert.IsType<AgentController.AgentDetail>(created!.Value!).Code;

		var del = await controller.Delete(code, Tenant5, CancellationToken.None)
			as Microsoft.AspNetCore.Mvc.NoContentResult;
		Assert.NotNull(del);

		var get = await controller.GetByCode(code, Tenant5, CancellationToken.None);
		Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(get);
	}

	[Fact]
	public async Task Delete_BuiltIn_Rejected()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		ctx.AgentPlans.Add(new AgentPlan
		{
			TenantId = 0,
			Code = "global-agent",
			Name = "全局 Agent",
			Status = AgentStatuses.Published,
			DslVersion = AgentDslVersions.Current,
			DslJson = "{}",
		});
		await ctx.SaveChangesAsync();

		var result = await Build(ctx).Delete("global-agent", Tenant5, CancellationToken.None)
			as Microsoft.AspNetCore.Mvc.BadRequestObjectResult;
		Assert.NotNull(result);
	}

	[Fact]
	public async Task List_ScopedReturns_TenantAgents()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var controller = Build(ctx);
		await controller.CreatePlan(new AgentController.CreateAgentPlanRequest(Tenant5, "查询销售额"), CancellationToken.None);

		var result = await controller.List(Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
		Assert.NotNull(result);
		var list = Assert.IsType<List<AgentController.AgentSummary>>(result!.Value);
		Assert.Contains(list, s => s.TenantId == Tenant5);
	}

	[Fact]
	public async Task Generate_ValidLlmJson_CreatesAgent_WithAi()
	{
		var llmJson = """
			{
			  "version": "1.0",
			  "name": "库存异常分析",
			  "selectedTools": [
			    { "tool": "query", "order": 1, "reason": "取库存指标" },
			    { "tool": "dashboard", "order": 2, "reason": "可视化下钻" }
			  ],
			  "anomalyChain": [
			    { "order": 1, "dimension": "time", "direction": "yoy", "tool": "query", "description": "库存同比" },
			    { "order": 2, "dimension": "region", "direction": "drill", "tool": "dashboard", "description": "区域下钻" }
			  ]
			}
			""";
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var result = await Build(ctx, qwenResponse: llmJson).GeneratePlan(
			new AgentController.GenerateAgentPlanRequest(3, "做一个库存异常分析"),
			CancellationToken.None) as Microsoft.AspNetCore.Mvc.CreatedAtActionResult;
		Assert.NotNull(result);

		var created = await ctx.AgentPlans
			.IgnoreQueryFilters()
			.FirstOrDefaultAsync(p => p.TenantId == 3 && p.Name == "库存异常分析");
		Assert.NotNull(created);
		Assert.Equal(AgentStatuses.Draft, created!.Status);
	}

	[Fact]
	public async Task Generate_MalformedLlmJson_Returns502()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var result = await Build(ctx, qwenResponse: "{ not json").GeneratePlan(
			new AgentController.GenerateAgentPlanRequest(3, "做一个分析"),
			CancellationToken.None) as Microsoft.AspNetCore.Mvc.ObjectResult;
		Assert.NotNull(result);
		Assert.Equal(502, result!.StatusCode);
	}

	[Fact]
	public async Task ToolsCatalog_Returns_AllEightTools()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var result = Build(ctx).ToolsCatalog() as Microsoft.AspNetCore.Mvc.OkObjectResult;
		Assert.NotNull(result);
		var tools = Assert.IsAssignableFrom<IReadOnlyList<AgentToolDescriptor>>(result!.Value);
		Assert.Equal(AgentTools.Supported.Count, tools.Count);
		Assert.Contains(tools, t => t.Tool == AgentTools.Query);
		Assert.Contains(tools, t => t.Tool == AgentTools.Workflow);
	}

	[Fact]
	public async Task AnomalyChain_Returns_SixSteps_WithMetric()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var result = Build(ctx).AnomalyChain(metric: "销售额", entity: "sales_order") as Microsoft.AspNetCore.Mvc.OkObjectResult;
		Assert.NotNull(result);
		var chain = Assert.IsType<AgentController.AgentAnomalyChainResponse>(result!.Value);
		Assert.Equal("销售额", chain.Metric);
		Assert.Equal("sales_order", chain.Entity);
		Assert.Equal(6, chain.Steps.Count);
		// 链路维度遍历顺序：时间(同比/环比) → 区域 → 客户 → 产品 → 渠道。
		Assert.Equal(AnalysisDimensions.Time, chain.Steps[0].Dimension);
		Assert.Equal(AnalysisDirections.YoY, chain.Steps[0].Direction);
		Assert.Equal(AnalysisDimensions.Channel, chain.Steps[5].Dimension);
	}

	[Fact]
	public async Task EditorBlueprint_Returns_Skeleton_And_Enums()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var result = Build(ctx).EditorBlueprint() as Microsoft.AspNetCore.Mvc.OkObjectResult;
		Assert.NotNull(result);
		var bp = Assert.IsType<AgentController.AgentEditorBlueprint>(result!.Value);
		Assert.Contains(AgentTools.Query, bp.Tools);
		Assert.Contains(AnalysisDimensions.Region, bp.Dimensions);
		Assert.Contains(AnalysisDirections.Drill, bp.Directions);
		Assert.Equal(AgentTools.Supported.Count, bp.ToolCatalog.Count);
		Assert.False(string.IsNullOrWhiteSpace(bp.Skeleton));
		// 骨架本身应当是可反序列化的合法 AgentDsl。
		Assert.True(new AgentDslSerializer().TryDeserialize(bp.Skeleton, out var _dsl, out var _errs));
	}
}
