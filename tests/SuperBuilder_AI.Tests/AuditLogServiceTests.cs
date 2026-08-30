using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Audit;
using SuperBuilder_AI.Services.Audit;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// P10.3 AuditLog 服务单测（SQLite 内存库，不依赖 Moq）。
/// 覆盖：记录写入、租户作用域隔离（本租户 + 全局0 可见）、按 action/entityType 过滤、时间/limit 过滤。
/// </summary>
public class AuditLogServiceTests
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

    private static AuditLogService CreateService(SuperBIContext ctx) => new(ctx);

    [Fact]
    public async Task LogAsync_Writes_And_Returns_Id()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        var id = await svc.LogAsync(new AuditLogEntry(Tenant100, "user.create", "User", UserId: 1, Actor: "alice"));
        Assert.True(id > 0);
        var stored = await ctx.AuditLogs.FirstAsync(a => a.Id == id);
        Assert.Equal(Tenant100, stored.TenantId);
        Assert.Equal("user.create", stored.Action);
        Assert.Equal("success", stored.Result);
    }

    [Fact]
    public async Task Query_TenantScoped_OnlyOwn_And_Global()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        await svc.LogAsync(new AuditLogEntry(Tenant100, "user.create", "User"));
        await svc.LogAsync(new AuditLogEntry(Tenant200, "user.create", "User"));
        await svc.LogAsync(new AuditLogEntry(0, "system.boot", "System")); // 全局

        var items = await svc.QueryAsync(new AuditLogQuery(TenantId: Tenant100));
        Assert.Equal(2, items.Count); // 本租户 + 全局
        Assert.Contains(items, a => a.TenantId == Tenant100);
        Assert.Contains(items, a => a.TenantId == 0);
        Assert.DoesNotContain(items, a => a.TenantId == Tenant200);
    }

    [Fact]
    public async Task Query_CrossTenant_Not_Visible()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        await svc.LogAsync(new AuditLogEntry(Tenant200, "role.assign", "Role"));
        var items = await svc.QueryAsync(new AuditLogQuery(TenantId: Tenant100));
        Assert.Empty(items);
    }

    [Fact]
    public async Task Query_Filter_By_Action()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        await svc.LogAsync(new AuditLogEntry(Tenant100, "user.create", "User"));
        await svc.LogAsync(new AuditLogEntry(Tenant100, "role.assign", "Role"));
        var items = await svc.QueryAsync(new AuditLogQuery(TenantId: Tenant100, Action: "role.assign"));
        Assert.Single(items);
        Assert.Equal("role.assign", items[0].Action);
    }

    [Fact]
    public async Task Query_Filter_By_EntityType()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        await svc.LogAsync(new AuditLogEntry(Tenant100, "create", "User"));
        await svc.LogAsync(new AuditLogEntry(Tenant100, "create", "Dashboard"));
        var items = await svc.QueryAsync(new AuditLogQuery(TenantId: Tenant100, EntityType: "Dashboard"));
        Assert.Single(items);
        Assert.Equal("Dashboard", items[0].EntityType);
    }

    [Fact]
    public async Task Query_Respects_Limit_And_Order()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        var baseT = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
            await svc.LogAsync(new AuditLogEntry(Tenant100, "act", "X", Timestamp: baseT.AddMinutes(i)));
        var items = await svc.QueryAsync(new AuditLogQuery(TenantId: Tenant100, Limit: 3));
        Assert.Equal(3, items.Count);
        // 时间倒序：最新的在前
        Assert.True(items[0].Timestamp >= items[1].Timestamp);
        Assert.True(items[1].Timestamp >= items[2].Timestamp);
    }

    [Fact]
    public async Task Query_Filter_By_TimeRange()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        var baseT = DateTime.UtcNow;
        await svc.LogAsync(new AuditLogEntry(Tenant100, "act", "X", Timestamp: baseT.AddMinutes(-10)));
        await svc.LogAsync(new AuditLogEntry(Tenant100, "act", "X", Timestamp: baseT.AddMinutes(10)));
        var items = await svc.QueryAsync(new AuditLogQuery(TenantId: Tenant100, From: baseT.AddMinutes(-1), To: baseT.AddMinutes(20)));
        Assert.Single(items);
        Assert.True(items[0].Timestamp > baseT);
    }

    [Fact]
    public async Task LogAsync_Rejects_Negative_Tenant()
    {
        using var ctx = CreateContext(out var conn);
        var svc = CreateService(ctx);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.LogAsync(new AuditLogEntry(-1, "x", "Y")));
    }
}
