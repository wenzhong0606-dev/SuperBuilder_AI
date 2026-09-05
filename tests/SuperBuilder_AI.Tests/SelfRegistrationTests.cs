using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SuperBuilder_AI.Application.Common.Options;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Audit;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Localization;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.Localization;
using SuperBuilder_AI.Services.Auth;
using SuperBuilder_AI.Services.Identity;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M2-06 租户自助注册（待裁决 / 默认关闭骨架）：关闭开关与待裁决特性被拒绝，
/// 开启开关时原子创建「租户 + 租户设置 + 首位租户管理员 + 口令」并重签令牌。
/// SQLite 内存库 + 真实 DbContext；Identity 走真实实现以验证原子创建。
/// </summary>
public sealed class SelfRegistrationTests
{
	private static SuperBIContext CreateContext(out SqliteConnection connection)
	{
		connection = new SqliteConnection("DataSource=:memory:");
		connection.Open();
		var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
		var ctx = new SuperBIContext(options);
		ctx.Database.EnsureCreated();
		// M3-01：平台语言目录（自助注册会按文化映射到 UiLanguageId）
		ctx.UiLanguages.AddRange(
			new UiLanguage { Id = 1, Culture = "zh-CN", DisplayName = "中文", NativeName = "简体中文", Enabled = true, SortOrder = 0 },
			new UiLanguage { Id = 2, Culture = "en-US", DisplayName = "English", NativeName = "English", Enabled = true, SortOrder = 1 });
		ctx.SaveChanges();
		return ctx;
	}

	private static SelfRegistrationService BuildEnabled(SuperBIContext ctx, bool enabled, bool approval = false, bool captcha = false)
	{
		var identity = new IdentityService(ctx, new PasswordHasher());
		identity.SeedAsync().GetAwaiter().GetResult();
		var options = new SelfRegistrationOptions
		{
			Enabled = enabled,
			ApprovalRequired = approval,
			RequireCaptcha = captcha,
			DefaultCulture = "zh-CN",
			DefaultAvailableCultures = new[] { "zh-CN" },
		};
		return new SelfRegistrationService(ctx, identity, new NoopTokenService(), new NoopAuditService(), Options.Create(options), new TenantLanguageService(ctx, new NoopAuditService()));
	}

	[Fact]
	public async Task Register_WhenDisabled_ReturnsDisabled_NoDataCreated()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var svc = BuildEnabled(ctx, enabled: false);
		var result = await svc.RegisterAsync(
			new SelfRegistrationRequest("acme", "Acme Inc", "admin", "admin@acme.com", "password1"),
			CancellationToken.None);

		Assert.False(result.Success);
		Assert.Equal("disabled", result.Status);
		Assert.Equal(0, await ctx.Tenants.CountAsync(t => t.TenantCode == "acme"));
		Assert.Equal(0, await ctx.Users.CountAsync(u => u.Username == "admin"));
	}

	[Fact]
	public async Task Register_WhenApprovalRequired_ReturnsFeatureNotImplemented()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var svc = BuildEnabled(ctx, enabled: true, approval: true);
		var result = await svc.RegisterAsync(
			new SelfRegistrationRequest("acme", "Acme Inc", "admin", "admin@acme.com", "password1"),
			CancellationToken.None);

		Assert.False(result.Success);
		Assert.Equal("feature_not_implemented", result.Status);
		Assert.Equal(0, await ctx.Tenants.CountAsync(t => t.TenantCode == "acme"));
	}

	[Fact]
	public async Task Register_WhenEnabled_CreatesTenantAdminAndSettingsAtomically()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var svc = BuildEnabled(ctx, enabled: true);
		var result = await svc.RegisterAsync(
			new SelfRegistrationRequest("acme", "Acme Inc", "admin", "admin@acme.com", "password1"),
			CancellationToken.None);

		Assert.True(result.Success);
		Assert.Equal("ok", result.Status);
		Assert.False(string.IsNullOrEmpty(result.Token));

		var tenant = await ctx.Tenants.SingleAsync(t => t.TenantCode == "acme");
		Assert.True(tenant.Enabled);

		// M3-01：语言授权以关系模型 TenantUiLanguage 落库（默认 zh-CN），不再写 localization:* JSON。
		var langRows = await ctx.TenantUiLanguages.Where(x => x.TenantId == tenant.Id).ToListAsync();
		Assert.Single(langRows);
		Assert.True(langRows[0].IsDefault);
		Assert.Equal("zh-CN", (await ctx.UiLanguages.FindAsync(langRows[0].UiLanguageId))!.Culture);
		Assert.Equal(0, await ctx.TenantSettings.CountAsync(s => s.TenantId == tenant.Id));

		var admin = await ctx.Users.SingleAsync(u => u.TenantId == tenant.Id && u.Username == "admin");
		Assert.StartsWith("pbkdf2:", admin.PasswordHash);
		Assert.False(string.IsNullOrEmpty(admin.SecurityStamp));
		Assert.True(await ctx.UserRoles.AnyAsync(r => r.UserId == admin.Id));
	}

	[Fact]
	public async Task Register_ConflictingTenantCode_ReturnsConflict()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		ctx.Tenants.Add(new Tenant { TenantCode = "acme", TenantName = "Existing", Enabled = true });
		await ctx.SaveChangesAsync();

		var svc = BuildEnabled(ctx, enabled: true);
		var result = await svc.RegisterAsync(
			new SelfRegistrationRequest("acme", "Acme Inc", "admin", "admin@acme.com", "password1"),
			CancellationToken.None);

		Assert.False(result.Success);
		Assert.Equal("conflict", result.Status);
	}
}
