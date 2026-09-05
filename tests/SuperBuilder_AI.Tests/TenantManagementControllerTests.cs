using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Controllers;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Services.Identity;
using SuperBuilder_AI.Services.Auth;
using SuperBuilder_AI.Models.Organization;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M1-02 TenantManagementController 字段完整性：编码规范化/唯一性、停用治理、TenantSetting 策略。
/// SQLite 内存库 + 真实 DbContext，IIdentityService 用成功桩避开账号创建副作用。
/// </summary>
public sealed class TenantManagementControllerTests
{
	private sealed class SuccessIdentityService : IIdentityService
	{
		public Task SeedAsync(CancellationToken ct = default) => Task.CompletedTask;
		public Task<IdentityResult> CreateUserAsync(long tenantId, string username, string displayName, string email, string[]? roleCodes, CancellationToken ct = default)
			=> Task.FromResult(IdentityResult.Ok(1L));
		public Task<IdentityResult> AssignRoleAsync(long tenantId, long userId, string roleCode, CancellationToken ct = default)
			=> Task.FromResult(IdentityResult.Ok(userId));
		public Task<IdentityResult> RevokeRoleAsync(long tenantId, long userId, string roleCode, CancellationToken ct = default)
			=> Task.FromResult(IdentityResult.Ok(userId));
		public Task<IdentityResult> SetPasswordAsync(long tenantId, long userId, string password, CancellationToken ct = default)
			=> Task.FromResult(IdentityResult.Ok(userId));
		public Task<IdentityResult> SetUserStatusAsync(long tenantId, long userId, UserStatus newStatus, CancellationToken ct = default)
			=> Task.FromResult(IdentityResult.Ok(userId));
		public Task<IReadOnlyList<string>> GetPermissionsAsync(long tenantId, long userId, CancellationToken ct = default)
			=> Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
	public Task<bool> HasPermissionAsync(long tenantId, long userId, string permissionCode, CancellationToken ct = default)
		=> Task.FromResult(false);
	}

	/// <summary>
	/// M2-04 回滚证据桩：CreateUserAsync 始终失败，用于验证控制器在管理员创建失败时整体回滚租户创建事务。
	/// </summary>
	private sealed class FailingIdentityService : IIdentityService
	{
		public Task SeedAsync(CancellationToken ct = default) => Task.CompletedTask;
		public Task<IdentityResult> CreateUserAsync(long tenantId, string username, string displayName, string email, string[]? roleCodes, CancellationToken ct = default)
			=> Task.FromResult(IdentityResult.Fail("simulated admin creation failure"));
		public Task<IdentityResult> AssignRoleAsync(long tenantId, long userId, string roleCode, CancellationToken ct = default)
			=> Task.FromResult(IdentityResult.Ok(userId));
		public Task<IdentityResult> RevokeRoleAsync(long tenantId, long userId, string roleCode, CancellationToken ct = default)
			=> Task.FromResult(IdentityResult.Ok(userId));
		public Task<IdentityResult> SetPasswordAsync(long tenantId, long userId, string password, CancellationToken ct = default)
			=> Task.FromResult(IdentityResult.Ok(userId));
		public Task<IdentityResult> SetUserStatusAsync(long tenantId, long userId, UserStatus newStatus, CancellationToken ct = default)
			=> Task.FromResult(IdentityResult.Ok(userId));
		public Task<IReadOnlyList<string>> GetPermissionsAsync(long tenantId, long userId, CancellationToken ct = default)
			=> Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
		public Task<bool> HasPermissionAsync(long tenantId, long userId, string permissionCode, CancellationToken ct = default)
			=> Task.FromResult(false);
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

	private static TenantManagementController Build(SuperBIContext db, ClaimsPrincipal? user = null, IPlatformAdminScopeService? scope = null)
	{
		var ctrl = new TenantManagementController(db, new SuccessIdentityService(), scope ?? new SuccessScopeService());
		var principal = user ?? new ClaimsPrincipal(new ClaimsIdentity(new[]
		{
			new Claim("perm", IdentityPermissions.PlatformTenantManage),
			new Claim(ClaimTypes.NameIdentifier, "99")
		}, "test"));
		ctrl.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } };
		return ctrl;
	}

	private static TenantManagementController Build(SuperBIContext db, IIdentityService identity, ClaimsPrincipal? user = null, IPlatformAdminScopeService? scope = null)
	{
		var ctrl = new TenantManagementController(db, identity, scope ?? new SuccessScopeService());
		var principal = user ?? new ClaimsPrincipal(new ClaimsIdentity(new[]
		{
			new Claim("perm", IdentityPermissions.PlatformTenantManage),
			new Claim(ClaimTypes.NameIdentifier, "99")
		}, "test"));
		ctrl.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } };
		return ctrl;
	}

	[Fact]
	public void NormalizeCode_TrimsAndLowercases()
	{
		Assert.Equal("abc", Tenant.NormalizeCode(" AbC "));
		Assert.Equal("abc", Tenant.NormalizeCode("ABC"));
		Assert.Equal("", Tenant.NormalizeCode(null));
	}

	[Fact]
	public async Task Create_NormalizesTenantCodeToLowercase()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		var ctrl = Build(ctx);

		var result = await ctrl.Create(
			new CreateTenantRequest(" AbC ", "Foo Corp", "admin", "password1"), CancellationToken.None);
		Assert.IsType<OkObjectResult>(result);

		var tenant = await ctx.Tenants.AsNoTracking().SingleAsync(t => t.TenantName == "Foo Corp");
		Assert.Equal("abc", tenant.TenantCode);
	}

	[Fact]
	public async Task Create_DuplicateCodeCaseInsensitive_Conflict()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		var ctrl = Build(ctx);

		Assert.IsType<OkObjectResult>(await ctrl.Create(
			new CreateTenantRequest("AbC", "Foo Corp", "admin", "password1"), CancellationToken.None));
		var dup = await ctrl.Create(
			new CreateTenantRequest("abc", "Bar Corp", "admin2", "password1"), CancellationToken.None);
		Assert.IsType<ConflictObjectResult>(dup);
	}

	[Fact]
	public async Task Create_CodeExceedingMaxLength_BadRequest()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		var ctrl = Build(ctx);
		var tooLong = new string('x', Tenant.MaxCodeLength + 1);
		var result = await ctrl.Create(
			new CreateTenantRequest(tooLong, "Foo Corp", "admin", "password1"), CancellationToken.None);
		Assert.IsType<BadRequestObjectResult>(result);
	}

	[Fact]
	public async Task Disable_RecordsReasonOperatorAndTime()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		ctx.Tenants.Add(new Tenant { Id = 1, TenantCode = "t1", TenantName = "T1", Enabled = true });
		await ctx.SaveChangesAsync();
		var ctrl = Build(ctx);

		var result = await ctrl.Disable(1, new SetEnabledRequest("合规下线"), CancellationToken.None);
		Assert.IsType<OkObjectResult>(result);

		var reloaded = await ctx.Tenants.FindAsync(1L);
		Assert.False(reloaded!.Enabled);
		Assert.Equal("合规下线", reloaded.DisabledReason);
		Assert.Equal(99L, reloaded.DisabledByUserId);
		Assert.NotNull(reloaded.DisabledAt);
		Assert.Equal(DateTimeKind.Utc, reloaded.DisabledAt!.Value.Kind);
	}

	[Fact]
	public async Task Enable_ClearsDisabledFields()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		ctx.Tenants.Add(new Tenant { Id = 1, TenantCode = "t1", TenantName = "T1", Enabled = false,
			DisabledReason = "x", DisabledAt = DateTime.UtcNow, DisabledByUserId = 5 });
		await ctx.SaveChangesAsync();
		var ctrl = Build(ctx);

		var result = await ctrl.Enable(1, null, CancellationToken.None);
		Assert.IsType<OkObjectResult>(result);

		var reloaded = await ctx.Tenants.FindAsync(1L);
		Assert.True(reloaded!.Enabled);
		Assert.Null(reloaded.DisabledReason);
		Assert.Null(reloaded.DisabledAt);
		Assert.Null(reloaded.DisabledByUserId);
	}

	[Fact]
	public async Task UpsertSetting_LockedKey_ReturnsConflict()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		ctx.Tenants.Add(new Tenant { Id = 1, TenantCode = "t1", TenantName = "T1", Enabled = true });
		await ctx.SaveChangesAsync();
		var ctrl = Build(ctx);

		var result = await ctrl.UpsertSetting(1,
			new UpsertTenantSettingRequest("localization:defaultCulture", "zh-CN", "string"), CancellationToken.None);
		var obj = Assert.IsType<ObjectResult>(result);
		Assert.Equal(StatusCodes.Status409Conflict, obj.StatusCode);
	}

	[Fact]
	public async Task UpsertSetting_UnknownPrefix_ReturnsBadRequest()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		ctx.Tenants.Add(new Tenant { Id = 1, TenantCode = "t1", TenantName = "T1", Enabled = true });
		await ctx.SaveChangesAsync();
		var ctrl = Build(ctx);

		var result = await ctrl.UpsertSetting(1,
			new UpsertTenantSettingRequest("evil:payload", "x", "string"), CancellationToken.None);
		Assert.IsType<BadRequestObjectResult>(result);
	}

	[Fact]
	public async Task UpsertSetting_ValidInt_Writes()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		ctx.Tenants.Add(new Tenant { Id = 1, TenantCode = "t1", TenantName = "T1", Enabled = true });
		await ctx.SaveChangesAsync();
		var ctrl = Build(ctx);

		var result = await ctrl.UpsertSetting(1,
			new UpsertTenantSettingRequest("workspace:limit", "42", "int"), CancellationToken.None);
		Assert.IsType<OkObjectResult>(result);

		var setting = await ctx.TenantSettings.SingleAsync(s => s.TenantId == 1 && s.Key == "workspace:limit");
		Assert.Equal("42", setting.Value);
		Assert.Equal("int", setting.DataType);
	}

	[Fact]
	public async Task UpsertSetting_InvalidIntValue_ReturnsBadRequest()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		ctx.Tenants.Add(new Tenant { Id = 1, TenantCode = "t1", TenantName = "T1", Enabled = true });
		await ctx.SaveChangesAsync();
		var ctrl = Build(ctx);

		var result = await ctrl.UpsertSetting(1,
			new UpsertTenantSettingRequest("workspace:limit", "abc", "int"), CancellationToken.None);
		Assert.IsType<BadRequestObjectResult>(result);
	}

	// === M2-02 租户范围强制校验 ===
	[Fact]
	public async Task Disable_OutOfScopeTenant_Returns403()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		ctx.Tenants.Add(new Tenant { Id = 1, TenantCode = "t1", TenantName = "T1", Enabled = true });
		ctx.Tenants.Add(new Tenant { Id = 2, TenantCode = "t2", TenantName = "T2", Enabled = true });
		await ctx.SaveChangesAsync();
		// 管理员 99 仅被授权管理租户 1，租户 2 超出授权范围
		var scope = new ScopedScopeService(new[] { 1L });
		var ctrl = Build(ctx, scope: scope);

		var result = await ctrl.Disable(2, new SetEnabledRequest("越权下线"), CancellationToken.None);
		var obj = Assert.IsType<ObjectResult>(result);
		Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
	}

	[Fact]
	public async Task Disable_InScopeTenant_Succeeds()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		ctx.Tenants.Add(new Tenant { Id = 1, TenantCode = "t1", TenantName = "T1", Enabled = true });
		await ctx.SaveChangesAsync();
		var scope = new ScopedScopeService(new[] { 1L });
		var ctrl = Build(ctx, scope: scope);

		var result = await ctrl.Disable(1, new SetEnabledRequest("合规下线"), CancellationToken.None);
		Assert.IsType<OkObjectResult>(result);
	}

	[Fact]
	public async Task List_FullScopeAdmin_SeesAllTenants()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		ctx.Tenants.Add(new Tenant { Id = 1, TenantCode = "t1", TenantName = "T1", Enabled = true });
		ctx.Tenants.Add(new Tenant { Id = 2, TenantCode = "t2", TenantName = "T2", Enabled = true });
		await ctx.SaveChangesAsync();
		// 默认范围服务：无范围记录 = 全部租户
		var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
		{
			new Claim("perm", IdentityPermissions.PlatformTenantView),
			new Claim("perm", IdentityPermissions.PlatformTenantManage),
			new Claim(ClaimTypes.NameIdentifier, "99")
		}, "test"));
		var ctrl = Build(ctx, user: user);

		var result = await ctrl.List(CancellationToken.None);
		var ok = Assert.IsType<OkObjectResult>(result);
		var list = Assert.IsAssignableFrom<System.Collections.IEnumerable>(ok.Value!);
		Assert.Equal(2, list.Cast<object>().Count());
	}

	// === M2-04 租户创建事务与生命周期原子性 ===
	[Fact]
	public async Task Create_WhenAdminCreationFails_RollsBackEntireTenantCreation()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		var ctrl = Build(ctx, new FailingIdentityService());

		var result = await ctrl.Create(
			new CreateTenantRequest("acme", "Acme Inc", "admin", "password1"), CancellationToken.None);
		// 管理员创建失败 → 返回冲突，且事务整体回滚。
		Assert.IsType<ConflictObjectResult>(result);

		// 回滚后：租户与其默认设置均未落库（事务原子性：tenant + settings 全有或全无）。
		Assert.Equal(0, await ctx.Tenants.CountAsync());
		Assert.Equal(0, await ctx.TenantSettings.CountAsync());
	}

	[Fact]
	public async Task Create_Success_CreatesTenantAdminUserAndSettingsAtomically()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		// 真实身份服务参与同一事务：管理员用户/角色/口令/安全戳一并写入。
		var identity = new IdentityService(ctx, new PasswordHasher());
		await identity.SeedAsync(); // 注入平台租户与全局角色目录（含 TenantAdmin）
		var ctrl = Build(ctx, identity);

		var result = await ctrl.Create(
			new CreateTenantRequest("acme", "Acme Inc", "admin", "password1"), CancellationToken.None);
		Assert.IsType<OkObjectResult>(result);

		var tenant = await ctx.Tenants.SingleAsync(t => t.TenantCode == "acme");
		// 两位默认本地化设置（availableCultures / defaultCulture）随租户一并提交。
		Assert.Equal(2, await ctx.TenantSettings.CountAsync(s => s.TenantId == tenant.Id));
		// 首位管理员用户已创建并赋 TenantAdmin 角色、已设置口令（安全戳已生成）。
		var admin = await ctx.Users.SingleAsync(u => u.TenantId == tenant.Id);
		Assert.Equal("admin", admin.Username);
		Assert.False(string.IsNullOrEmpty(admin.PasswordHash));
		Assert.StartsWith("pbkdf2:", admin.PasswordHash);
		Assert.False(string.IsNullOrEmpty(admin.SecurityStamp));
		var tenantAdminRoleId = (await ctx.Roles.SingleAsync(r => r.Code == IdentityRoles.TenantAdmin)).Id;
		Assert.True(await ctx.UserRoles.AnyAsync(ur => ur.UserId == admin.Id && ur.RoleId == tenantAdminRoleId));
	}
}

internal sealed class SuccessScopeService : IPlatformAdminScopeService
{
	public Task<bool> HasFullScopeAsync(long adminUserId, CancellationToken ct = default) => Task.FromResult(true);
	public Task<bool> CanManageAsync(long adminUserId, long targetTenantId, CancellationToken ct = default) => Task.FromResult(true);
	public Task<IReadOnlyList<long>> GetScopedTenantIdsAsync(long adminUserId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<long>>(Array.Empty<long>());
	public Task<PlatformAdminScopeView> GetScopeAsync(long adminUserId, CancellationToken ct = default) => Task.FromResult(new PlatformAdminScopeView(true, Array.Empty<TenantScopeItem>()));
	public Task<PlatformAdminScopeView> SetScopeAsync(long adminUserId, IReadOnlyList<long> tenantIds, string actor, CancellationToken ct = default) => Task.FromResult(new PlatformAdminScopeView(true, Array.Empty<TenantScopeItem>()));
}

internal sealed class ScopedScopeService : IPlatformAdminScopeService
{
	private readonly HashSet<long> _allowed;
	public ScopedScopeService(IEnumerable<long> allowed) => _allowed = allowed.ToHashSet();
	public Task<bool> HasFullScopeAsync(long adminUserId, CancellationToken ct = default) => Task.FromResult(_allowed.Count == 0);
	public Task<bool> CanManageAsync(long adminUserId, long targetTenantId, CancellationToken ct = default) => Task.FromResult(_allowed.Contains(targetTenantId));
	public Task<IReadOnlyList<long>> GetScopedTenantIdsAsync(long adminUserId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<long>>(_allowed.ToList());
	public Task<PlatformAdminScopeView> GetScopeAsync(long adminUserId, CancellationToken ct = default) => Task.FromResult(new PlatformAdminScopeView(_allowed.Count == 0, _allowed.Select(x => new TenantScopeItem(x, "", "")).ToList()));
	public Task<PlatformAdminScopeView> SetScopeAsync(long adminUserId, IReadOnlyList<long> tenantIds, string actor, CancellationToken ct = default) => Task.FromResult(new PlatformAdminScopeView(_allowed.Count == 0, _allowed.Select(x => new TenantScopeItem(x, "", "")).ToList()));
}
