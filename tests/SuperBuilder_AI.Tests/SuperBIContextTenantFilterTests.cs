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

		var ds1 = new DataSource { TenantId = t1.Id, Name = "ds1", NormalizedName = "ds1", DbType = "SQLServer", ConnectionString = "cs1" };
		var ds2 = new DataSource { TenantId = t2.Id, Name = "ds2", NormalizedName = "ds2", DbType = "MySQL", ConnectionString = "cs2" };
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

        var be1 = new BusinessEntity { TenantId = t1.Id, BusinessKey = "BE1", Name = "Customer" };
        var be2 = new BusinessEntity { TenantId = t2.Id, BusinessKey = "BE2", Name = "Supplier" };
        ctx.BusinessEntities.AddRange(be1, be2);
        ctx.SaveChanges();

        // ---- DB-02 / M13-17：依赖实体（无自身 TenantId，须经父导航隔离）----
        // 每个父实体各挂一条属于租户1、一条属于租户2，用于验证「依赖实体直查」的跨租户隔离。
        var c1 = new MetadataColumn { MetadataTableId = mt1.Id, ColumnName = "OrderId" };
        var c2 = new MetadataColumn { MetadataTableId = mt2.Id, ColumnName = "ProductId" };
        ctx.MetadataColumns.AddRange(c1, c2);
        ctx.SaveChanges();

        ctx.MetadataScanJobs.AddRange(
            new MetadataScanJob { TenantId = t1.Id, DataSourceId = ds1.Id },
            new MetadataScanJob { TenantId = t2.Id, DataSourceId = ds2.Id });
        ctx.SaveChanges();

        ctx.BusinessEntityAttributes.AddRange(
            new BusinessEntityAttribute { BusinessEntityId = be1.Id, Name = "Attr1" },
            new BusinessEntityAttribute { BusinessEntityId = be2.Id, Name = "Attr2" });
        ctx.SaveChanges();

        ctx.BusinessEntityKeys.AddRange(
            new BusinessEntityKey { BusinessEntityId = be1.Id, Name = "Key1" },
            new BusinessEntityKey { BusinessEntityId = be2.Id, Name = "Key2" });
        ctx.SaveChanges();

        ctx.BusinessEntityMetrics.AddRange(
            new BusinessEntityMetric { BusinessEntityId = be1.Id, Name = "Metric1" },
            new BusinessEntityMetric { BusinessEntityId = be2.Id, Name = "Metric2" });
        ctx.SaveChanges();

        ctx.BusinessEntityRelationships.AddRange(
            new BusinessEntityRelationship { SourceEntityId = be1.Id, TargetEntityId = be1.Id, Name = "Rel1" },
            new BusinessEntityRelationship { SourceEntityId = be2.Id, TargetEntityId = be2.Id, Name = "Rel2" });
        ctx.SaveChanges();

        ctx.PhysicalBindings.AddRange(
            new PhysicalBinding { DataSourceId = ds1.Id, MetadataTableId = mt1.Id, MetadataColumnId = c1.Id },
            new PhysicalBinding { DataSourceId = ds2.Id, MetadataTableId = mt2.Id, MetadataColumnId = c2.Id });
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
        // DB-02 依赖实体在未开启作用域时同样可见全部（no-op 守卫不变）。
        Assert.Equal(2, ctx.MetadataColumns.Count());
        Assert.Equal(2, ctx.MetadataScanJobs.Count());
        Assert.Equal(2, ctx.BusinessEntityAttributes.Count());
        Assert.Equal(2, ctx.BusinessEntityKeys.Count());
        Assert.Equal(2, ctx.BusinessEntityMetrics.Count());
        Assert.Equal(2, ctx.BusinessEntityRelationships.Count());
        Assert.Equal(2, ctx.PhysicalBindings.Count());
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
        // DB-02 依赖实体：直接 DbSet 查询仅返回当前租户（经父导航隔离），跨租户不可见。
        Assert.Equal(1, ctx.MetadataColumns.Count());
        Assert.Equal(1, ctx.MetadataScanJobs.Count());
        Assert.Equal(1, ctx.BusinessEntityAttributes.Count());
        Assert.Equal(1, ctx.BusinessEntityKeys.Count());
        Assert.Equal(1, ctx.BusinessEntityMetrics.Count());
        Assert.Equal(1, ctx.BusinessEntityRelationships.Count());
        Assert.Equal(1, ctx.PhysicalBindings.Count());
    }

    [Fact]
    public void Db02_DependentEntities_TenantScoped_OnDirectQuery()
    {
        using var ctx = CreateContext(out var conn);
        Seed(ctx);
        var t1Id = ctx.Tenants.First(t => t.TenantCode == "T1").Id;

        ctx.ApplyTenantScope(t1Id);

        // 依赖实体直接 DbSet 查询：仅返回当前租户（经父导航隔离）的行，跨租户彻底不可见。
        Assert.Equal(1, ctx.MetadataColumns.Count());
        Assert.All(ctx.MetadataColumns.ToList(), c => Assert.True(c.MetadataTable!.TenantId == t1Id));
        Assert.Equal(1, ctx.MetadataScanJobs.Count());
        Assert.All(ctx.MetadataScanJobs.ToList(), j => Assert.True(j.TenantId == t1Id));
        Assert.Equal(1, ctx.BusinessEntityAttributes.Count());
        Assert.All(ctx.BusinessEntityAttributes.ToList(), a => Assert.True(a.BusinessEntity!.TenantId == t1Id));
        Assert.Equal(1, ctx.BusinessEntityKeys.Count());
        Assert.All(ctx.BusinessEntityKeys.ToList(), k => Assert.True(k.BusinessEntity!.TenantId == t1Id));
        Assert.Equal(1, ctx.BusinessEntityMetrics.Count());
        Assert.All(ctx.BusinessEntityMetrics.ToList(), m => Assert.True(m.BusinessEntity!.TenantId == t1Id));
        Assert.Equal(1, ctx.BusinessEntityRelationships.Count());
        Assert.All(ctx.BusinessEntityRelationships.ToList(), r => Assert.True(r.SourceEntity!.TenantId == t1Id));
        Assert.Equal(1, ctx.PhysicalBindings.Count());
        Assert.All(ctx.PhysicalBindings.ToList(), b => Assert.True(b.DataSource!.TenantId == t1Id));
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
