using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Controllers;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.BI.Dashboard;
using SuperBuilder_AI.Interfaces.Platform;
using SuperBuilder_AI.Interfaces.Theme;
using SuperBuilder_AI.Models.Dashboard;
using SuperBuilder_AI.Models.Dashboard.Rendering;
using SuperBuilder_AI.Models.Organization;

// 实体类 Dashboard 与命名空间 SuperBuilder_AI.Models.Dashboard 同名，用别名消歧。
using DashboardEntity = SuperBuilder_AI.Models.Dashboard.Dashboard;

using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// P6.4 DashboardController 单测（手写轻量 fake + SQLite 内存库，不依赖 Moq）。
/// 覆盖 CRUD、租户作用域隔离、渲染编排与编辑器蓝图。
/// </summary>
public class DashboardControllerTests
{
	private sealed class FakeSerializer : IDashboardDslSerializer
	{
		public bool FailDeserialize;
		public List<string> ValidateErrors { get; set; } = new();
		public string LastSerialized { get; set; } = "{\"v\":1}";
		public DashboardDsl? NextDsl { get; set; }

		public string Serialize(DashboardDsl dsl) => LastSerialized;

		public bool TryDeserialize(string? json, out DashboardDsl? dsl, out IReadOnlyList<string> errors)
		{
			if (FailDeserialize)
			{
				dsl = null;
				errors = new[] { "DSL 解析失败（fake）" };
				return false;
			}

			dsl = NextDsl ?? new DashboardDsl
			{
				Title = "示例仪表盘",
				Pages = new List<PageDsl> { new() { Id = "p1", Name = "首页", Order = 1 } },
			};
			errors = Array.Empty<string>();
			return true;
		}

		public IReadOnlyList<string> Validate(DashboardDsl dsl) => ValidateErrors;
	}

	private sealed class FakeRenderer : IDashboardRenderer
	{
		public DashboardRenderModel Model { get; set; } = new() { Title = "渲染结果" };

		public Task<DashboardRenderModel> RenderAsync(
			DashboardDsl dsl, PlatformContext context, CancellationToken cancellationToken = default) =>
			Task.FromResult(Model);
	}

	private sealed class FakeAccessor : IPlatformContextAccessor
	{
		public PlatformContext? Current { get; set; }
	}

	private sealed class FakeThemeResolver : IThemeResolver
	{
		public SuperBuilder_AI.Models.Theme.ThemeContext Resolved { get; set; }
			= SuperBuilder_AI.Models.Theme.ThemeContext.Default;

		public Task<SuperBuilder_AI.Models.Theme.ThemeContext> ResolveAsync(
			long tenantId, string? dashboardThemeKey = null, CancellationToken ct = default) =>
			Task.FromResult(Resolved);
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

	private static DashboardController Build(
		SuperBIContext db,
		out FakeSerializer serializer,
		out FakeRenderer renderer,
		out FakeAccessor accessor)
	{
		serializer = new FakeSerializer();
		renderer = new FakeRenderer();
		accessor = new FakeAccessor();
		var themeResolver = new FakeThemeResolver();
		return new DashboardController(serializer, renderer, accessor, themeResolver, db);
	}

	[Fact]
	public async Task Create_ValidDsl_ReturnsCreatedAndPersists()
	{
		var ctx = CreateContext(out var connection);
		await using var connDispose = connection;
		await using var ctxDispose = ctx;

		var controller = Build(ctx, out var serializer, out _, out _);
		var dsl = new DashboardDsl
		{
			Title = "销售看板",
			Pages = new List<PageDsl> { new() { Id = "p1", Name = "首页", Order = 1 } },
		};
		serializer.NextDsl = dsl;

		var result = await controller.Create(
			new CreateDashboardRequest(TenantId: 1, DslJson: "{}", Status: "published"));

		Assert.IsType<CreatedAtActionResult>(result);
		Assert.Equal(1, ctx.Dashboards.Count());
		var entity = ctx.Dashboards.Single();
		Assert.Equal(1, entity.TenantId);
		Assert.Equal("销售看板", entity.Title);
		Assert.Equal("published", entity.Status);
	}

	[Fact]
	public async Task Create_InvalidDsl_ReturnsBadRequest()
	{
		var ctx = CreateContext(out var connection);
		await using var connDispose = connection;
		await using var ctxDispose = ctx;

		var controller = Build(ctx, out var serializer, out _, out _);
		serializer.FailDeserialize = true;

		var result = await controller.Create(new CreateDashboardRequest(TenantId: 1, DslJson: "bad"));

		Assert.IsType<BadRequestObjectResult>(result);
		Assert.Equal(0, ctx.Dashboards.Count());
	}

	[Fact]
	public async Task Create_ValidationFails_ReturnsBadRequest()
	{
		var ctx = CreateContext(out var connection);
		await using var connDispose = connection;
		await using var ctxDispose = ctx;

		var controller = Build(ctx, out var serializer, out _, out _);
		serializer.ValidateErrors = new List<string> { "页面 Id 重复（fake）" };

		var result = await controller.Create(new CreateDashboardRequest(TenantId: 1, DslJson: "{}"));

		Assert.IsType<BadRequestObjectResult>(result);
	}

	[Fact]
	public async Task List_RespectsTenantScope_AndShowsGlobalTemplates()
	{
		var ctx = CreateContext(out var connection);
		await using var connDispose = connection;
		await using var ctxDispose = ctx;

		ctx.Dashboards.AddRange(
			new DashboardEntity { TenantId = 1, Code = "a", Title = "T1", DslJson = "{}" },
			new DashboardEntity { TenantId = 2, Code = "b", Title = "T2", DslJson = "{}" },
			new DashboardEntity { TenantId = 0, Code = "g", Title = "Global", DslJson = "{}" });
		await ctx.SaveChangesAsync();

		var controller = Build(ctx, out _, out _, out _);
		var result = await controller.List(tenantId: 1);

		var ok = Assert.IsType<OkObjectResult>(result);
		var list = Assert.IsAssignableFrom<IEnumerable<DashboardSummary>>(ok.Value).ToList();
		Assert.Contains(list, d => d.TenantId == 1);
		Assert.Contains(list, d => d.TenantId == 0);
		Assert.DoesNotContain(list, d => d.TenantId == 2);
	}

	[Fact]
	public async Task GetById_NotFound_Returns404()
	{
		var ctx = CreateContext(out var connection);
		await using var connDispose = connection;
		await using var ctxDispose = ctx;

		var controller = Build(ctx, out _, out _, out _);
		var result = await controller.GetById(999, tenantId: 1);

		Assert.IsType<NotFoundResult>(result);
	}

	[Fact]
	public async Task GetById_Found_ReturnsOk()
	{
		var ctx = CreateContext(out var connection);
		await using var connDispose = connection;
		await using var ctxDispose = ctx;

		var entity = new DashboardEntity { TenantId = 1, Code = "a", Title = "T1", DslJson = "{}" };
		ctx.Dashboards.Add(entity);
		await ctx.SaveChangesAsync();

		var controller = Build(ctx, out _, out _, out _);
		var result = await controller.GetById(entity.Id, tenantId: 1);

		var ok = Assert.IsType<OkObjectResult>(result);
		var summary = Assert.IsType<DashboardSummary>(ok.Value);
		Assert.Equal("T1", summary.Title);
	}

	[Fact]
	public async Task Update_ChangesTitle()
	{
		var ctx = CreateContext(out var connection);
		await using var connDispose = connection;
		await using var ctxDispose = ctx;

		var entity = new DashboardEntity { TenantId = 1, Code = "a", Title = "旧标题", DslJson = "{}" };
		ctx.Dashboards.Add(entity);
		await ctx.SaveChangesAsync();

		var controller = Build(ctx, out var serializer, out _, out _);
		serializer.NextDsl = new DashboardDsl
		{
			Title = "新标题",
			Pages = new List<PageDsl> { new() { Id = "p1", Name = "首页", Order = 1 } },
		};

		var result = await controller.Update(entity.Id, new CreateDashboardRequest(TenantId: 1, DslJson: "{}"));

		var ok = Assert.IsType<OkObjectResult>(result);
		Assert.Equal("新标题", ctx.Dashboards.Single().Title);
	}

	[Fact]
	public async Task Delete_RemovesEntity()
	{
		var ctx = CreateContext(out var connection);
		await using var connDispose = connection;
		await using var ctxDispose = ctx;

		var entity = new DashboardEntity { TenantId = 1, Code = "a", Title = "T1", DslJson = "{}" };
		ctx.Dashboards.Add(entity);
		await ctx.SaveChangesAsync();

		var controller = Build(ctx, out _, out _, out _);
		var result = await controller.Delete(entity.Id, tenantId: 1);

		Assert.IsType<NoContentResult>(result);
		Assert.Equal(0, ctx.Dashboards.Count());
	}

	[Fact]
	public async Task Render_ReturnsRenderModel_WithTenantScope()
	{
		var ctx = CreateContext(out var connection);
		await using var connDispose = connection;
		await using var ctxDispose = ctx;

		var entity = new DashboardEntity
		{
			TenantId = 7,
			Code = "a",
			Title = "T7",
			DslJson = "{\"title\":\"x\",\"pages\":[]}",
		};
		ctx.Dashboards.Add(entity);
		await ctx.SaveChangesAsync();

		var controller = Build(ctx, out var serializer, out var renderer, out var accessor);
		serializer.NextDsl = new DashboardDsl
		{
			Title = "X",
			Pages = new List<PageDsl> { new() { Id = "p1", Name = "首页", Order = 1 } },
		};

		var result = await controller.Render(entity.Id, tenantId: 0);

		var ok = Assert.IsType<OkObjectResult>(result);
		Assert.Same(renderer.Model, ok.Value);
		Assert.NotNull(accessor.Current);
		Assert.Equal(7, accessor.Current!.Tenant.TenantId);
	}

	[Fact]
	public async Task EditorBlueprint_ReturnsSkeletonAndEnums()
	{
		var ctx = CreateContext(out var connection);
		await using var connDispose = connection;
		await using var ctxDispose = ctx;

		var controller = Build(ctx, out _, out _, out _);
		var result = await Task.FromResult(controller.EditorBlueprint());

		var ok = Assert.IsType<OkObjectResult>(result);
		var blueprint = Assert.IsType<DashboardEditorBlueprint>(ok.Value);
		Assert.Contains("chart", blueprint.WidgetTypes);
		Assert.Contains("column", blueprint.ChartTypes);
		Assert.Contains("SUM", blueprint.AggregateTypes);
		Assert.False(string.IsNullOrWhiteSpace(blueprint.Skeleton));
	}
}
