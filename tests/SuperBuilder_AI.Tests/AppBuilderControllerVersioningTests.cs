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
using static SuperBuilder_AI.Controllers.AppBuilderController;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.AppBuilder;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.AppBuilder;
using SuperBuilder_AI.Services.AppBuilder;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M7-02 App 草稿/发布隔离 + 可追踪回滚 的控制器级验证。
/// 使用 SQLite 内存库（EnsureCreated 按模型建表，含 AppVersions）+ 真实 DSL 序列化器，
/// 应用编排代理用轻量桩（仅把 AppDsl 映射为 AppPlan，不调 LLM）。
/// 不触碰 BI 查询链路，对 Golden 契约免疫。
/// </summary>
public sealed class AppBuilderControllerVersioningTests
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

	private static AppBuilderController BuildController(SuperBIContext db)
	{
		var controller = new AppBuilderController(
			db,
			new AppDslSerializer(),
			new FakeAppBuilderAgent(),
			new FakeAppQueryBindingExporter(),
			new FakeAppQueryExecutor(),
			new FakeDataSourceAuth())
		{
			ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
		};
		return controller;
	}

	/// <summary>测试桩：返回空授权数据源集合。</summary>
	private sealed class FakeDataSourceAuth : IDataSourceAuthorizationService
	{
		public Task<IReadOnlyList<long>> GetAuthorizedDataSourceIdsAsync(long tenantId, long userId, CancellationToken ct = default)
			=> Task.FromResult<IReadOnlyList<long>>(Array.Empty<long>());
		public Task<bool> IsAuthorizedAsync(long tenantId, long userId, long dataSourceId, CancellationToken ct = default)
			=> Task.FromResult(false);
		public Task GrantAsync(long tenantId, long dataSourceId, DataSourceGrantSubjectType subjectType, long subjectId, CancellationToken ct = default)
			=> Task.CompletedTask;
		public Task RevokeAsync(long tenantId, long dataSourceId, DataSourceGrantSubjectType subjectType, long subjectId, CancellationToken ct = default)
			=> Task.CompletedTask;
		public Task RevokeBySubjectAsync(long tenantId, DataSourceGrantSubjectType subjectType, long subjectId, CancellationToken ct = default)
			=> Task.CompletedTask;
		public Task<IReadOnlyList<DataSourceAccessGrant>> DetectOrphanGrantsAsync(long tenantId, CancellationToken ct = default)
			=> Task.FromResult<IReadOnlyList<DataSourceAccessGrant>>(Array.Empty<DataSourceAccessGrant>());
	}

	private static string SampleDslJson(AppDslSerializer serializer, string name, string code) =>
		serializer.Serialize(new AppDsl
		{
			Version = AppDslVersions.Current,
			Code = code,
			Name = name,
			Description = "应用描述-" + name,
			ThemeKey = null,
			Pages = new List<PagePlan>
			{
				new()
				{
					Id = "home",
					Name = "首页",
					Order = 1,
					Layout = new AppLayoutDsl { Kind = AppLayoutKinds.Grid, Columns = 12, RowHeight = 64, Gap = 12 },
					Components = new List<ComponentPlan>
					{
						new()
						{
							Type = AppComponentTypes.Kpi,
							Id = "kpi1",
							Title = "示例指标",
							Order = 1,
							Binding = new AppDataSourceBinding
							{
								Entity = "sales_order",
								Metrics = new List<AppMetricBinding> { new() { Field = "amount", Aggregation = AppAggregateTypes.Sum } },
								Dimensions = new List<string>(),
								Filters = new List<AppFilterBinding>(),
								Limit = null,
							},
							Properties = new Dictionary<string, string> { { "format", "N2" } },
							Style = new AppComponentStyle { Palette = "primary", ShowBorder = true, Padding = "normal" },
						}
					}
				}
			}
		});

	private static async Task<string> CreateAppAsync(AppBuilderController controller, string dslJson, long tenantId = 1)
	{
		var result = await controller.Create(new CreateAppRequest(tenantId, dslJson, "app-" + Guid.NewGuid().ToString("N")[..8]), CancellationToken.None);
		var created = Assert.IsType<CreatedAtActionResult>(result);
		var detail = Assert.IsType<AppDetail>(created.Value);
		return detail.Code;
	}

	[Fact]
	public async Task Publish_Creates_Version_Snapshot_And_Sets_PublishedState()
	{
		var db = CreateContext(out var connection);
		try
		{
			var serializer = new AppDslSerializer();
			var controller = BuildController(db);
			var dsl = SampleDslJson(serializer, "销售应用", "sales-app");
			var code = await CreateAppAsync(controller, dsl);

			var pubResult = await controller.Publish(code, tenantId: 1, cancellationToken: CancellationToken.None);
			var pub = Assert.IsType<AppBuilderController.PublishResult>(Assert.IsType<OkObjectResult>(pubResult).Value);
			Assert.Equal(1, pub.Version);

			var entity = await db.AppPlans.AsNoTracking().FirstAsync(p => p.Code == code);
			Assert.Equal(AppStatuses.Published, entity.Status);
			Assert.Equal(1, entity.PublishedVersion);
			Assert.NotNull(entity.PublishedAt);
			Assert.Equal(dsl, entity.PublishedDslJson);
			Assert.Equal(dsl, entity.DslJson); // 草稿 == 发布态（首次发布尚无差异）

			var versions = await db.AppVersions.AsNoTracking().Where(v => v.AppId == entity.Id).ToListAsync();
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
			var serializer = new AppDslSerializer();
			var controller = BuildController(db);
			var dslA = SampleDslJson(serializer, "应用A", "app-a");
			var code = await CreateAppAsync(controller, dslA);
			await controller.Publish(code, tenantId: 1, cancellationToken: CancellationToken.None);

			var dslB = SampleDslJson(serializer, "应用A-修订", "app-a");
			await controller.Update(code, new UpdateAppRequest(dslB), tenantId: 1, cancellationToken: CancellationToken.None);

			var entity = await db.AppPlans.AsNoTracking().FirstAsync(p => p.Code == code);
			Assert.Equal(dslB, entity.DslJson);              // 草稿已更新
			Assert.Equal(dslA, entity.PublishedDslJson);     // 发布态不变（不直接覆盖线上版本，M7-02 验收）
			Assert.Equal(1, entity.PublishedVersion);        // 版本未变
			Assert.Equal(AppStatuses.Published, entity.Status);
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
			var serializer = new AppDslSerializer();
			var controller = BuildController(db);
			var dslA = SampleDslJson(serializer, "应用A", "app-rl");
			var dslB = SampleDslJson(serializer, "应用B", "app-rl");
			var code = await CreateAppAsync(controller, dslA);
			await controller.Publish(code, tenantId: 1, cancellationToken: CancellationToken.None);          // v1 = A
			await controller.Update(code, new UpdateAppRequest(dslB), tenantId: 1, cancellationToken: CancellationToken.None);
			await controller.Publish(code, tenantId: 1, cancellationToken: CancellationToken.None);          // v2 = B

			var rbResult = await controller.Rollback(code, 1, 1, CancellationToken.None);
			var rb = Assert.IsType<AppBuilderController.PublishResult>(Assert.IsType<OkObjectResult>(rbResult).Value);
			Assert.Equal(3, rb.Version);                 // 回滚固化为 v3
			Assert.Equal(1, rb.RolledBackFromVersion);   // 来源 v1

			var entity = await db.AppPlans.AsNoTracking().FirstAsync(p => p.Code == code);
			Assert.Equal(3, entity.PublishedVersion);
			Assert.Equal(dslA, entity.PublishedDslJson); // 恢复到 A

			var versions = await db.AppVersions.AsNoTracking()
				.Where(v => v.AppId == entity.Id).OrderBy(v => v.Version).ToListAsync();
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
			var serializer = new AppDslSerializer();
			var controller = BuildController(db);
			var code = await CreateAppAsync(controller, SampleDslJson(serializer, "应用", "app-v"));
			await controller.Publish(code, tenantId: 1, cancellationToken: CancellationToken.None);
			await controller.Update(code, new UpdateAppRequest(SampleDslJson(serializer, "应用-2", "app-v")));
			await controller.Publish(code, tenantId: 1, cancellationToken: CancellationToken.None);

			var verResult = await controller.Versions(code, 1, CancellationToken.None);
			var versions = Assert.IsType<AppVersionListResult>(Assert.IsType<OkObjectResult>(verResult).Value);
			Assert.Equal(2, versions.Items.Count);
			Assert.Equal(2, versions.Total);
			Assert.Equal(2, versions.Items[0].Version);
			Assert.True(versions.Items[0].IsCurrent);
			Assert.Equal(1, versions.Items[1].Version);
			Assert.False(versions.Items[1].IsCurrent);
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
			var serializer = new AppDslSerializer();
			var controller = BuildController(db);
			var code = await CreateAppAsync(controller, SampleDslJson(serializer, "空草稿源", "app-empty"));
			// 直接把草稿置空，模拟“无内容可发布”
			var entity = await db.AppPlans.FirstAsync(p => p.Code == code);
			entity.DslJson = string.Empty;
			await db.SaveChangesAsync();

			var pubResult = await controller.Publish(code, tenantId: 1, cancellationToken: CancellationToken.None);
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
			var serializer = new AppDslSerializer();
			var controller = BuildController(db);
			var code = await CreateAppAsync(controller, SampleDslJson(serializer, "应用", "app-rb"));
			await controller.Publish(code, tenantId: 1, cancellationToken: CancellationToken.None);

			var rbResult = await controller.Rollback(code, 99, 1, CancellationToken.None);
			Assert.IsType<NotFoundObjectResult>(rbResult);
		}
		finally
		{
			db.Dispose();
			connection.Dispose();
		}
	}

	[Fact]
	public async Task Tenant_Scope_Isolates_App_And_Versions()
	{
		var db = CreateContext(out var connection);
		try
		{
			var serializer = new AppDslSerializer();
			var controller = BuildController(db);
			// 租户 7 创建并发布
			var code = await CreateAppAsync(controller, SampleDslJson(serializer, "租户7应用", "app-t7"), tenantId: 7);
			await controller.Publish(code, tenantId: 7, cancellationToken: CancellationToken.None);

			// 租户 8 看不到租户 7 的应用（结构化 404 ApiError）。
			var getOther = await controller.GetByCode(code, tenantId: 8) as ObjectResult;
			Assert.NotNull(getOther);
			Assert.Equal(404, getOther!.StatusCode);
			var verOther = await controller.Versions(code, tenantId: 8) as ObjectResult;
			Assert.NotNull(verOther);
			Assert.Equal(404, verOther!.StatusCode);

			// 租户 7 自身可见
			var getSelf = await controller.GetByCode(code, tenantId: 7);
			Assert.IsType<OkObjectResult>(getSelf);
			var verSelf = await controller.Versions(code, tenantId: 7);
			Assert.IsType<OkObjectResult>(verSelf);
		}
		finally
		{
			db.Dispose();
			connection.Dispose();
		}
	}

	private sealed class FakeAppBuilderAgent : IAppBuilderAgent
	{
		public Task<AppBuildResult> BuildFromDslAsync(long tenantId, AppDsl dsl, string? code = null)
		{
			var dslJson = new AppDslSerializer().Serialize(dsl);
			var plan = new AppPlan
			{
				TenantId = tenantId,
				Code = !string.IsNullOrWhiteSpace(code)
					? code
					: (!string.IsNullOrWhiteSpace(dsl.Code) ? dsl.Code : Slugify(dsl.Name)),
				Name = dsl.Name,
				Description = dsl.Description,
				Status = AppStatuses.Draft,
				DslVersion = dsl.Version,
				DslJson = dslJson,
				ThemeKey = dsl.ThemeKey,
			};
			return Task.FromResult(AppBuildResult.Ok(plan, dslJson));
		}

		public Task<AppBuildResult> GenerateFromDescriptionAsync(long tenantId, string description, string? code = null)
			=> Task.FromResult(AppBuildResult.Fail(new[] { "测试桩不启用 LLM 生成路径。" }));

		private static string Slugify(string name)
		{
			var src = string.IsNullOrWhiteSpace(name) ? "app" : name;
			var sb = new System.Text.StringBuilder(src.Length);
			foreach (var ch in src.ToLowerInvariant())
			{
				if (char.IsLetterOrDigit(ch))
					sb.Append(ch);
				else if (char.IsWhiteSpace(ch) || ch is '-' or '_')
				{
					if (sb.Length > 0 && sb[^1] != '-')
						sb.Append('-');
				}
			}

			var slug = sb.ToString().Trim('-');
			return string.IsNullOrEmpty(slug) ? "app" : slug;
		}
	}
}
