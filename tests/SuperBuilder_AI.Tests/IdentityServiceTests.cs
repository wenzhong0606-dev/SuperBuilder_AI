using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.Identity;
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
        return ctx;
    }

    private static IdentityService CreateService(SuperBIContext ctx) => new(ctx);

    [Fact]
    public async Task SeedAsync_Is_Idempotent()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        await svc.SeedAsync();
        await svc.SeedAsync(); // 第二次应无副作用

        var permCount = await ctx.Permissions.CountAsync(p => p.TenantId == 0);
        var roleCount = await ctx.Roles.CountAsync(r => r.TenantId == 0);
        Assert.Equal(24, permCount);
        Assert.Equal(4, roleCount);
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
    public async Task CreateUser_Duplicate_Username_Fails()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        await svc.SeedAsync();

        var first = await svc.CreateUserAsync(Tenant100, "bob", "Bob", "b@x.com", null);
        Assert.True(first.Success);
        var second = await svc.CreateUserAsync(Tenant200, "bob", "Bob2", "b2@x.com", null); // 全局唯一
        Assert.False(second.Success);
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
}
