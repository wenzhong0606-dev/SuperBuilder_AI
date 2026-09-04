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

	private static SuperBIContext CreateContext(out SqliteConnection connection)
	{
		connection = new SqliteConnection("DataSource=:memory:");
		connection.Open();
		var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
		var ctx = new SuperBIContext(options);
		ctx.Database.EnsureCreated();
		return ctx;
	}

	private static TenantManagementController Build(SuperBIContext db, ClaimsPrincipal? user = null)
	{
		var ctrl = new TenantManagementController(db, new SuccessIdentityService());
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
}
