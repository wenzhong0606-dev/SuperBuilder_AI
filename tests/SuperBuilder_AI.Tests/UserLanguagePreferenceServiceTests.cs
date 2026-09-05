using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Audit;
using SuperBuilder_AI.Interfaces.Localization;
using SuperBuilder_AI.Models.Audit;
using SuperBuilder_AI.Models.Localization;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.Localization;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M3-G0「用户语言恢复」服务单测：偏好持久化、范围校验回退、幂等更新。
/// SQLite 内存库 + 真实 DbContext + 真实服务，审计用记录桩。
/// </summary>
public sealed class UserLanguagePreferenceServiceTests
{
    private static async Task<(SuperBIContext db, SqliteConnection conn)> CreateContextAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
        var ctx = new SuperBIContext(options);
        await ctx.Database.EnsureCreatedAsync();
        // 平台语言目录（M3-G0 五文化 + 一个停用语言）
        ctx.UiLanguages.AddRange(
            new UiLanguage { Id = 1, Culture = "zh-CN", DisplayName = "中文", NativeName = "简体中文", Enabled = true, SortOrder = 0 },
            new UiLanguage { Id = 2, Culture = "en-US", DisplayName = "English", NativeName = "English", Enabled = true, SortOrder = 1 },
            new UiLanguage { Id = 3, Culture = "fr-FR", DisplayName = "Français", NativeName = "Français", Enabled = false, SortOrder = 2 });
        ctx.Tenants.Add(new Tenant { Id = 10, TenantCode = "t10", TenantName = "Tenant 10", Enabled = true });
        // M3-01：租户语言关系（替代 localization:* JSON）
        ctx.TenantUiLanguages.AddRange(
            new TenantUiLanguage { TenantId = 10, UiLanguageId = 1, Enabled = true, IsDefault = true, SortOrder = 0 },
            new TenantUiLanguage { TenantId = 10, UiLanguageId = 2, Enabled = true, IsDefault = false, SortOrder = 1 });
        await ctx.SaveChangesAsync();
        return (ctx, connection);
    }

    [Fact]
    public async Task Get_ReturnsNull_WhenNotSet()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;
        var svc = new UserLanguagePreferenceService(db, new NoopAuditService(), new TenantLanguageService(db, new NoopAuditService()));
        Assert.Null(await svc.GetAsync(10, 42));
    }

    [Fact]
    public async Task Set_Persists_And_RoundTrips()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;
        var audit = new RecordingAuditService();
        var svc = new UserLanguagePreferenceService(db, audit, new TenantLanguageService(db, new NoopAuditService()));
        var effective = await svc.SetAsync(10, 42, "en-US");
        Assert.Equal("en-US", effective);
        Assert.Equal("en-US", await svc.GetAsync(10, 42));
        Assert.Single(await db.UserLanguagePreferences.Where(x => x.TenantId == 10 && x.UserId == 42).ToListAsync());
        Assert.Contains(audit.Events, e => e.Action == "user.language.set");
    }

    [Fact]
    public async Task Set_OutOfRange_FallsBackToTenantDefault()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;
        var svc = new UserLanguagePreferenceService(db, new NoopAuditService(), new TenantLanguageService(db, new NoopAuditService()));
        var effective = await svc.SetAsync(10, 42, "fr-FR");
        Assert.Equal("zh-CN", effective);
        Assert.Equal("zh-CN", await svc.GetAsync(10, 42));
    }

    [Fact]
    public async Task Set_Update_IsIdempotent_NotDuplicate()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;
        var svc = new UserLanguagePreferenceService(db, new NoopAuditService(), new TenantLanguageService(db, new NoopAuditService()));
        await svc.SetAsync(10, 42, "en-US");
        await svc.SetAsync(10, 42, "zh-CN");
        Assert.Equal(1, await db.UserLanguagePreferences.CountAsync(x => x.TenantId == 10 && x.UserId == 42));
        Assert.Equal("zh-CN", await svc.GetAsync(10, 42));
    }
}

/// <summary>记录审计事件的测试桩。</summary>
internal sealed class RecordingAuditService : IAuditLogService
{
    public System.Collections.Generic.List<AuditLogEntry> Events { get; } = new();
    public Task<long> LogAsync(AuditLogEntry entry, CancellationToken ct = default)
    {
        Events.Add(entry);
        return Task.FromResult(0L);
    }
    public Task<IReadOnlyList<AuditLog>> QueryAsync(AuditLogQuery query, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AuditLog>>(System.Array.Empty<AuditLog>());
}
