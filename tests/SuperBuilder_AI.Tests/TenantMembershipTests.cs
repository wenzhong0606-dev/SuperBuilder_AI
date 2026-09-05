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
using SuperBuilder_AI.Interfaces.Audit;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.Audit;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.Auth;
using SuperBuilder_AI.Services.Identity;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M2-05 多租户成员关系（SB-P1-16）：UserTenant 查询与平台治理面管理端点。
/// SQLite 内存库 + 真实 DbContext；仅 ListAll 用到 ITenantMembershipService，其余依赖用桩。
/// </summary>
public sealed class TenantMembershipTests
{
	private static SuperBIContext CreateContext(out SqliteConnection connection)
	{
		connection = new SqliteConnection("DataSource=:memory:");
		connection.Open();
		var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
		var ctx = new SuperBIContext(options);
		ctx.Database.EnsureCreated();
		return ctx;
	}

	private static TenantMembershipController Build(SuperBIContext db, ClaimsPrincipal user)
	{
		var ctrl = new TenantMembershipController(
			db, new TenantMembershipService(db), new NoopIdentityService(), new NoopTokenService(), new NoopAuditService());
		ctrl.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } };
		return ctrl;
	}

	private static ClaimsPrincipal WithPerms(params string[] perms)
	{
		var claims = perms.Select(p => new Claim("perm", p)).ToList();
		claims.Add(new Claim(ClaimTypes.NameIdentifier, "1"));
		return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
	}

	private static long SeedTenant(SuperBIContext ctx, string code)
	{
		var t = new Tenant { TenantCode = code, TenantName = code, Enabled = true };
		ctx.Tenants.Add(t);
		ctx.SaveChanges();
		return t.Id;
	}

	private static long SeedUser(SuperBIContext ctx, long homeTenantId, string username)
	{
		var u = new User { TenantId = homeTenantId, Username = username, Status = UserStatus.Active };
		ctx.Users.Add(u);
		ctx.SaveChanges();
		return u.Id;
	}

	[Fact]
	public async Task ListAllAsync_ReturnsOnlyExplicitMemberships_NotImplicitHome()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var home = SeedTenant(ctx, "home");
		var other = SeedTenant(ctx, "other");
		var user = SeedUser(ctx, home, "alice");
		// 显式加入 other 租户（home 为隐式成员，不应出现在 ListAll 中）
		ctx.UserTenants.Add(new UserTenant { UserId = user, TenantId = other, IsDefault = false, CreatedAtUtc = DateTime.UtcNow });
		await ctx.SaveChangesAsync();

		var svc = new TenantMembershipService(ctx);
		var rows = await svc.ListAllAsync(CancellationToken.None);

		Assert.Single(rows);
		var row = rows[0];
		Assert.Equal(user, row.UserId);
		Assert.Equal("alice", row.UserName);
		Assert.Equal(other, row.TenantId);
		Assert.Equal("other", row.TenantCode);
		Assert.False(row.IsDefault);
	}

	[Fact]
	public async Task AdminList_RequiresPlatformTenantManage_ForbiddenWithoutClaim()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		// 仅携带身份，无 platform:tenant:manage 权限
		var ctrl = Build(ctx, WithPerms());
		var result = await ctrl.ListAll(CancellationToken.None);

		var obj = Assert.IsType<ObjectResult>(result);
		Assert.Equal(403, obj.StatusCode);
	}

	[Fact]
	public async Task AdminList_AllowedWithClaim_ReturnsArray()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		var home = SeedTenant(ctx, "home");
		var other = SeedTenant(ctx, "other");
		var user = SeedUser(ctx, home, "alice");
		ctx.UserTenants.Add(new UserTenant { UserId = user, TenantId = other });
		await ctx.SaveChangesAsync();

		var ctrl = Build(ctx, WithPerms(IdentityPermissions.PlatformTenantManage));
		var result = await ctrl.ListAll(CancellationToken.None);

		var ok = Assert.IsType<OkObjectResult>(result);
		var rows = Assert.IsAssignableFrom<IEnumerable<TenantMembershipAdminRow>>(ok.Value);
		Assert.Single(rows);
	}
}

internal sealed class NoopIdentityService : IIdentityService
{
	public Task SeedAsync(CancellationToken ct = default) => Task.CompletedTask;
	public Task<IdentityResult> CreateUserAsync(long tenantId, string username, string? displayName, string? email, string[]? roleCodes, CancellationToken ct = default) => Task.FromResult(IdentityResult.Ok(1L));
	public Task<IdentityResult> AssignRoleAsync(long tenantId, long userId, string roleCode, CancellationToken ct = default) => Task.FromResult(IdentityResult.Ok(userId));
	public Task<IdentityResult> RevokeRoleAsync(long tenantId, long userId, string roleCode, CancellationToken ct = default) => Task.FromResult(IdentityResult.Ok(userId));
	public Task<IdentityResult> SetPasswordAsync(long tenantId, long userId, string password, CancellationToken ct = default) => Task.FromResult(IdentityResult.Ok(userId));
	public Task<IdentityResult> SetUserStatusAsync(long tenantId, long userId, UserStatus newStatus, CancellationToken ct = default) => Task.FromResult(IdentityResult.Ok(userId));
	public Task<IReadOnlyList<string>> GetPermissionsAsync(long tenantId, long userId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
	public Task<bool> HasPermissionAsync(long tenantId, long userId, string permissionCode, CancellationToken ct = default) => Task.FromResult(false);
}

internal sealed class NoopTokenService : ITokenService
{
	public string Issue(long tenantId, long userId, string username, IEnumerable<string> permissions, string? securityStamp = null, long? homeTenantId = null) => "x";
	public TokenPrincipal? Validate(string? token) => null;
}

internal sealed class NoopAuditService : IAuditLogService
{
	public Task<long> LogAsync(AuditLogEntry entry, CancellationToken ct = default) => Task.FromResult(0L);
	public Task<IReadOnlyList<AuditLog>> QueryAsync(AuditLogQuery query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<AuditLog>>(Array.Empty<AuditLog>());
}
