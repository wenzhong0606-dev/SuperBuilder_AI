using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models;
using SuperBuilder_AI.Models.Organization;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M1-01 验收：审计字段（UTC 时间、UpdatedTime 自动回填）与乐观并发（RowVersion/ETag）行为。
/// 数据库使用 SQLite（与 EF Core 提供程序一致，验证并发令牌在 Update/Delete 的 WHERE 中生效）。
/// </summary>
public class AuditConcurrencyTests
{
    [Fact]
    public void BaseEntity_CreatedTime_IsUtc()
    {
        var tenant = new Tenant();
        Assert.Equal(DateTimeKind.Utc, tenant.CreatedTime.Kind);
        Assert.True(tenant.CreatedTime <= DateTime.UtcNow.AddSeconds(2));
    }

    [Fact]
    public async Task AuditFields_AutoSet_OnModify()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;

        using (var ctx = new SuperBIContext(options))
        {
            ctx.Database.EnsureCreated();
            ctx.Tenants.Add(new Tenant { TenantCode = "audit-tc", TenantName = "orig" });
            await ctx.SaveChangesAsync();
        }

        using (var ctx = new SuperBIContext(options))
        {
            var t = ctx.Tenants.Single();
            Assert.Null(t.UpdatedTime);          // 从未更新
            Assert.Equal(1, t.RowVersion);        // 新增初始为 1
            t.TenantName = "updated";
            await ctx.SaveChangesAsync();
        }

        using (var ctx = new SuperBIContext(options))
        {
            var t = ctx.Tenants.Single();
            Assert.NotNull(t.UpdatedTime);
            Assert.Equal(DateTimeKind.Utc, t.UpdatedTime!.Value.Kind); // 写入期统一 UTC
            Assert.Equal(2, t.RowVersion);        // 修改后自增
        }
    }

    [Fact]
    public async Task RowVersion_ConcurrentUpdate_ThrowsDbUpdateConcurrencyException()
    {
        // 使用共享的同一开放连接（:memory:），使两个 DbContext 作用于同一库，避免文件锁。
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;

        using (var seed = new SuperBIContext(options))
        {
            seed.Database.EnsureCreated();
            seed.Tenants.Add(new Tenant { TenantCode = "cv-tc", TenantName = "orig" });
            await seed.SaveChangesAsync();
        }

        using var ctx1 = new SuperBIContext(options);
        using var ctx2 = new SuperBIContext(options);

        var t1 = ctx1.Tenants.Single();
        var t2 = ctx2.Tenants.Single();
        Assert.Equal(1, t1.RowVersion);
        Assert.Equal(1, t2.RowVersion);

        // 第一个请求成功提交，RowVersion 推进到 2。
        t1.TenantName = "from-ctx1";
        await ctx1.SaveChangesAsync();

        // 第二个请求基于过期 RowVersion=1 提交，应触发乐观并发冲突。
        t2.TenantName = "from-ctx2";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ctx2.SaveChangesAsync());
    }
}
