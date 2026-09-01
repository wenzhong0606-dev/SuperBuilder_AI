using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Audit;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Interfaces.Quota;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Quota;
using SuperBuilder_AI.Services.Audit;
using SuperBuilder_AI.Services.Auth;
using SuperBuilder_AI.Services.Identity;
using SuperBuilder_AI.Services.Quota;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// P10 总验收 —— 安全 / 审计基线测试（确定性，不调 LLM，复用 SQLite 内存库）。
/// 覆盖：RBAC deny-by-default、角色权限差异、租户作用域隔离、审计记录↔查询租户隔离往返、配额平台默认 enforcement。
/// </summary>
public class P10AcceptanceTests
{
    private const long Tenant1 = 110;
    private const long Tenant2 = 120;

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
    public async Task RBAC_Deny_By_Default_For_User_With_No_Role()
    {
        using var ctx = CreateContext(out var conn);
        var identity = new IdentityService(ctx, new PasswordHasher());
        await identity.SeedAsync();

        var created = await identity.CreateUserAsync(Tenant1, "alice", "Alice", "a@x.com", null);
        Assert.True(created.Success);

        var perms = await identity.GetPermissionsAsync(Tenant1, created.Id!.Value);
        Assert.Empty(perms); // deny-by-default

        Assert.False(await identity.HasPermissionAsync(Tenant1, created.Id.Value, IdentityPermissions.IdentityManage));
        Assert.False(await identity.HasPermissionAsync(Tenant1, created.Id.Value, IdentityPermissions.DashboardDelete));
    }

    [Fact]
    public async Task RBAC_Viewer_And_Tenant_User_Cannot_Acquire_PlatformAdmin()
    {
        using var ctx = CreateContext(out var conn);
        var identity = new IdentityService(ctx, new PasswordHasher());
        await identity.SeedAsync();

        var viewer = await identity.CreateUserAsync(Tenant1, "viewer1", "V", "v@x.com", new[] { IdentityRoles.Viewer });
        var admin = await identity.CreateUserAsync(Tenant1, "admin1", "A", "a@x.com", new[] { IdentityRoles.PlatformAdmin });
        Assert.True(viewer.Success);
        Assert.True(admin.Success);

        // viewer: 仅查看类权限，不能管理身份 / 删除仪表盘
        Assert.True(await identity.HasPermissionAsync(Tenant1, viewer.Id!.Value, IdentityPermissions.DashboardView));
        Assert.False(await identity.HasPermissionAsync(Tenant1, viewer.Id.Value, IdentityPermissions.IdentityManage));
        Assert.False(await identity.HasPermissionAsync(Tenant1, viewer.Id.Value, IdentityPermissions.DashboardDelete));

        // platform-admin 是平台租户专属治理角色，普通租户创建入口必须忽略该角色。
        Assert.Empty(await identity.GetPermissionsAsync(Tenant1, admin.Id!.Value));
        Assert.False(await identity.HasPermissionAsync(Tenant1, admin.Id.Value, IdentityPermissions.PlatformTenantManage));
    }

    [Fact]
    public async Task RBAC_Permissions_Are_Tenant_Scoped()
    {
        using var ctx = CreateContext(out var conn);
        var identity = new IdentityService(ctx, new PasswordHasher());
        await identity.SeedAsync();

        var created = await identity.CreateUserAsync(Tenant1, "bob", "Bob", "b@x.com", new[] { IdentityRoles.Member });
        Assert.True(created.Success);

        var inTenant1 = await identity.GetPermissionsAsync(Tenant1, created.Id!.Value);
        Assert.NotEmpty(inTenant1);

        var inTenant2 = await identity.GetPermissionsAsync(Tenant2, created.Id.Value);
        Assert.Empty(inTenant2); // 跨租户不可见
    }

    [Fact]
    public async Task Audit_Log_Record_And_Query_RoundTrip_With_Tenant_Isolation()
    {
        using var ctx = CreateContext(out var conn);
        var audit = new AuditLogService(ctx);

        var id1 = await audit.LogAsync(new AuditLogEntry(
            TenantId: Tenant1, Action: "user.create", EntityType: "User",
            UserId: 1, Actor: "system", EntityId: "1", Result: "success"));
        Assert.True(id1 > 0);

        await audit.LogAsync(new AuditLogEntry(
            TenantId: Tenant2, Action: "role.assign", EntityType: "Role",
            UserId: 2, Actor: "system", EntityId: "2", Result: "success"));

        // 租户作用域查询：仅本租户条目
        var t1Logs = await audit.QueryAsync(new AuditLogQuery(TenantId: Tenant1));
        Assert.Single(t1Logs);
        Assert.Equal("user.create", t1Logs[0].Action);
        Assert.Equal(Tenant1, t1Logs[0].TenantId);

        // 平台级查询（TenantId=null）可见全部
        var allLogs = await audit.QueryAsync(new AuditLogQuery());
        Assert.Equal(2, allLogs.Count);
    }

    [Fact]
    public async Task Quota_Platform_Default_Enforcement_Blocks_Overflow()
    {
        using var ctx = CreateContext(out var conn);
        var quota = new QuotaService(ctx);
        await quota.EnsureSeededAsync();

        // 平台默认 Users=50
        var ok = await quota.ConsumeAsync(Tenant1, QuotaResourceType.Users, 50);
        Assert.True(ok);
        var overflow = await quota.ConsumeAsync(Tenant1, QuotaResourceType.Users, 1);
        Assert.False(overflow);

        // 默认查询返回全部 7 个受管资源，且 Users 上限为平台默认 50
        var overview = await quota.GetQuotaAsync(Tenant2);
        Assert.Equal(7, overview.Items.Count);

        var check = await quota.CheckAsync(Tenant2, QuotaResourceType.Users, 1);
        Assert.Equal(50, check.Limit);
        Assert.Equal(0, check.Used);
        Assert.Equal(50, check.Remaining);
        Assert.True(check.Allowed);
    }
}
