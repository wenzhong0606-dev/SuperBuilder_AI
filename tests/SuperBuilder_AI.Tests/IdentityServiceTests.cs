using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.Auth;
using SuperBuilder_AI.Services.Identity;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// P10.1 Identity/RBAC 服务单测（手写 SQLite 内存库，不依赖 Moq）。
/// 覆盖：种子幂等、全局角色/权限目录、用户创建、RBAC 权限解析、租户隔离、角色指派/撤销。
/// </summary>
public class IdentityServiceTests
{
    private const long Tenant100 = 100;
    private const long Tenant200 = 200;

    private static SuperBIContext CreateContext(out SqliteConnection connection)
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
        var ctx = new SuperBIContext(options);
        ctx.Database.EnsureCreated();
		ctx.Tenants.AddRange(
			new Tenant { Id = 1, TenantCode = "t1", TenantName = "Tenant 1", Enabled = true },
			new Tenant { Id = Tenant100, TenantCode = "t100", TenantName = "Tenant 100", Enabled = true },
			new Tenant { Id = Tenant200, TenantCode = "t200", TenantName = "Tenant 200", Enabled = true });
		ctx.SaveChanges();
        return ctx;
    }

    private static IdentityService CreateService(SuperBIContext ctx) => new(ctx, new PasswordHasher());

    [Fact]
    public async Task SeedAsync_Is_Idempotent()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        await svc.SeedAsync();
        await svc.SeedAsync(); // 第二次应无副作用

        var permCount = await ctx.Permissions.CountAsync(p => p.TenantId == 0);
        var roleCount = await ctx.Roles.CountAsync(r => r.TenantId == 0);
        Assert.Equal(34, permCount);
        Assert.Equal(4, roleCount);
		Assert.Single(await ctx.Tenants.Where(t => t.TenantCode == IdentityService.PlatformTenantCode).ToListAsync());
    }

    [Fact]
    public async Task Seed_Creates_Expected_Global_Roles_And_Permissions()
    {
        using var ctx = CreateContext(out var conn);
        await CreateService(ctx).SeedAsync();

        var roleCodes = await ctx.Roles.Where(r => r.TenantId == 0).Select(r => r.Code).ToListAsync();
        Assert.Contains(IdentityRoles.PlatformAdmin, roleCodes);
        Assert.Contains(IdentityRoles.TenantAdmin, roleCodes);
        Assert.Contains(IdentityRoles.Member, roleCodes);
        Assert.Contains(IdentityRoles.Viewer, roleCodes);

        var permCodes = await ctx.Permissions.Where(p => p.TenantId == 0).Select(p => p.Code).ToListAsync();
        Assert.Contains(IdentityPermissions.DashboardView, permCodes);
        Assert.Contains(IdentityPermissions.IdentityManage, permCodes);
        Assert.Contains(IdentityPermissions.BillingManage, permCodes);
        Assert.Contains(IdentityPermissions.PlatformDiagnosticsView, permCodes);
        Assert.Contains(IdentityPermissions.PlatformDiagnosticsManage, permCodes);
    }

    [Fact]
    public async Task CreateUser_Success_Returns_Id_And_Assigns_Global_Role()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        await svc.SeedAsync();

        var r = await svc.CreateUserAsync(Tenant100, "alice", "Alice", "alice@x.com", new[] { IdentityRoles.Member });
        Assert.True(r.Success, string.Join(";", r.Errors));
        Assert.True(r.Id.HasValue);

        var perms = await svc.GetPermissionsAsync(Tenant100, r.Id!.Value);
        Assert.Contains(IdentityPermissions.DashboardView, perms);
        Assert.Contains(IdentityPermissions.AppCreate, perms);
        Assert.DoesNotContain(IdentityPermissions.DashboardDelete, perms); // member 无删除权
    }

    [Fact]
    public async Task CreateUser_Duplicate_Username_SameTenant_Fails_But_CrossTenant_Succeeds()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        await svc.SeedAsync();

        var first = await svc.CreateUserAsync(Tenant100, "bob", "Bob", "b@x.com", null);
        Assert.True(first.Success);
        // 同租户内重复登录名（规范化后大小写不敏感）应失败。
        var sameTenantDup = await svc.CreateUserAsync(Tenant100, "BOB", "Bob2", "b2@x.com", null);
        Assert.False(sameTenantDup.Success);
        // 跨租户同名允许：唯一范围收窄为 (TenantId, NormalizedUsername)（DEC-02）。
        var crossTenant = await svc.CreateUserAsync(Tenant200, "bob", "Bob3", "b3@x.com", null);
        Assert.True(crossTenant.Success);
    }

    [Fact]
    public async Task CreateUser_Normalizes_Username_Email_And_Defaults_EmailConfirmed()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        await svc.SeedAsync();

        var r = await svc.CreateUserAsync(Tenant100, "  Bob.Smith@Example.COM ", "Bob", "Bob.Smith@Example.COM", null);
        Assert.True(r.Success);
        var user = await ctx.Users.SingleAsync(u => u.Id == r.Id!.Value);

        Assert.Equal("  Bob.Smith@Example.COM ", user.Username); // 原始大小写保留
        Assert.Equal("bob.smith@example.com", user.NormalizedUsername); // 小写、去首尾空白
        Assert.Equal("bob.smith@example.com", user.NormalizedEmail);
        Assert.False(user.EmailConfirmed); // 默认未验证
    }

    [Fact]
    public async Task SetUserStatus_Disable_RotatesSecurityStamp_And_Reenable_Works()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        await svc.SeedAsync();

        var r = await svc.CreateUserAsync(Tenant100, "carol", "Carol", "c@x.com", null);
        var user = await ctx.Users.SingleAsync(u => u.Id == r.Id!.Value);
        var originalStamp = user.SecurityStamp;
        Assert.False(string.IsNullOrEmpty(originalStamp));

        // 禁用即轮换安全戳（M1-03：停用后轮换）。
        var disable = await svc.SetUserStatusAsync(Tenant100, user.Id, UserStatus.Disabled);
        Assert.True(disable.Success);
        user = await ctx.Users.SingleAsync(u => u.Id == user.Id);
        Assert.Equal(UserStatus.Disabled, user.Status);
        Assert.NotEqual(originalStamp, user.SecurityStamp);

        // 重新启用同样轮换。
        var enable = await svc.SetUserStatusAsync(Tenant100, user.Id, UserStatus.Active);
        Assert.True(enable.Success);
        user = await ctx.Users.SingleAsync(u => u.Id == user.Id);
        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public async Task SetUserStatus_UnknownUser_Fails()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        await svc.SeedAsync();

        var result = await svc.SetUserStatusAsync(Tenant100, 999999, UserStatus.Disabled);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task UserRole_CascadeDeleted_When_User_Removed()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        await svc.SeedAsync();

        var user = new User { TenantId = 1, Username = "cascade-u", NormalizedUsername = "cascade-u", SecurityStamp = Guid.NewGuid().ToString("N") };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        var role = new Role { TenantId = 1, Code = "cascade-r", Name = "Cascade R" };
        ctx.Roles.Add(role);
        await ctx.SaveChangesAsync();
        ctx.UserRoles.Add(new UserRole { TenantId = 1, UserId = user.Id, RoleId = role.Id });
        await ctx.SaveChangesAsync();

        Assert.True(await ctx.UserRoles.AnyAsync(ur => ur.UserId == user.Id));
        // M1-03：UserRole→User 外键级联删除。
        ctx.Users.Remove(user);
        await ctx.SaveChangesAsync();
        Assert.False(await ctx.UserRoles.AnyAsync(ur => ur.UserId == user.Id));
    }

    [Fact]
    public async Task RolePermission_CascadeDeleted_When_Role_Removed()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        await svc.SeedAsync();

        var perm = new Permission { TenantId = 1, Code = "cascade:perm", Name = "C", Category = "c" };
        var role = new Role { TenantId = 1, Code = "cascade-role", Name = "C" };
        ctx.Permissions.Add(perm);
        ctx.Roles.Add(role);
        await ctx.SaveChangesAsync();
        ctx.RolePermissions.Add(new RolePermission { TenantId = 1, RoleId = role.Id, PermissionId = perm.Id });
        await ctx.SaveChangesAsync();

        Assert.True(await ctx.RolePermissions.AnyAsync(rp => rp.RoleId == role.Id));
        // M1-03：RolePermission→Role 外键级联删除。
        ctx.Roles.Remove(role);
        await ctx.SaveChangesAsync();
        Assert.False(await ctx.RolePermissions.AnyAsync(rp => rp.RoleId == role.Id));
    }

    [Fact]
    public async Task CreateUser_Invalid_Tenant_Fails()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        await svc.SeedAsync();

        var r = await svc.CreateUserAsync(0, "carol", "Carol", "c@x.com", null);
        Assert.False(r.Success);
    }

    [Fact]
    public async Task Viewer_Role_Only_Has_View_Permissions()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        await svc.SeedAsync();

        var r = await svc.CreateUserAsync(Tenant100, "dave", "Dave", "d@x.com", new[] { IdentityRoles.Viewer });
        var perms = await svc.GetPermissionsAsync(Tenant100, r.Id!.Value);
        Assert.All(perms, p => Assert.EndsWith(":view", p));
        Assert.True(await svc.HasPermissionAsync(Tenant100, r.Id.Value, IdentityPermissions.DashboardView));
        Assert.False(await svc.HasPermissionAsync(Tenant100, r.Id.Value, IdentityPermissions.DashboardEdit));
    }

    [Fact]
    public async Task Assign_And_Revoke_Role_Updates_Permissions()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        await svc.SeedAsync();

        var r = await svc.CreateUserAsync(Tenant100, "erin", "Erin", "e@x.com", null);
        var id = r.Id!.Value;

        var assign = await svc.AssignRoleAsync(Tenant100, id, IdentityRoles.Member);
        Assert.True(assign.Success);
        Assert.Contains(IdentityPermissions.AppCreate, await svc.GetPermissionsAsync(Tenant100, id));

        var revoke = await svc.RevokeRoleAsync(Tenant100, id, IdentityRoles.Member);
        Assert.True(revoke.Success);
        Assert.DoesNotContain(IdentityPermissions.AppCreate, await svc.GetPermissionsAsync(Tenant100, id));
    }

    [Fact]
    public async Task Tenant_Specific_Role_Isolated_From_Other_Tenant()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        await svc.SeedAsync();

        // 直接插入租户 100 专有角色/权限（P10.1 服务不暴露角色创建）
        var customPerm = new Permission { TenantId = Tenant100, Code = "custom:perm", Name = "定制权限", Category = "custom" };
        var customRole = new Role { TenantId = Tenant100, Code = "t100-role", Name = "T100 角色" };
        ctx.Permissions.Add(customPerm);
        ctx.Roles.Add(customRole);
        await ctx.SaveChangesAsync();
        ctx.RolePermissions.Add(new RolePermission { TenantId = Tenant100, RoleId = customRole.Id, PermissionId = customPerm.Id });
        await ctx.SaveChangesAsync();

        var u100 = await svc.CreateUserAsync(Tenant100, "t100u", "T100U", "u@x.com", new[] { "t100-role" });
        var u200 = await svc.CreateUserAsync(Tenant200, "t200u", "T200U", "u2@x.com", null);

        var p100 = await svc.GetPermissionsAsync(Tenant100, u100.Id!.Value);
        Assert.Contains("custom:perm", p100);

        var p200 = await svc.GetPermissionsAsync(Tenant200, u200.Id!.Value);
        Assert.DoesNotContain("custom:perm", p200); // 租户 200 用户看不到租户 100 专有角色
        Assert.Empty(p200);
    }

    [Fact]
    public async Task Global_Role_Visible_To_Any_Tenant_User()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        await svc.SeedAsync();

        var r = await svc.CreateUserAsync(Tenant200, "grace", "Grace", "g@x.com", new[] { IdentityRoles.Viewer });
        var perms = await svc.GetPermissionsAsync(Tenant200, r.Id!.Value);
        Assert.Contains(IdentityPermissions.MetadataView, perms); // 全局 viewer 角色对租户 200 生效
    }

	[Fact]
	public async Task PlatformAdmin_IsPureGovernanceAndCannotBeAssignedToTenantUsers()
	{
		using var ctx = CreateContext(out var conn);
		var svc = CreateService(ctx);
		await svc.SeedAsync();

		var role = await ctx.Roles.SingleAsync(r => r.Code == IdentityRoles.PlatformAdmin);
		var permissionCodes = await (from rp in ctx.RolePermissions
			join p in ctx.Permissions on rp.PermissionId equals p.Id
			where rp.RoleId == role.Id
			select p.Code).ToListAsync();
		Assert.NotEmpty(permissionCodes);
		// PlatformAdmin 仅持有平台治理与多语言治理类权限，不得持有任何租户业务能力
		// （dashboard/app/agent/theme/metadata/audit/billing/identity 等）。localization:* 属平台治理面。
		Assert.All(permissionCodes, code => Assert.True(
			code.StartsWith("platform:") || code.StartsWith("localization:"),
			$"PlatformAdmin 不应持有租户业务能力权限: {code}"));

		var user = await svc.CreateUserAsync(Tenant100, "tenant-governor", "Tenant", "", new[] { IdentityRoles.PlatformAdmin });
		Assert.Empty(await svc.GetPermissionsAsync(Tenant100, user.Id!.Value));
		Assert.False((await svc.AssignRoleAsync(Tenant100, user.Id.Value, IdentityRoles.PlatformAdmin)).Success);
	}

	[Fact]
	public async Task Seed_RevokesHistoricalBusinessPermissionAndTenantUserBinding()
	{
		using var ctx = CreateContext(out var conn);
		var svc = CreateService(ctx);
		await svc.SeedAsync();

		var role = await ctx.Roles.SingleAsync(r => r.Code == IdentityRoles.PlatformAdmin);
		var businessPermission = await ctx.Permissions.SingleAsync(p => p.Code == IdentityPermissions.DashboardView);
		var tenantUser = new User { TenantId = Tenant100, Username = "legacy-admin", DisplayName = "Legacy" };
		ctx.Users.Add(tenantUser);
		await ctx.SaveChangesAsync();
		ctx.RolePermissions.Add(new RolePermission { TenantId = 0, RoleId = role.Id, PermissionId = businessPermission.Id });
		ctx.UserRoles.Add(new UserRole { TenantId = Tenant100, UserId = tenantUser.Id, RoleId = role.Id });
		await ctx.SaveChangesAsync();

		await svc.SeedAsync();

		Assert.False(await ctx.RolePermissions.AnyAsync(rp => rp.RoleId == role.Id && rp.PermissionId == businessPermission.Id));
		Assert.False(await ctx.UserRoles.AnyAsync(ur => ur.UserId == tenantUser.Id && ur.RoleId == role.Id));
		var platformTenant = await ctx.Tenants.SingleAsync(t => t.TenantCode == IdentityService.PlatformTenantCode);
		Assert.True(platformTenant.Id > 0);
	}
}
