using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Quota;
using SuperBuilder_AI.Services.Quota;
using SuperBuilder_AI.Models.Quota;
using SuperBuilder_AI.Models.Organization;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// P10.4 Quota/Billing 服务单测（手写 SQLite 内存库，不依赖 Moq）。
/// 覆盖：种子幂等、平台默认回退、校验通过/超限、扣减、租户隔离、租户覆盖策略、月度滚动、负数防御。
/// </summary>
public class QuotaServiceTests
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

    private static QuotaService CreateService(SuperBIContext ctx) => new(ctx);

    private static void SeedTenant(SuperBIContext ctx, long tenantId)
    {
        ctx.Tenants.Add(new Tenant
        {
            Id = tenantId,
            TenantCode = $"t-{tenantId}",
            TenantName = $"Tenant {tenantId}",
            Enabled = true,
        });
        ctx.SaveChanges();
    }

    [Fact]
    public async Task EnsureSeeded_Is_Idempotent()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        await svc.EnsureSeededAsync();
        await svc.EnsureSeededAsync(); // 第二次应无副作用
        var count = await ctx.QuotaPolicies.CountAsync(p => p.TenantId == 0);
        Assert.Equal(7, count); // 7 个资源类型
    }

    [Fact]
    public async Task GetQuota_FallsBack_To_PlatformDefault()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        await svc.EnsureSeededAsync();

        var overview = await svc.GetQuotaAsync(Tenant100);
        Assert.Equal(Tenant100, overview.TenantId);
        Assert.Equal(7, overview.Items.Count);

        var users = await svc.CheckAsync(Tenant100, QuotaResourceType.Users, 0);
        Assert.Equal(50, users.Limit);
        Assert.Equal(0, users.Used);
        Assert.Equal(50, users.Remaining);
    }

    [Fact]
    public async Task Check_Allows_Within_Limit()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        await svc.EnsureSeededAsync();

        var res = await svc.CheckAsync(Tenant100, QuotaResourceType.Users, 1);
        Assert.True(res.Allowed);
        Assert.Equal(50, res.Limit);
        Assert.Equal(0, res.Used);
        Assert.Equal(50, res.Remaining);
    }

    [Fact]
    public async Task Consume_Success_Decrements_Remaining()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        await svc.EnsureSeededAsync();

        var ok = await svc.ConsumeAsync(Tenant100, QuotaResourceType.Users, 5);
        Assert.True(ok);

        var after = await svc.CheckAsync(Tenant100, QuotaResourceType.Users, 0);
        Assert.Equal(5, after.Used);
        Assert.Equal(45, after.Remaining);
    }

    [Fact]
    public async Task Consume_Exceeding_Limit_Returns_False()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        await svc.EnsureSeededAsync();

        var first = await svc.ConsumeAsync(Tenant100, QuotaResourceType.Users, 50);
        Assert.True(first);
        var second = await svc.ConsumeAsync(Tenant100, QuotaResourceType.Users, 1);
        Assert.False(second); // 51 > 50
    }

    [Fact]
    public async Task Tenant_Usage_Is_Isolated()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        await svc.EnsureSeededAsync();

        await svc.ConsumeAsync(Tenant100, QuotaResourceType.Apps, 3);
        var t100 = await svc.CheckAsync(Tenant100, QuotaResourceType.Apps, 0);
        var t200 = await svc.CheckAsync(Tenant200, QuotaResourceType.Apps, 0);
        Assert.Equal(3, t100.Used);
        Assert.Equal(0, t200.Used);
    }

    [Fact]
    public async Task Tenant_Override_Policy_Beats_Default()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        await svc.EnsureSeededAsync();

        ctx.QuotaPolicies.Add(new QuotaPolicy
        {
            TenantId = Tenant100,
            ResourceType = QuotaResourceType.Users,
            Limit = 2,
            Window = QuotaWindow.Total,
            CreatedTime = System.DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var res = await svc.CheckAsync(Tenant100, QuotaResourceType.Users, 1);
        Assert.Equal(2, res.Limit);
    }

    [Fact]
    public async Task Monthly_Usage_Rolls_Over_When_Period_Changes()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        await svc.EnsureSeededAsync();

        var prevKey = System.DateTime.UtcNow.AddMonths(-1).ToString("yyyy-MM");
        ctx.QuotaUsages.Add(new QuotaUsage
        {
            TenantId = Tenant100,
            ResourceType = QuotaResourceType.ApiCallsPerMonth,
            Used = 9999,
            PeriodKey = prevKey,
            LastReset = System.DateTime.UtcNow.AddMonths(-1),
            CreatedTime = System.DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        // 只读校验：跨周期视为已归零
        var res = await svc.CheckAsync(Tenant100, QuotaResourceType.ApiCallsPerMonth, 1);
        Assert.True(res.Allowed);
        Assert.Equal(0, res.Used);
    }

    [Fact]
    public async Task Consume_Negative_Amount_Throws()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        await svc.EnsureSeededAsync();
        await Assert.ThrowsAsync<System.ArgumentException>(() => svc.ConsumeAsync(Tenant100, QuotaResourceType.Users, -1));
    }

    [Fact]
    public async Task UpsertPolicy_Creates_Override_And_Reports_Platform_Baseline()
    {
        using var ctx = CreateContext(out var conn);
        SeedTenant(ctx, Tenant100);
        var svc = CreateService(ctx);
        await svc.EnsureSeededAsync();

        var row = await svc.UpsertPolicyAsync(Tenant100, QuotaResourceType.DataSources, 9, QuotaWindow.Monthly);

        Assert.True(row.IsOverride);
        Assert.Equal("DataSources", row.ResourceType);
        Assert.Equal(9, row.Limit);
        Assert.Equal("Monthly", row.Window);
        Assert.Equal(5, row.PlatformLimit);
        Assert.Equal("Total", row.PlatformWindow);
    }

    [Fact]
    public async Task RemoveOverride_Restores_Platform_Default()
    {
        using var ctx = CreateContext(out var conn);
        SeedTenant(ctx, Tenant100);
        var svc = CreateService(ctx);
        await svc.EnsureSeededAsync();
        await svc.UpsertPolicyAsync(Tenant100, QuotaResourceType.Users, 2, QuotaWindow.Daily);

        Assert.True(await svc.RemoveTenantOverrideAsync(Tenant100, QuotaResourceType.Users));
        var row = Assert.Single((await svc.GetQuotaAsync(Tenant100)).Items, x => x.ResourceType == "Users");
        Assert.False(row.IsOverride);
        Assert.Equal(50, row.Limit);
        Assert.Equal("Total", row.Window);
    }

    [Fact]
    public async Task SetUsage_Maintains_Current_Effective_Window()
    {
        using var ctx = CreateContext(out var conn);
        SeedTenant(ctx, Tenant100);
        var svc = CreateService(ctx);
        await svc.EnsureSeededAsync();
        await svc.UpsertPolicyAsync(Tenant100, QuotaResourceType.ApiCallsPerMonth, 200, QuotaWindow.Daily);

        var row = await svc.SetUsageAsync(Tenant100, QuotaResourceType.ApiCallsPerMonth, 42);

        Assert.Equal(42, row.Used);
        Assert.Equal(158, row.Remaining);
        Assert.Equal(System.DateTime.UtcNow.ToString("yyyy-MM-dd"), row.PeriodKey);
    }
}
