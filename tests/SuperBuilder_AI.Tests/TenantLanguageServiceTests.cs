using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Localization;
using SuperBuilder_AI.Models.Localization;
using SuperBuilder_AI.Services.Localization;
using SuperBuilder_AI.Models.Organization;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M3-01 租户语言关系服务单测：约束校验、JSON 迁移、幂等播种、平台语言停用迁移。
/// SQLite 内存库 + 真实 DbContext + 真实服务；审计用 TenantMembershipTests 中的 NoopAuditService。
/// </summary>
public sealed class TenantLanguageServiceTests
{
    private static async Task<(SuperBIContext db, SqliteConnection conn)> CreateContextAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
        var ctx = new SuperBIContext(options);
        await ctx.Database.EnsureCreatedAsync();
        // 平台语言目录：五文化全启用 + 一个停用语言（fr-FR）
        ctx.UiLanguages.AddRange(
            new UiLanguage { Id = 1, Culture = "zh-CN", DisplayName = "中文", NativeName = "简体中文", Enabled = true, SortOrder = 0 },
            new UiLanguage { Id = 2, Culture = "en-US", DisplayName = "English", NativeName = "English", Enabled = true, SortOrder = 1 },
            new UiLanguage { Id = 3, Culture = "zh-TW", DisplayName = "繁體中文", NativeName = "繁體中文", Enabled = true, SortOrder = 2 },
            new UiLanguage { Id = 4, Culture = "ja-JP", DisplayName = "日本語", NativeName = "日本語", Enabled = true, SortOrder = 3 },
            new UiLanguage { Id = 5, Culture = "ko-KR", DisplayName = "한국어", NativeName = "한국어", Enabled = true, SortOrder = 4 },
            new UiLanguage { Id = 6, Culture = "fr-FR", DisplayName = "Français", NativeName = "Français", Enabled = false, SortOrder = 5 });
        ctx.Tenants.AddRange(
            new Tenant { Id = 10, TenantCode = "t10", TenantName = "Tenant 10", Enabled = true },
            new Tenant { Id = 20, TenantCode = "t20", TenantName = "Tenant 20", Enabled = true });
        await ctx.SaveChangesAsync();
        return (ctx, connection);
    }

    private static TenantLanguageService Build(SuperBIContext db) => new(db, new NoopAuditService());

    // === SetLanguagesAsync 约束校验 ===

    [Fact]
    public async Task SetLanguagesAsync_Empty_Throws()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;
        await Assert.ThrowsAsync<TenantLanguageException>(() =>
            Build(db).SetLanguagesAsync(10, new List<TenantLanguageUpdate>(), 1, CancellationToken.None));
    }

    [Fact]
    public async Task SetLanguagesAsync_NoEnabled_Throws()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;
        var updates = new List<TenantLanguageUpdate> { new(1, Enabled: false, IsDefault: false, SortOrder: 0) };
        await Assert.ThrowsAsync<TenantLanguageException>(() =>
            Build(db).SetLanguagesAsync(10, updates, 1, CancellationToken.None));
    }

    [Fact]
    public async Task SetLanguagesAsync_MultipleDefaults_Throws()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;
        var updates = new List<TenantLanguageUpdate>
        {
            new(1, Enabled: true, IsDefault: true, SortOrder: 0),
            new(2, Enabled: true, IsDefault: true, SortOrder: 1),
        };
        await Assert.ThrowsAsync<TenantLanguageException>(() =>
            Build(db).SetLanguagesAsync(10, updates, 1, CancellationToken.None));
    }

    [Fact]
    public async Task SetLanguagesAsync_DefaultMustBeEnabled_Throws()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;
        var updates = new List<TenantLanguageUpdate>
        {
            new(1, Enabled: false, IsDefault: true, SortOrder: 0),
            new(2, Enabled: true, IsDefault: false, SortOrder: 1),
        };
        await Assert.ThrowsAsync<TenantLanguageException>(() =>
            Build(db).SetLanguagesAsync(10, updates, 1, CancellationToken.None));
    }

    [Fact]
    public async Task SetLanguagesAsync_DisabledPlatformLanguage_Throws()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;
        var updates = new List<TenantLanguageUpdate>
        {
            new(6, Enabled: true, IsDefault: true, SortOrder: 0), // fr-FR 平台已停用
        };
        await Assert.ThrowsAsync<TenantLanguageException>(() =>
            Build(db).SetLanguagesAsync(10, updates, 1, CancellationToken.None));
    }

    [Fact]
    public async Task SetLanguagesAsync_HappyPath_WritesSingleDefault()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;
        var updates = new List<TenantLanguageUpdate>
        {
            new(1, Enabled: true, IsDefault: true, SortOrder: 0),
            new(2, Enabled: true, IsDefault: false, SortOrder: 1),
        };
        await Build(db).SetLanguagesAsync(10, updates, 1, CancellationToken.None);

        var rows = await db.TenantUiLanguages.Where(x => x.TenantId == 10).OrderBy(x => x.SortOrder).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.True(rows[0].IsDefault && rows[0].UiLanguageId == 1);
        Assert.False(rows[1].IsDefault && rows[1].UiLanguageId == 2);
        Assert.Equal("zh-CN", await Build(db).GetDefaultCultureAsync(10, CancellationToken.None));
    }

    [Fact]
    public async Task SetLanguagesAsync_ReplacesExistingConfig()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;
        await Build(db).SetLanguagesAsync(10, new[]
        {
            new TenantLanguageUpdate(1, Enabled: true, IsDefault: true, SortOrder: 0),
            new TenantLanguageUpdate(2, Enabled: true, IsDefault: false, SortOrder: 1),
        }, 1, CancellationToken.None);

        // 改为仅 en-US 且为默认
        await Build(db).SetLanguagesAsync(10, new[]
        {
            new TenantLanguageUpdate(2, Enabled: true, IsDefault: true, SortOrder: 0),
        }, 1, CancellationToken.None);

        var rows = await db.TenantUiLanguages.Where(x => x.TenantId == 10).ToListAsync();
        Assert.Single(rows);
        Assert.True(rows[0].IsDefault && rows[0].UiLanguageId == 2);
        Assert.Equal("en-US", await Build(db).GetDefaultCultureAsync(10, CancellationToken.None));
    }

    // === Ensure 播种（含 JSON 迁移） ===

    [Fact]
    public async Task EnsureTenantLanguagesAsync_FromJson_Migrates()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;
        // 遗留 localization:* JSON，无关系行
        db.TenantSettings.AddRange(
            new TenantSetting { TenantId = 10, Key = "localization:availableCultures", Value = "[\"zh-CN\",\"en-US\"]", IsLocked = true },
            new TenantSetting { TenantId = 10, Key = "localization:defaultCulture", Value = "zh-CN", IsLocked = true });
        await db.SaveChangesAsync();

        await Build(db).EnsureTenantLanguagesAsync(10, CancellationToken.None);

        var rows = await db.TenantUiLanguages.Where(x => x.TenantId == 10).OrderBy(x => x.SortOrder).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.True(rows[0].IsDefault && rows[0].UiLanguageId == 1);
        Assert.False(rows[1].IsDefault && rows[1].UiLanguageId == 2);
    }

    [Fact]
    public async Task EnsureTenantLanguagesAsync_FromDefault_WhenNoJson()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;
        // 无 JSON、无关系行 → 回退平台默认 zh-CN
        await Build(db).EnsureTenantLanguagesAsync(20, CancellationToken.None);

        var rows = await db.TenantUiLanguages.Where(x => x.TenantId == 20).ToListAsync();
        Assert.Single(rows);
        Assert.True(rows[0].IsDefault && rows[0].UiLanguageId == 1);
    }

    [Fact]
    public async Task EnsureTenantLanguagesAsync_Idempotent()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;
        await Build(db).EnsureTenantLanguagesAsync(10, CancellationToken.None);
        await Build(db).EnsureTenantLanguagesAsync(10, CancellationToken.None);
        Assert.Equal(1, await db.TenantUiLanguages.CountAsync(x => x.TenantId == 10));
    }

    [Fact]
    public async Task EnsureAllTenantsLanguagesAsync_SeedsAllTenants()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;
        await Build(db).EnsureAllTenantsLanguagesAsync(CancellationToken.None);
        Assert.Equal(1, await db.TenantUiLanguages.CountAsync(x => x.TenantId == 10));
        Assert.Equal(1, await db.TenantUiLanguages.CountAsync(x => x.TenantId == 20));
    }

    // === 平台语言停用迁移 ===

    [Fact]
    public async Task DisablePlatformLanguageAsync_MigratesDefaultTenants_AndDeactivatesRows()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;
        // t10：zh-CN 默认 + en-US 启用；t20：仅有 zh-CN 默认
        await Build(db).SetLanguagesAsync(10, new[]
        {
            new TenantLanguageUpdate(1, Enabled: true, IsDefault: true, SortOrder: 0),
            new TenantLanguageUpdate(2, Enabled: true, IsDefault: false, SortOrder: 1),
        }, 1, CancellationToken.None);
        await Build(db).SetLanguagesAsync(20, new[]
        {
            new TenantLanguageUpdate(1, Enabled: true, IsDefault: true, SortOrder: 0),
        }, 1, CancellationToken.None);

        var result = await Build(db).DisablePlatformLanguageAsync("zh-CN", 1, CancellationToken.None);

        Assert.Equal(2, result.AffectedTenantIds.Count);
        Assert.Contains(10L, result.AffectedTenantIds);
        Assert.Contains(20L, result.AffectedTenantIds);
        Assert.Equal(2, result.MigratedTenantIds.Count);

        // t10 默认迁移到 en-US；t20 新建 en-US 默认
        Assert.Equal("en-US", await Build(db).GetDefaultCultureAsync(10, CancellationToken.None));
        Assert.Equal("en-US", await Build(db).GetDefaultCultureAsync(20, CancellationToken.None));
        // 停用的 zh-CN 不再出现在任一租户的可用语言中
        var avail10 = await Build(db).GetAvailableCulturesAsync(10, CancellationToken.None);
        var avail20 = await Build(db).GetAvailableCulturesAsync(20, CancellationToken.None);
        Assert.DoesNotContain("zh-CN", avail10);
        Assert.DoesNotContain("zh-CN", avail20);
        // 平台语言目录项已停用
        Assert.False((await db.UiLanguages.FirstAsync(u => u.Culture == "zh-CN")).Enabled);
    }

    [Fact]
    public async Task DisablePlatformLanguageAsync_AlreadyDisabled_ReturnsEmpty()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;
        var result = await Build(db).DisablePlatformLanguageAsync("fr-FR", 1, CancellationToken.None);
        Assert.Empty(result.AffectedTenantIds);
        Assert.Empty(result.MigratedTenantIds);
    }
}
