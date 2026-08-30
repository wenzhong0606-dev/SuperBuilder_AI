using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Theme;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Models.Theme;
using SuperBuilder_AI.Services.Theming;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// P7.2 ThemeResolver 单测（手写种子 + SQLite 内存库，不依赖 Moq）。
/// 覆盖：仪表盘显式键 → 租户默认 → 内置默认 的级联优先级，以及跨租户隔离（不泄漏其他租户主题）。
/// </summary>
public class ThemeResolverTests
{
	private const long Tenant5 = 5;
	private const long Tenant9 = 9;

	private static Theme MakeTheme(long tenantId, string key, string primaryColor)
	{
		var dsl = new ThemeDsl
		{
			Brand = new ThemeBrand { Primary = primaryColor },
			Color = new ThemeColor { Primary = primaryColor },
		};
		return new Theme
		{
			TenantId = tenantId,
			Key = key,
			Name = key,
			IsBuiltIn = tenantId == 0,
			DslVersion = ThemeDslVersions.Current,
			DslJson = JsonSerializer.Serialize(dsl),
		};
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

	[Fact]
	public async Task Resolve_ExplicitDashboardKey_BuiltIn_Found_AsDashboardSource()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		ctx.Themes.Add(MakeTheme(0, "default", "#222222"));
		await ctx.SaveChangesAsync();

		var resolver = new ThemeResolver(ctx);
		var result = await resolver.ResolveAsync(Tenant5, "default", CancellationToken.None);

		Assert.Equal("default", result.Key);
		Assert.Equal(ThemeSource.Dashboard, result.Source);
		Assert.Equal("#222222", result.Dsl.Color.Primary);
	}

	[Fact]
	public async Task Resolve_ExplicitDashboardKey_TenantTheme_Found_AsDashboardSource()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		ctx.Themes.Add(MakeTheme(0, "default", "#2563eb"));
		ctx.Themes.Add(MakeTheme(Tenant5, "acme-dark", "#0b0b0b"));
		await ctx.SaveChangesAsync();

		var resolver = new ThemeResolver(ctx);
		var result = await resolver.ResolveAsync(Tenant5, "acme-dark", CancellationToken.None);

		Assert.Equal("acme-dark", result.Key);
		Assert.Equal(ThemeSource.Dashboard, result.Source);
		Assert.Equal("#0b0b0b", result.Dsl.Color.Primary);
	}

	[Fact]
	public async Task Resolve_NoKey_TenantSetting_FallsToTenantDefault()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		ctx.Themes.Add(MakeTheme(0, "default", "#2563eb"));
		ctx.Themes.Add(MakeTheme(Tenant5, "acme-dark", "#0b0b0b"));
		ctx.Tenants.Add(new Tenant { Id = Tenant5 });
		ctx.TenantSettings.Add(new TenantSetting
		{
			TenantId = Tenant5,
			Key = "theme:defaultKey",
			Value = "acme-dark",
			DataType = "string",
		});
		await ctx.SaveChangesAsync();

		var resolver = new ThemeResolver(ctx);
		var result = await resolver.ResolveAsync(Tenant5, null, CancellationToken.None);

		Assert.Equal("acme-dark", result.Key);
		Assert.Equal(ThemeSource.Tenant, result.Source);
		Assert.Equal("#0b0b0b", result.Dsl.Color.Primary);
	}

	[Fact]
	public async Task Resolve_NoKey_NoSetting_FallsToBuiltIn()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		ctx.Themes.Add(MakeTheme(0, "default", "#2563eb"));
		await ctx.SaveChangesAsync();

		var resolver = new ThemeResolver(ctx);
		var result = await resolver.ResolveAsync(Tenant5, null, CancellationToken.None);

		Assert.Equal(ThemeSource.BuiltIn, result.Source);
		Assert.Equal("default", result.Key);
		Assert.Equal("#2563eb", result.Dsl.Color.Primary);
	}

	[Fact]
	public async Task Resolve_TenantIdZero_AlwaysBuiltIn()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		ctx.Themes.Add(MakeTheme(0, "default", "#2563eb"));
		ctx.Themes.Add(MakeTheme(Tenant5, "acme-dark", "#0b0b0b"));
		await ctx.SaveChangesAsync();

		var resolver = new ThemeResolver(ctx);
		// 即便给定其他租户的主题键，系统/全局(tenantId=0) 也应直接取内置默认
		var result = await resolver.ResolveAsync(0, "acme-dark", CancellationToken.None);

		Assert.Equal(ThemeSource.BuiltIn, result.Source);
		Assert.Equal("default", result.Key);
	}

	[Fact]
	public async Task Resolve_ExplicitKey_OnlyForeignTenant_NotLeaked_FallsToBuiltIn()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		ctx.Themes.Add(MakeTheme(0, "default", "#2563eb"));
		ctx.Themes.Add(MakeTheme(Tenant5, "acme-dark", "#0b0b0b")); // 属于 Tenant5
		await ctx.SaveChangesAsync();

		var resolver = new ThemeResolver(ctx);
		// Tenant9 没有自己的 acme-dark，也不应拿到 Tenant5 的 → 兜底内置默认
		var result = await resolver.ResolveAsync(Tenant9, "acme-dark", CancellationToken.None);

		Assert.Equal(ThemeSource.BuiltIn, result.Source);
		Assert.Equal("default", result.Key);
		Assert.Equal("#2563eb", result.Dsl.Color.Primary);
	}
}
