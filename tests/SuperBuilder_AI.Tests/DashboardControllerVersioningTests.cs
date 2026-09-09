using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
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
using SuperBuilder_AI.Models.Theme;
using SuperBuilder_AI.Services.BI.Dashboard;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M7-01 Dashboard 草稿/发布隔离 + 可追踪回滚 的控制器级验证。
/// 使用 SQLite 内存库（EnsureCreated 按模型建表，含 DashboardVersions）+ 真实 DSL 序列化器，
/// 渲染器/主题解析器/平台上下文访问器用轻量桩（仅满足控制器构造与 ScopeTo 的 HttpContext 写入）。
/// 不触发 BI 查询链路，对 Golden 契约免疫。
/// </summary>
public sealed class DashboardControllerVersioningTests
{
	private static SuperBIContext CreateContext(out SqliteConnection connection)
	{
		connection = new SqliteConnection("DataSource=:memory:");
		connection.Open();
		var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
		var db = new SuperBIContext(options);
		db.Database.EnsureCreated();
		return db;
	}

	private static DashboardController BuildController(SuperBIContext db)
	{
		var controller = new DashboardController(
			new DashboardDslSerializer(),
			new FakeDashboardRenderer(),
			new FakePlatformContextAccessor(),
			new FakeThemeResolver(),
			db)
		{
			ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
		};
		return controller;
	}

	private static string SampleDslJson(DashboardDslSerializer serializer, string title) =>
		serializer.Serialize(new DashboardDsl
		{
			Title = title,
			Code = "code-" + title,
			Pages = new List<PageDsl>
			{
				new()
				{
					Id = "p1",
					Name = "首页",
					Order = 1,
					Layout = new LayoutDsl(),
					Widgets = new List<WidgetDsl> { new TextWidgetDsl { Id = "w1", Content = "hi", Markdown = true } }
				}
			}
		});

	private static async Task<long> CreateDashboardAsync(DashboardController controller, string dslJson, long tenantId = 0)
	{
		var result = await controller.Create(new CreateDashboardRequest(tenantId, dslJson), CancellationToken.None);
		var created = Assert.IsType<CreatedAtActionResult>(result);
		var summary = Assert.IsType<DashboardSummary>(created.Value);
		return summary.Id;
	}

	[Fact]
	public async Task Publish_Creates_Version_Snapshot_And_Sets_PublishedState()
	{
		var db = CreateContext(out var connection);
		try
		{
			var serializer = new DashboardDslSerializer();
			var controller = BuildController(db);
			var dsl = SampleDslJson(serializer, "销售概览");
			var id = await CreateDashboardAsync(controller, dsl);

			var pubResult = await controller.Publish(id, 0, CancellationToken.None);
			var pub = Assert.IsType<PublishResult>(Assert.IsType<OkObjectResult>(pubResult).Value);
			Assert.Equal(1, pub.Version);

			var entity = await db.Dashboards.AsNoTracking().FirstAsync(d => d.Id == id);
			Assert.Equal(DashboardStatuses.Published, entity.Status);
			Assert.Equal(1, entity.PublishedVersion);
			Assert.NotNull(entity.PublishedAt);
			Assert.Equal(dsl, entity.PublishedDslJson);
			Assert.Equal(dsl, entity.DslJson); // 草稿 == 发布态（首次发布尚无差异）

			var versions = await db.DashboardVersions.AsNoTracking().Where(v => v.DashboardId == id).ToListAsync();
			Assert.Single(versions);
			Assert.Equal(1, versions[0].Version);
			Assert.Equal(dsl, versions[0].DslJson);
			Assert.Null(versions[0].RolledBackFromVersion);
		}
		finally
		{
			db.Dispose();
			connection.Dispose();
		}
	}

	[Fact]
	public async Task Draft_Edit_Does_Not_Change_Published_State()
	{
		var db = CreateContext(out var connection);
		try
		{
			var serializer = new DashboardDslSerializer();
			var controller = BuildController(db);
			var dslA = SampleDslJson(serializer, "概览A");
			var id = await CreateDashboardAsync(controller, dslA);
			await controller.Publish(id, 0, CancellationToken.None);

			var dslB = SampleDslJson(serializer, "概览B-修订");
			await controller.Update(id, new CreateDashboardRequest(0, dslB), 0, CancellationToken.None);

			var entity = await db.Dashboards.AsNoTracking().FirstAsync(d => d.Id == id);
			Assert.Equal(dslB, entity.DslJson);              // 草稿已更新
			Assert.Equal(dslA, entity.PublishedDslJson);     // 发布态不变
			Assert.Equal(1, entity.PublishedVersion);        // 版本未变
			Assert.Equal(DashboardStatuses.Published, entity.Status);
		}
		finally
		{
			db.Dispose();
			connection.Dispose();
		}
	}

	[Fact]
	public async Task Rollback_Restores_Previous_Version_And_Creates_NewVersion()
	{
		var db = CreateContext(out var connection);
		try
		{
			var serializer = new DashboardDslSerializer();
			var controller = BuildController(db);
			var dslA = SampleDslJson(serializer, "概览A");
			var dslB = SampleDslJson(serializer, "概览B");
			var id = await CreateDashboardAsync(controller, dslA);
			await controller.Publish(id, 0, CancellationToken.None);          // v1 = A
			await controller.Update(id, new CreateDashboardRequest(0, dslB), 0, CancellationToken.None);
			await controller.Publish(id, 0, CancellationToken.None);          // v2 = B

			var rbResult = await controller.Rollback(id, 1, 0, CancellationToken.None);
			var rb = Assert.IsType<PublishResult>(Assert.IsType<OkObjectResult>(rbResult).Value);
			Assert.Equal(3, rb.Version);                 // 回滚固化为 v3
			Assert.Equal(1, rb.RolledBackFromVersion);   // 来源 v1

			var entity = await db.Dashboards.AsNoTracking().FirstAsync(d => d.Id == id);
			Assert.Equal(3, entity.PublishedVersion);
			Assert.Equal(dslA, entity.PublishedDslJson); // 恢复到 A

			var versions = await db.DashboardVersions.AsNoTracking()
				.Where(v => v.DashboardId == id).OrderBy(v => v.Version).ToListAsync();
			Assert.Equal(3, versions.Count);
			Assert.Equal(dslA, versions[0].DslJson);      // v1 快照不可变
			Assert.Equal(dslB, versions[1].DslJson);      // v2 快照不可变
			Assert.Equal(dslA, versions[2].DslJson);      // v3 = 回滚到 A
			Assert.Equal(1, versions[2].RolledBackFromVersion);
		}
		finally
		{
			db.Dispose();
			connection.Dispose();
		}
	}

	[Fact]
	public async Task Versions_List_Returns_Descending_With_CurrentFlag()
	{
		var db = CreateContext(out var connection);
		try
		{
			var serializer = new DashboardDslSerializer();
			var controller = BuildController(db);
			var id = await CreateDashboardAsync(controller, SampleDslJson(serializer, "概览"));
			await controller.Publish(id, 0, CancellationToken.None);
			await controller.Update(id, new CreateDashboardRequest(0, SampleDslJson(serializer, "概览-2")));
			await controller.Publish(id, 0, CancellationToken.None);

			var verResult = await controller.Versions(id, 0, CancellationToken.None);
			var versions = Assert.IsType<List<DashboardVersionSummary>>(Assert.IsType<OkObjectResult>(verResult).Value);
			Assert.Equal(2, versions.Count);
			Assert.Equal(2, versions[0].Version);
			Assert.True(versions[0].IsCurrent);
			Assert.Equal(1, versions[1].Version);
			Assert.False(versions[1].IsCurrent);
		}
		finally
		{
			db.Dispose();
			connection.Dispose();
		}
	}

	[Fact]
	public async Task Publish_EmptyDraft_Returns_BadRequest()
	{
		var db = CreateContext(out var connection);
		try
		{
			var controller = BuildController(db);
			var id = await CreateDashboardAsync(controller, SampleDslJson(new DashboardDslSerializer(), "空草稿源"));
			// 直接把草稿置空，模拟“无内容可发布”
			var entity = await db.Dashboards.FirstAsync(d => d.Id == id);
			entity.DslJson = string.Empty;
			await db.SaveChangesAsync();

			var pubResult = await controller.Publish(id, 0, CancellationToken.None);
			Assert.IsType<BadRequestObjectResult>(pubResult);
		}
		finally
		{
			db.Dispose();
			connection.Dispose();
		}
	}

	[Fact]
	public async Task Rollback_UnknownVersion_Returns_NotFound()
	{
		var db = CreateContext(out var connection);
		try
		{
			var controller = BuildController(db);
			var id = await CreateDashboardAsync(controller, SampleDslJson(new DashboardDslSerializer(), "概览"));
			await controller.Publish(id, 0, CancellationToken.None);

			var rbResult = await controller.Rollback(id, 99, 0, CancellationToken.None);
			Assert.IsType<NotFoundObjectResult>(rbResult);
		}
		finally
		{
			db.Dispose();
			connection.Dispose();
		}
	}

	[Fact]
	public async Task Tenant_Scope_Isolates_Dashboard_And_Versions()
	{
		var db = CreateContext(out var connection);
		try
		{
			var serializer = new DashboardDslSerializer();
			var controller = BuildController(db);
			// 租户 7 创建并发布
			var id = await CreateDashboardAsync(controller, SampleDslJson(serializer, "租户7仪表盘"), tenantId: 7);
			await controller.Publish(id, 7, CancellationToken.None);

			// 租户 8 看不到租户 7 的仪表盘
			var getOther = await controller.GetById(id, tenantId: 8);
			Assert.IsType<NotFoundResult>(getOther);
			var verOther = await controller.Versions(id, tenantId: 8);
			Assert.IsType<NotFoundResult>(verOther);

			// 租户 7 自身可见
			var getSelf = await controller.GetById(id, tenantId: 7);
			Assert.IsType<OkObjectResult>(getSelf);
			var verSelf = await controller.Versions(id, tenantId: 7);
			Assert.IsType<OkObjectResult>(verSelf);
		}
		finally
		{
			db.Dispose();
			connection.Dispose();
		}
	}

	private sealed class FakeDashboardRenderer : IDashboardRenderer
	{
		public Task<DashboardRenderModel> RenderAsync(
			DashboardDsl dsl, PlatformContext context, CancellationToken cancellationToken = default) =>
			Task.FromResult(new DashboardRenderModel());
	}

	private sealed class FakeThemeResolver : IThemeResolver
	{
		public Task<ThemeContext> ResolveAsync(long tenantId, string? dashboardThemeKey = null, CancellationToken ct = default) =>
			Task.FromResult(ThemeContext.Default);
	}

	private sealed class FakePlatformContextAccessor : IPlatformContextAccessor
	{
		public SuperBuilder_AI.Models.Organization.PlatformContext? Current { get; set; }
	}
}
