using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Controllers;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Quota;
using SuperBuilder_AI.Services.Quota;
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

    private static QuotaController Build(SuperBIContext db)
    {
        var svc = new QuotaService(db);
        svc.EnsureSeededAsync(CancellationToken.None).GetAwaiter().GetResult();
        return new QuotaController(svc);
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
}
