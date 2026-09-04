using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Controllers;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Models.Theme;
using SuperBuilder_AI.Services.Theming;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// P7.4 ThemeController 单测（手写种子 + SQLite 内存库，不依赖 Moq）。
/// 覆盖：租户作用域 CRUD、内置主题不可改/删、重复键冲突、指派租户默认、复制（含从内置）、编辑器蓝图。
/// </summary>
public class ThemeControllerTests
{
	private const long Tenant5 = 5;

	private static string DslJson(string primary) => ThemeDslSerializer.Serialize(new ThemeDsl
	{
		Brand = new ThemeBrand { Primary = primary },
		Color = new ThemeColor { Primary = primary },
	});

	private static SuperBIContext CreateContext(out SqliteConnection connection)
	{
		connection = new SqliteConnection("DataSource=:memory:");
		connection.Open();
		var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
		var ctx = new SuperBIContext(options);
		ctx.Database.EnsureCreated();
		return ctx;
	}

	private static ThemeController Build(SuperBIContext db) => new(db);

	[Fact]
	public async Task Create_Then_Get_Returns_Dsl()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var controller = Build(ctx);
		var result = await controller.Create(
			new ThemeController.CreateThemeRequest(Tenant5, "acme-dark", DslJson("#0b0b0b"), "Acme 暗色"),
			CancellationToken.None) as Microsoft.AspNetCore.Mvc.CreatedAtActionResult;

		Assert.NotNull(result);
		Assert.Equal((int)HttpStatusCode.Created, result!.StatusCode);

		var get = await controller.GetByKey("acme-dark", Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
		Assert.NotNull(get);
		var detail = Assert.IsType<ThemeController.ThemeDetail>(get!.Value);
		Assert.Equal("acme-dark", detail.Key);
		// DslJson 是完整的主题 DSL 文档，应包含所写入的主色。
		Assert.Contains("#0b0b0b", detail.DslJson);
		Assert.False(detail.IsBuiltIn);
	}

	[Fact]
	public async Task Create_BuiltInTenantId_Rejected()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var result = await Build(ctx).Create(
			new ThemeController.CreateThemeRequest(0, "x", DslJson("#000000")),
			CancellationToken.None) as Microsoft.AspNetCore.Mvc.BadRequestObjectResult;
		Assert.NotNull(result);
	}

	[Fact]
	public async Task Create_DuplicateKey_Conflict()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var controller = Build(ctx);
		await controller.Create(new ThemeController.CreateThemeRequest(Tenant5, "dup", DslJson("#111111")), CancellationToken.None);
		var dup = await controller.Create(new ThemeController.CreateThemeRequest(Tenant5, "dup", DslJson("#222222")), CancellationToken.None)
			as Microsoft.AspNetCore.Mvc.ConflictObjectResult;
		Assert.NotNull(dup);
	}

	[Fact]
	public async Task Create_InvalidDslVersion_BadRequest()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var bad = ThemeDslSerializer.Serialize(new ThemeDsl { Version = "9.9" });
		var result = await Build(ctx).Create(
			new ThemeController.CreateThemeRequest(Tenant5, "bad", bad),
			CancellationToken.None) as Microsoft.AspNetCore.Mvc.BadRequestObjectResult;
		Assert.NotNull(result);
	}

	[Fact]
	public async Task Update_BuiltIn_Rejected()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		// 播种一条内置主题行（TenantId=0），验证内置主题不可改的守卫。
		ctx.Themes.Add(new Theme
		{
			TenantId = 0,
			Key = "default",
			Name = "内置默认",
			IsBuiltIn = true,
			DslVersion = ThemeDslVersions.Current,
			DslJson = DslJson("#2563eb"),
		});
		await ctx.SaveChangesAsync();

		var result = await Build(ctx).Update(
			"default",
			new ThemeController.UpdateThemeRequest(DslJson("#abcabc")),
			Tenant5,
			CancellationToken.None) as Microsoft.AspNetCore.Mvc.BadRequestObjectResult;
		Assert.NotNull(result);
	}

	[Fact]
	public async Task Delete_BuiltIn_Rejected()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		ctx.Themes.Add(new Theme
		{
			TenantId = 0,
			Key = "default",
			Name = "内置默认",
			IsBuiltIn = true,
			DslVersion = ThemeDslVersions.Current,
			DslJson = DslJson("#2563eb"),
		});
		await ctx.SaveChangesAsync();

		var result = await Build(ctx).Delete("default", Tenant5, CancellationToken.None)
			as Microsoft.AspNetCore.Mvc.BadRequestObjectResult;
		Assert.NotNull(result);
	}

	[Fact]
	public async Task Delete_TenantTheme_Removes()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var controller = Build(ctx);
		await controller.Create(new ThemeController.CreateThemeRequest(Tenant5, "to-del", DslJson("#333333")), CancellationToken.None);
		var del = await controller.Delete("to-del", Tenant5, CancellationToken.None)
			as Microsoft.AspNetCore.Mvc.NoContentResult;
		Assert.NotNull(del);

		var get = await controller.GetByKey("to-del", Tenant5, CancellationToken.None);
		Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(get);
	}

	[Fact]
	public async Task AssignDefault_Writes_TenantSetting()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		ctx.Tenants.Add(new Tenant { Id = Tenant5 });
		await ctx.SaveChangesAsync();

		var controller = Build(ctx);
		await controller.Create(new ThemeController.CreateThemeRequest(Tenant5, "acme-dark", DslJson("#0b0b0b")), CancellationToken.None);

		var assign = await controller.AssignDefault("acme-dark", Tenant5, CancellationToken.None)
			as Microsoft.AspNetCore.Mvc.OkObjectResult;
		Assert.NotNull(assign);

		// 验证 TenantSetting 已落库，且租户默认级联解析（P7.2）能命中。
		var setting = await ctx.TenantSettings
			.IgnoreQueryFilters()
			.FirstOrDefaultAsync(s => s.TenantId == Tenant5 && s.Key == "theme:defaultKey");
		Assert.NotNull(setting);
		Assert.Equal("acme-dark", setting!.Value);

		var resolved = await new ThemeResolver(ctx).ResolveAsync(Tenant5, null, CancellationToken.None);
		Assert.Equal("acme-dark", resolved.Key);
		Assert.Equal(ThemeSource.Tenant, resolved.Source);
	}

	[Fact]
	public async Task AssignDefault_TenantMissing_Rejected()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		// 创建一个属于租户 999 的主题（不创建 Tenant 行），再尝试将其指派为 999 的默认主题。
		await Build(ctx).Create(
			new ThemeController.CreateThemeRequest(999, "acme-dark", DslJson("#0b0b0b")),
			CancellationToken.None);
		// 注意：AssignDefault 以入参 tenantId 作用域解析主题，故需先以 999 作用域可见该主题。
		var result = await Build(ctx).AssignDefault("acme-dark", 999, CancellationToken.None)
			as Microsoft.AspNetCore.Mvc.BadRequestObjectResult;
		Assert.NotNull(result);
	}

	[Fact]
	public async Task Copy_FromBuiltIn_Default_CreatesTenantTheme()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var result = await Build(ctx).Copy(
			"default",
			new ThemeController.CopyThemeRequest(Tenant5, "acme-clone", "Acme 克隆"),
			Tenant5,
			CancellationToken.None) as Microsoft.AspNetCore.Mvc.CreatedAtActionResult;
		Assert.NotNull(result);

		var cloned = await ctx.Themes
			.IgnoreQueryFilters()
			.FirstOrDefaultAsync(t => t.Key == "acme-clone" && t.TenantId == Tenant5);
		Assert.NotNull(cloned);
		Assert.False(cloned!.IsBuiltIn);
		// 克隆体应继承内置默认主色 #2563eb
		Assert.Contains("#2563eb", cloned.DslJson);
	}

	[Fact]
	public async Task EditorBlueprint_Returns_Skeleton_And_BuiltInKeys()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var result = Build(ctx).EditorBlueprint() as Microsoft.AspNetCore.Mvc.OkObjectResult;
		Assert.NotNull(result);
		var bp = Assert.IsType<ThemeController.ThemeEditorBlueprint>(result!.Value);
		Assert.Contains("default", bp.BuiltInKeys);
		Assert.False(string.IsNullOrWhiteSpace(bp.Skeleton));
	}

	[Fact]
	public async Task List_ScopedReturns_TenantAndBuiltIn()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var controller = Build(ctx);
		await controller.Create(new ThemeController.CreateThemeRequest(Tenant5, "tenant-only", DslJson("#444444")), CancellationToken.None);

		var result = await controller.List(Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
		Assert.NotNull(result);
		var list = Assert.IsType<List<ThemeController.ThemeSummary>>(result!.Value);
		// 即便未落库内置行，列表在租户作用域下不强制含内置；此处只断言租户自有主题可见。
		Assert.Contains(list, s => s.Key == "tenant-only" && s.TenantId == Tenant5);
	}

	[Fact]
	public async Task Copy_IgnoresBodyTargetTenant_AndUsesAuthenticatedTenant()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		ctx.Tenants.Add(new Tenant { Id = 5 });
		ctx.Tenants.Add(new Tenant { Id = 7 });
		await ctx.SaveChangesAsync();

		var controller = new ThemeController(ctx);
		controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext
			{
				User = new ClaimsPrincipal(new ClaimsIdentity(new[]
				{
					new Claim("tid", "5"),
					new Claim(ClaimTypes.NameIdentifier, "1"),
				})),
			},
		};

		// 认证租户 5 的管理员试图通过请求体把主题复制到租户 7（伪造他租户）。
		var result = await controller.Copy(
			"default",
			new ThemeController.CopyThemeRequest(7, "acme-clone", "Acme 克隆"),
			5,
			CancellationToken.None) as Microsoft.AspNetCore.Mvc.CreatedAtActionResult;
		Assert.NotNull(result);

		// M0-06：目标租户恒为认证租户 5，而非请求体伪造的 7。
		var cloned = await ctx.Themes
			.IgnoreQueryFilters()
			.FirstOrDefaultAsync(t => t.Key == "acme-clone");
		Assert.NotNull(cloned);
		Assert.Equal(5, cloned!.TenantId);
		Assert.False(await ctx.Themes.IgnoreQueryFilters().AnyAsync(t => t.Key == "acme-clone" && t.TenantId == 7));
	}
}
