using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Controllers;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.AppBuilder;
using SuperBuilder_AI.Models.AppBuilder;
using SuperBuilder_AI.Services.AppBuilder;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// P8.3 AppBuilderController 单测（手写种子 + SQLite 内存库，不依赖 Moq）。
/// 覆盖：租户作用域 CRUD、跨租户隔离、全局模板不可改/删、重复 Code 冲突、
/// 从描述生成应用（LLM 路径，含畸形返回 502）、编辑器蓝图。
/// </summary>
public class AppBuilderControllerTests
{
	private const long Tenant5 = 5;

	/// <summary>假 Qwen 服务，用于隔离测试生成路径。</summary>
	private sealed class FakeQwen : IQwenService
	{
		private readonly string _response;
		public FakeQwen(string response) => _response = response;
		public Task<string> GenerateSqlAsync(string prompt) => Task.FromResult(_response);
	}

	private static string DslJson(string code, string name) =>
		new AppDslSerializer().Serialize(new AppDsl
		{
			Version = AppDslVersions.Current,
			Code = code,
			Name = name,
			Pages = new List<PagePlan>
			{
				new()
				{
					Id = "home",
					Name = "首页",
					Order = 1,
					Components = new List<ComponentPlan>
					{
						new()
						{
							Type = AppComponentTypes.Kpi,
							Id = "k1",
							Title = "KPI",
							Binding = new AppDataSourceBinding
							{
								Entity = "sales_order",
								Metrics = new List<AppMetricBinding> { new() { Field = "amount", Aggregation = AppAggregateTypes.Sum } },
							},
						},
					},
				},
			},
		});

	private static string DslJsonNoPages() =>
		new AppDslSerializer().Serialize(new AppDsl { Name = "缺页面" });

	private static SuperBIContext CreateContext(out SqliteConnection connection)
	{
		connection = new SqliteConnection("DataSource=:memory:");
		connection.Open();
		var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
		var ctx = new SuperBIContext(options);
		ctx.Database.EnsureCreated();
		return ctx;
	}

	private static AppBuilderAgent CreateAgent(string qwenResponse) =>
		new(new AppDslSerializer(), new FakeQwen(qwenResponse));

	private static AppBuilderController Build(SuperBIContext db, string qwenResponse = "ignored") =>
		new(db, new AppDslSerializer(), CreateAgent(qwenResponse));

	[Fact]
	public async Task Create_Then_Get_Returns_Dsl()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var controller = Build(ctx);
		var result = await controller.Create(
			new AppBuilderController.CreateAppRequest(Tenant5, DslJson("app5", "应用5")),
			CancellationToken.None) as Microsoft.AspNetCore.Mvc.CreatedAtActionResult;

		Assert.NotNull(result);
		Assert.Equal((int)HttpStatusCode.Created, result!.StatusCode);

		var get = await controller.GetByCode("app5", Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
		Assert.NotNull(get);
		var detail = Assert.IsType<AppBuilderController.AppDetail>(get!.Value);
		Assert.Equal("app5", detail.Code);
		Assert.Equal("应用5", detail.Name);
		Assert.Equal(AppStatuses.Draft, detail.Status);
		// DslJson 是完整的 AppDsl 文档，应包含所写入的页面元素（中文按默认策略转义，断言用 ASCII 子串）。
		Assert.Contains("home", detail.DslJson);
		Assert.Contains("sales_order", detail.DslJson);
	}

	[Fact]
	public async Task Create_BuiltInTenantId_Rejected()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var result = await Build(ctx).Create(
			new AppBuilderController.CreateAppRequest(0, DslJson("x", "X")),
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
		await controller.Create(new AppBuilderController.CreateAppRequest(Tenant5, DslJson("dup", "Dup1")), CancellationToken.None);
		var dup = await controller.Create(new AppBuilderController.CreateAppRequest(Tenant5, DslJson("dup", "Dup2")), CancellationToken.None)
			as Microsoft.AspNetCore.Mvc.ConflictObjectResult;
		Assert.NotNull(dup);
	}

	[Fact]
	public async Task Create_InvalidDsl_BadRequest()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		// 缺页面的 DSL：JSON 合法但业务校验失败。
		var result = await Build(ctx).Create(
			new AppBuilderController.CreateAppRequest(Tenant5, DslJsonNoPages()),
			CancellationToken.None) as Microsoft.AspNetCore.Mvc.BadRequestObjectResult;
		Assert.NotNull(result);
	}

	[Fact]
	public async Task CrossTenant_Isolation_OtherTenantCannotSeeApp()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var controller = Build(ctx);
		await controller.Create(new AppBuilderController.CreateAppRequest(Tenant5, DslJson("app5", "应用5")), CancellationToken.None);

		// 租户 7 作用域下按 code 查 app5 → 404（跨租户不可见）。
		var other = await controller.GetByCode("app5", 7, CancellationToken.None);
		Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(other);

		// 所属租户作用域下可见。
		var own = await controller.GetByCode("app5", Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
		Assert.NotNull(own);
	}

	[Fact]
	public async Task Update_PreservesStatusAndCode()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		// 播种一个已发布的租户应用。
		ctx.AppPlans.Add(new AppPlan
		{
			TenantId = Tenant5,
			Code = "appx",
			Name = "原名称",
			Status = AppStatuses.Published,
			DslVersion = AppDslVersions.Current,
			DslJson = "{}",
		});
		await ctx.SaveChangesAsync();

		var controller = Build(ctx);
		var newDsl = DslJson("appx", "新名称");
		var result = await controller.Update(
			"appx",
			new AppBuilderController.UpdateAppRequest(newDsl),
			Tenant5,
			CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
		Assert.NotNull(result);

		var detail = Assert.IsType<AppBuilderController.AppDetail>(result!.Value);
		Assert.Equal("新名称", detail.Name);              // Name 随 DSL 更新
		Assert.Equal("appx", detail.Code);               // Code 不可变
		Assert.Equal(AppStatuses.Published, detail.Status); // Status 保持不变
	}

	[Fact]
	public async Task Update_BuiltIn_Rejected()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		// 播种一条全局应用行（TenantId=0）。
		ctx.AppPlans.Add(new AppPlan
		{
			TenantId = 0,
			Code = "global-app",
			Name = "全局应用",
			Status = AppStatuses.Published,
			DslVersion = AppDslVersions.Current,
			DslJson = DslJson("global-app", "全局应用"),
		});
		await ctx.SaveChangesAsync();

		var result = await Build(ctx).Update(
			"global-app",
			new AppBuilderController.UpdateAppRequest(DslJson("global-app", "改后")),
			Tenant5,
			CancellationToken.None) as Microsoft.AspNetCore.Mvc.BadRequestObjectResult;
		Assert.NotNull(result);
	}

	[Fact]
	public async Task Delete_TenantApp_Removes()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var controller = Build(ctx);
		await controller.Create(new AppBuilderController.CreateAppRequest(Tenant5, DslJson("to-del", "待删")), CancellationToken.None);
		var del = await controller.Delete("to-del", Tenant5, CancellationToken.None)
			as Microsoft.AspNetCore.Mvc.NoContentResult;
		Assert.NotNull(del);

		var get = await controller.GetByCode("to-del", Tenant5, CancellationToken.None);
		Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(get);
	}

	[Fact]
	public async Task Delete_BuiltIn_Rejected()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		ctx.AppPlans.Add(new AppPlan
		{
			TenantId = 0,
			Code = "global-app",
			Name = "全局应用",
			Status = AppStatuses.Published,
			DslVersion = AppDslVersions.Current,
			DslJson = DslJson("global-app", "全局应用"),
		});
		await ctx.SaveChangesAsync();

		var result = await Build(ctx).Delete("global-app", Tenant5, CancellationToken.None)
			as Microsoft.AspNetCore.Mvc.BadRequestObjectResult;
		Assert.NotNull(result);
	}

	[Fact]
	public async Task List_ScopedReturns_TenantApps()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var controller = Build(ctx);
		await controller.Create(new AppBuilderController.CreateAppRequest(Tenant5, DslJson("tenant-only", "租户应用")), CancellationToken.None);

		var result = await controller.List(Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
		Assert.NotNull(result);
		var list = Assert.IsType<List<AppBuilderController.AppSummary>>(result!.Value);
		Assert.Contains(list, s => s.Code == "tenant-only" && s.TenantId == Tenant5);
	}

	[Fact]
	public async Task Generate_ValidLlmJson_CreatesApp_WithAi()
	{
		var llmJson = """
			{
			  "version": "1.0",
			  "name": "库存看板",
			  "pages": [
			    {
			      "id": "p1", "name": "库存", "order": 1,
			      "components": [
			        { "type": "kpi", "id": "k1", "title": "总库存",
			          "binding": { "entity": "stock", "metrics": [ { "field": "qty", "aggregation": "sum" } ], "dimensions": [], "filters": [] } }
			      ]
			    }
			  ]
			}
			""";
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var result = await Build(ctx, qwenResponse: llmJson).Generate(
			new AppBuilderController.GenerateAppRequest(3, "做一个库存看板"),
			CancellationToken.None) as Microsoft.AspNetCore.Mvc.CreatedAtActionResult;
		Assert.NotNull(result);

		var created = await ctx.AppPlans
			.IgnoreQueryFilters()
			.FirstOrDefaultAsync(p => p.TenantId == 3 && p.Name == "库存看板");
		Assert.NotNull(created);
		Assert.Equal(AppStatuses.Draft, created!.Status);
	}

	[Fact]
	public async Task Generate_MalformedLlmJson_Returns502()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var result = await Build(ctx, qwenResponse: "{ not json").Generate(
			new AppBuilderController.GenerateAppRequest(3, "做一个看板"),
			CancellationToken.None) as Microsoft.AspNetCore.Mvc.ObjectResult;
		Assert.NotNull(result);
		Assert.Equal(502, result!.StatusCode);
	}

	[Fact]
	public async Task EditorBlueprint_Returns_Skeleton_And_Enums()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var result = Build(ctx).EditorBlueprint() as Microsoft.AspNetCore.Mvc.OkObjectResult;
		Assert.NotNull(result);
		var bp = Assert.IsType<AppBuilderController.AppEditorBlueprint>(result!.Value);
		Assert.Contains(AppComponentTypes.Kpi, bp.ComponentTypes);
		Assert.Contains(AppAggregateTypes.Sum, bp.AggregateTypes);
		Assert.Contains(AppFilterOperators.Eq, bp.FilterOperators);
		Assert.Contains(AppLayoutKinds.Grid, bp.LayoutKinds);
		Assert.False(string.IsNullOrWhiteSpace(bp.Skeleton));
		// 骨架本身应当是可反序列化的合法 AppDsl。
		Assert.True(new AppDslSerializer().TryDeserialize(bp.Skeleton, out var _dsl, out var _errs));
	}
}
