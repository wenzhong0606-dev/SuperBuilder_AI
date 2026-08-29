using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models.BI.Entity;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.Organization;
using Xunit;

namespace SuperBuilder_AI.Tests.Persistence;

/// <summary>
/// P4.3 全局租户过滤的跨租户隔离单测。
///
/// 使用 EF Core SQLite 内存库（离线可还原）验证：
///   1. 未开启作用域（Golden/系统路径，ApplyTenantScope 从未调用）=> 查询不过滤（no-op），可见全部租户数据；
///   2. 开启某租户作用域 => 仅该租户根实体可见，跨租户数据不可见；
///   3. 系统/全局上下文（tenantId &lt;= 0）=> 等价于 no-op。
///
/// 覆盖全部直接持有 TenantId 的根实体：DataSource / MetadataTable / TenantSetting / BusinessEntity。
/// </summary>
public class SuperBIContextTenantFilterTests
{
    private static SuperBIContext CreateContext(out SqliteConnection connection)
    {
        // SQLite 内存库：连接必须保持打开，否则数据随连接关闭而丢失。
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SuperBIContext>()
            .UseSqlite(connection)
            .Options;
        var ctx = new SuperBIContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static void Seed(SuperBIContext ctx)
    {
        var t1 = new Tenant { TenantCode = "T1", TenantName = "Tenant One" };
        var t2 = new Tenant { TenantCode = "T2", TenantName = "Tenant Two" };
        ctx.Tenants.AddRange(t1, t2);
        ctx.SaveChanges();

        var ds1 = new DataSource { TenantId = t1.Id, DbType = "SQLServer", ConnectionString = "cs1" };
        var ds2 = new DataSource { TenantId = t2.Id, DbType = "MySQL", ConnectionString = "cs2" };
        ctx.DataSources.AddRange(ds1, ds2);
        ctx.SaveChanges();

        var mt1 = new MetadataTable { TenantId = t1.Id, DataSourceId = ds1.Id, TableName = "Orders" };
        var mt2 = new MetadataTable { TenantId = t2.Id, DataSourceId = ds2.Id, TableName = "Products" };
        ctx.MetadataTables.AddRange(mt1, mt2);
        ctx.SaveChanges();

        ctx.TenantSettings.AddRange(
            new TenantSetting { TenantId = t1.Id, Key = "locale", Value = "zh-CN" },
            new TenantSetting { TenantId = t2.Id, Key = "locale", Value = "en-US" });
        ctx.SaveChanges();

        ctx.BusinessEntities.AddRange(
            new BusinessEntity { TenantId = t1.Id, BusinessKey = "BE1", Name = "Customer" },
            new BusinessEntity { TenantId = t2.Id, BusinessKey = "BE2", Name = "Supplier" });
        ctx.SaveChanges();
    }

    [Fact]
    public void Golden_NoTenantScope_ReturnsAllRows_NoOp()
    {
        using var ctx = CreateContext(out var conn);
        Seed(ctx);

        // 全局过滤默认关闭：等价于恒真 no-op，可见全部租户数据。
        Assert.Equal(2, ctx.DataSources.Count());
        Assert.Equal(2, ctx.MetadataTables.Count());
        Assert.Equal(2, ctx.TenantSettings.Count());
        Assert.Equal(2, ctx.BusinessEntities.Count());
    }

    [Fact]
    public void TenantScope_OnlyOwnRowsVisible()
    {
        using var ctx = CreateContext(out var conn);
        Seed(ctx);
        var t1Id = ctx.Tenants.First(t => t.TenantCode == "T1").Id;

        ctx.ApplyTenantScope(t1Id);

        Assert.Equal(1, ctx.DataSources.Count());
        Assert.All(ctx.DataSources.ToList(), x => Assert.True(x.TenantId == t1Id));
        Assert.Equal(1, ctx.MetadataTables.Count());
        Assert.All(ctx.MetadataTables.ToList(), x => Assert.True(x.TenantId == t1Id));
        Assert.Equal(1, ctx.TenantSettings.Count());
        Assert.All(ctx.TenantSettings.ToList(), x => Assert.True(x.TenantId == t1Id));
        Assert.Equal(1, ctx.BusinessEntities.Count());
        Assert.All(ctx.BusinessEntities.ToList(), x => Assert.True(x.TenantId == t1Id));
    }

    [Fact]
    public void TenantScope_OtherTenantInvisible()
    {
        using var ctx = CreateContext(out var conn);
        Seed(ctx);
        var t2Id = ctx.Tenants.First(t => t.TenantCode == "T2").Id;

        ctx.ApplyTenantScope(t2Id);

        // 仅租户2可见，租户1彻底不可见。
        Assert.Equal(1, ctx.DataSources.Count());
        Assert.All(ctx.DataSources.ToList(), x => Assert.True(x.TenantId == t2Id));
        Assert.Equal(0, ctx.BusinessEntities.Count(x => x.TenantId != t2Id));
        Assert.Equal(0, ctx.MetadataTables.Count(x => x.TenantId != t2Id));
    }

    [Fact]
    public void SystemScope_TenantIdZero_IsNoOp()
    {
        using var ctx = CreateContext(out var conn);
        Seed(ctx);

        // 系统/全局上下文（tenantId <= 0）等价于 no-op，不做任何隔离。
        ctx.ApplyTenantScope(0);
        Assert.Equal(2, ctx.DataSources.Count());
        Assert.Equal(2, ctx.BusinessEntities.Count());
        Assert.Equal(2, ctx.MetadataTables.Count());
        Assert.Equal(2, ctx.TenantSettings.Count());
    }
}
