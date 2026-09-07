using System.Linq;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Controllers;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Quota;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Models.Quota;
using SuperBuilder_AI.Services.Quota;
using SuperBuilder_AI.Services.Identity;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// P10.4 QuotaController 单测（SQLite 内存库 + 真实 QuotaService，不依赖 Moq）。
/// 覆盖：配额概览、单资源查询、校验端点、未知资源拒绝、扣减成功、超配额 409、全局租户拒绝。
/// </summary>
public class QuotaControllerTests
{
    private const long Tenant5 = 5;

    private static SuperBIContext CreateContext(out SqliteConnection connection)
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
        var ctx = new SuperBIContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private sealed class DenyScopeService : IPlatformAdminScopeService
    {
        public Task<bool> HasFullScopeAsync(long adminUserId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> CanManageAsync(long adminUserId, long targetTenantId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<long>> GetScopedTenantIdsAsync(long adminUserId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<long>>(new[] { 999L });
        public Task<PlatformAdminScopeView> GetScopeAsync(long adminUserId, CancellationToken ct = default) => Task.FromResult(new PlatformAdminScopeView(false, System.Array.Empty<TenantScopeItem>()));
        public Task<PlatformAdminScopeView> SetScopeAsync(long adminUserId, IReadOnlyList<long> tenantIds, string actor, CancellationToken ct = default) => GetScopeAsync(adminUserId, ct);
    }

    private static QuotaController Build(SuperBIContext db, IPlatformAdminScopeService? scope = null)
    {
        var svc = new QuotaService(db);
        svc.EnsureSeededAsync(CancellationToken.None).GetAwaiter().GetResult();
        var ctrl = new QuotaController(svc, scope);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim("perm", IdentityPermissions.PlatformQuotaManage),
                    new Claim(ClaimTypes.NameIdentifier, "99"),
                }, "test")),
            },
        };
        return ctrl;
    }

    private static void SeedTenant(SuperBIContext db)
    {
        if (db.Tenants.Any(t => t.Id == Tenant5)) return;
        db.Tenants.Add(new Tenant { Id = Tenant5, TenantCode = "t5", TenantName = "Tenant 5", Enabled = true });
        db.SaveChanges();
    }

    [Fact]
    public async Task Get_Returns_200_With_Items()
    {
        var ctx = CreateContext(out var conn);
        await using var _ = conn;
        await using var __ = ctx;
        var ctrl = Build(ctx);
        var res = await ctrl.Get(tenantId: Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
        Assert.NotNull(res);
        var overview = Assert.IsType<QuotaOverviewResponse>(res!.Value);
        Assert.Equal(Tenant5, overview.TenantId);
        Assert.Equal(7, overview.Items.Count);
    }

    [Fact]
    public async Task Get_GlobalTenant_Rejected_400()
    {
        var ctx = CreateContext(out var conn);
        await using var _ = conn;
        await using var __ = ctx;
        var ctrl = Build(ctx);
        var res = await ctrl.Get(tenantId: 0, CancellationToken.None) as Microsoft.AspNetCore.Mvc.BadRequestObjectResult;
        Assert.NotNull(res);
    }

    [Fact]
    public async Task GetOne_Returns_Current_State()
    {
        var ctx = CreateContext(out var conn);
        await using var _ = conn;
        await using var __ = ctx;
        var ctrl = Build(ctx);
        var res = await ctrl.GetOne("users", tenantId: Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
        Assert.NotNull(res);
        var check = Assert.IsType<QuotaCheckResponse>(res!.Value);
        Assert.Equal(50, check.Limit);
        Assert.True(check.Allowed);
    }

    [Fact]
    public async Task Check_Endpoint_Allows_Within_Limit()
    {
        var ctx = CreateContext(out var conn);
        await using var _ = conn;
        await using var __ = ctx;
        var ctrl = Build(ctx);
        var res = await ctrl.Check(new QuotaCheckRequest("apps", 1), tenantId: Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
        Assert.NotNull(res);
        var check = Assert.IsType<QuotaCheckResponse>(res!.Value);
        Assert.True(check.Allowed);
        Assert.Equal(20, check.Limit);
    }

    [Fact]
    public async Task Check_Unknown_Resource_Rejected_400()
    {
        var ctx = CreateContext(out var conn);
        await using var _ = conn;
        await using var __ = ctx;
        var ctrl = Build(ctx);
        var res = await ctrl.Check(new QuotaCheckRequest("bogus", 1), tenantId: Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.BadRequestObjectResult;
        Assert.NotNull(res);
    }

    [Fact]
    public async Task Consume_Endpoint_Success()
    {
        var ctx = CreateContext(out var conn);
        await using var _ = conn;
        await using var __ = ctx;
        var ctrl = Build(ctx);
        var res = await ctrl.Consume(new QuotaConsumeRequest("dashboards", 2), tenantId: Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
        Assert.NotNull(res);
        var after = Assert.IsType<QuotaCheckResponse>(res!.Value);
        Assert.Equal(2, after.Used);
        Assert.Equal(28, after.Remaining);
    }

    [Fact]
    public async Task Consume_Exceeding_Limit_Returns_409()
    {
        var ctx = CreateContext(out var conn);
        await using var _ = conn;
        await using var __ = ctx;
        var ctrl = Build(ctx);
        var fill = await ctrl.Consume(new QuotaConsumeRequest("dashboards", 30), tenantId: Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
        Assert.NotNull(fill);
        var over = await ctrl.Consume(new QuotaConsumeRequest("dashboards", 1), tenantId: Tenant5, CancellationToken.None) as Microsoft.AspNetCore.Mvc.ConflictObjectResult;
        Assert.NotNull(over);
        Assert.Equal(409, over!.StatusCode);
    }

    [Fact]
    public async Task Consume_GlobalTenant_Rejected_400()
    {
        var ctx = CreateContext(out var conn);
        await using var _ = conn;
        await using var __ = ctx;
        var ctrl = Build(ctx);
        var res = await ctrl.Consume(new QuotaConsumeRequest("apps", 1), tenantId: 0, CancellationToken.None) as Microsoft.AspNetCore.Mvc.BadRequestObjectResult;
        Assert.NotNull(res);
    }

    [Fact]
    public async Task Defaults_Returns_Platform_Policies_With_Stable_Resource_Names()
    {
        var ctx = CreateContext(out var conn);
        await using var _ = conn;
        await using var __ = ctx;
        var ctrl = Build(ctx);

        var res = Assert.IsType<OkObjectResult>(await ctrl.GetDefaults(CancellationToken.None));
        var overview = Assert.IsType<QuotaOverviewResponse>(res.Value);
        Assert.Equal(0, overview.TenantId);
        Assert.Contains(overview.Items, x => x.ResourceType == "Users" && x.Window == "Total");
    }

    [Fact]
    public async Task Policy_Override_And_Inherit_Form_A_Closed_Loop()
    {
        var ctx = CreateContext(out var conn);
        await using var _ = conn;
        await using var __ = ctx;
        SeedTenant(ctx);
        var ctrl = Build(ctx);

        var saved = Assert.IsType<OkObjectResult>(await ctrl.UpsertPolicy(
            "users", new QuotaPolicyUpdateRequest(12, "Monthly"), Tenant5, CancellationToken.None));
        var row = Assert.IsType<QuotaItemView>(saved.Value);
        Assert.True(row.IsOverride);
        Assert.Equal(12, row.Limit);
        Assert.Equal("Monthly", row.Window);
        Assert.Equal(50, row.PlatformLimit);

        var inherited = Assert.IsType<OkObjectResult>(await ctrl.RemoveOverride("users", Tenant5, CancellationToken.None));
        var overview = Assert.IsType<QuotaOverviewResponse>(inherited.Value);
        var users = Assert.Single(overview.Items, x => x.ResourceType == "Users");
        Assert.False(users.IsOverride);
        Assert.Equal(50, users.Limit);
    }

    [Fact]
    public async Task Usage_Maintenance_Updates_Current_Period()
    {
        var ctx = CreateContext(out var conn);
        await using var _ = conn;
        await using var __ = ctx;
        SeedTenant(ctx);
        var ctrl = Build(ctx);

        var result = Assert.IsType<OkObjectResult>(await ctrl.SetUsage(
            "apps", new QuotaUsageUpdateRequest(7), Tenant5, CancellationToken.None));
        var row = Assert.IsType<QuotaItemView>(result.Value);
        Assert.Equal(7, row.Used);
        Assert.Equal(13, row.Remaining);
        Assert.Equal("total", row.PeriodKey);
    }

    [Fact]
    public async Task Policy_Write_Without_Platform_Permission_Is_Forbidden()
    {
        var ctx = CreateContext(out var conn);
        await using var _ = conn;
        await using var __ = ctx;
        SeedTenant(ctx);
        var ctrl = Build(ctx);
        ctrl.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("tid", Tenant5.ToString()) }, "test"));

        var result = Assert.IsType<ObjectResult>(await ctrl.UpsertPolicy(
            "users", new QuotaPolicyUpdateRequest(5, "Total"), Tenant5, CancellationToken.None));
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task Scoped_Platform_Admin_Cannot_Manage_Outside_Assigned_Tenants()
    {
        var ctx = CreateContext(out var conn);
        await using var _ = conn;
        await using var __ = ctx;
        SeedTenant(ctx);
        var ctrl = Build(ctx, new DenyScopeService());

        var result = Assert.IsType<ObjectResult>(await ctrl.UpsertPolicy(
            "users", new QuotaPolicyUpdateRequest(5, "Total"), Tenant5, CancellationToken.None));
        Assert.Equal(403, result.StatusCode);
        Assert.Empty(ctx.QuotaPolicies.Where(x => x.TenantId == Tenant5));
    }
}
