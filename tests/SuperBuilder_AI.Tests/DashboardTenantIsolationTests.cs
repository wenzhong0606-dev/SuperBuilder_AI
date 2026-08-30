using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models.Dashboard;
using Xunit;

// 本测试命名空间为 SuperBuilder_AI.Tests.Dashboard，与实体类型 Dashboard 同名，
// 故用别名消歧（编译器会优先把 Dashboard 解析为命名空间）。
using DashboardEntity = SuperBuilder_AI.Models.Dashboard.Dashboard;

namespace SuperBuilder_AI.Tests.Dashboard;

/// <summary>
/// P6.1 仪表盘持久化的租户隔离单测（EF Core SQLite 内存库，离线可还原）。
///
/// 与 P4.3/P5.2 保持一致的隔离语义：
///   1. 未开启作用域（Golden/系统路径）=> 过滤为 no-op，全部可见；
///   2. 开启某租户作用域 => 仅本租户私有仪表盘可见，他租户不可见；
///   3. 全局模板（<c>TenantId = 0</c>）对本租户仍可见 —— 否则租户作用域内模板会集体消失。
/// </summary>
public class DashboardTenantIsolationTests
{
    private static SuperBIContext CreateContext(out SqliteConnection connection)
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SuperBIContext>()
            .UseSqlite(connection)
            .Options;
        var ctx = new SuperBIContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static async Task<DashboardEntity> AddDashboard(
        SuperBIContext ctx,
        long tenantId,
        string code,
        string title = "看板")
    {
        var dashboard = new DashboardEntity
        {
            TenantId = tenantId,
            Code = code,
            Title = title,
            Status = DashboardStatuses.Draft,
            DslVersion = DslVersions.Current,
            DslJson = "{\"version\":\"1.0\",\"title\":\"" + title + "\",\"pages\":[]}",
        };
        ctx.Dashboards.Add(dashboard);
        await ctx.SaveChangesAsync();
        return dashboard;
    }

    [Fact]
    public async Task NoTenantScope_SeesAllDashboards()
    {
        var ctx = CreateContext(out var connection);
        await using var _ = connection;
        await using var __ = ctx;

        await AddDashboard(ctx, 1, "t1-a");
        await AddDashboard(ctx, 2, "t2-a");
        await AddDashboard(ctx, 0, "global-a");

        // 未开启作用域（Golden/系统路径）：过滤恒为 no-op。
        Assert.Equal(3, await ctx.Dashboards.CountAsync());
    }

    [Fact]
    public async Task TenantScope_OtherTenantDashboards_AreInvisible()
    {
        var ctx = CreateContext(out var connection);
        await using var _ = connection;
        await using var __ = ctx;

        await AddDashboard(ctx, 1, "t1-a", "租户一看板");
        await AddDashboard(ctx, 2, "t2-a", "租户二看板");

        ctx.ApplyTenantScope(1);

        var visible = await ctx.Dashboards.AsNoTracking().ToListAsync();

        var dashboard = Assert.Single(visible);
        Assert.Equal("租户一看板", dashboard.Title);
    }

    [Fact]
    public async Task TenantScope_GlobalTemplate_StaysVisible()
    {
        var ctx = CreateContext(out var connection);
        await using var _ = connection;
        await using var __ = ctx;

        await AddDashboard(ctx, 1, "t1-a", "租户一私有");
        await AddDashboard(ctx, 0, "global-tpl", "全局模板");

        ctx.ApplyTenantScope(1);

        var codes = await ctx.Dashboards.AsNoTracking().Select(d => d.Code).ToListAsync();

        // 全局模板（TenantId=0）必须对本租户可见，否则模板能力在租户作用域内失效。
        Assert.Contains("global-tpl", codes);
        Assert.Contains("t1-a", codes);
        Assert.Equal(2, codes.Count);
    }

    [Fact]
    public async Task TenantScope_SystemContext_IsNoOp()
    {
        var ctx = CreateContext(out var connection);
        await using var _ = connection;
        await using var __ = ctx;

        await AddDashboard(ctx, 1, "t1-a");
        await AddDashboard(ctx, 2, "t2-a");

        // tenantId <= 0：ApplyTenantScope 内部保持关闭，等价于 no-op。
        ctx.ApplyTenantScope(0);

        Assert.Equal(2, await ctx.Dashboards.CountAsync());
    }

    [Fact]
    public async Task Persistence_RoundTripsDslDocument()
    {
        var ctx = CreateContext(out var connection);
        await using var _ = connection;
        await using var __ = ctx;

        var dsl = new DashboardDsl
        {
            Code = "sales",
            Title = "销售总览",
            Pages = { new PageDsl { Id = "p1", Name = "概览" } },
        };
        var json = new Services.BI.Dashboard.DashboardDslSerializer().Serialize(dsl);

        ctx.Dashboards.Add(new DashboardEntity
        {
            TenantId = 1,
            Code = "sales",
            Title = dsl.Title,
            DslVersion = dsl.Version,
            DslJson = json,
        });
        await ctx.SaveChangesAsync();

        var stored = await ctx.Dashboards.AsNoTracking().SingleAsync();

        Assert.True(
            new Services.BI.Dashboard.DashboardDslSerializer()
                .TryDeserialize(stored.DslJson, out var restored, out var errors),
            string.Join("; ", errors));
        Assert.Equal("销售总览", restored!.Title);
        Assert.Single(restored.Pages);
    }
}
