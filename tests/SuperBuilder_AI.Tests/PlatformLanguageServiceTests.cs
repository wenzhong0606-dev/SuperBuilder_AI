using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Localization;
using SuperBuilder_AI.Models.Localization;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.Localization;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M3-02 平台语言维护服务单测：BCP 47 归一化、唯一性、必填名、复制键集合标记“待翻译”、
/// 启停委托租户关系迁移、排序、统计。
/// SQLite 内存库 + 真实 DbContext + 真实服务；审计用 NoopAuditService。
/// </summary>
public sealed class PlatformLanguageServiceTests
{
    private static async Task<(SuperBIContext db, SqliteConnection conn)> CreateContextAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
        var ctx = new SuperBIContext(options);
        await ctx.Database.EnsureCreatedAsync();

        ctx.UiLanguages.AddRange(
            new UiLanguage { Id = 1, Culture = "zh-CN", DisplayName = "中文", NativeName = "简体中文", Enabled = true, SortOrder = 0 },
            new UiLanguage { Id = 2, Culture = "en-US", DisplayName = "English", NativeName = "English", Enabled = true, SortOrder = 1 },
            new UiLanguage { Id = 3, Culture = "fr-FR", DisplayName = "Français", NativeName = "Français", Enabled = false, SortOrder = 2 });
        // en-US 平台基线 3 键，作为复制源
        ctx.UiTextResources.AddRange(new[]
        {
            new UiTextResource { TenantId = 0, Culture = "en-US", ResourceKey = "Common.Login", Value = "Sign in", IsTranslated = true },
            new UiTextResource { TenantId = 0, Culture = "en-US", ResourceKey = "Common.Save", Value = "Save", IsTranslated = true },
            new UiTextResource { TenantId = 0, Culture = "en-US", ResourceKey = "Login.Title", Value = "Sign in", IsTranslated = true },
        });
        ctx.Tenants.Add(new Tenant { Id = 10, TenantCode = "t10", TenantName = "Tenant 10", Enabled = true });
        await ctx.SaveChangesAsync();
        return (ctx, connection);
    }

    private static PlatformLanguageService Build(SuperBIContext db)
        => new(db, new TenantLanguageService(db, new NoopAuditService()), new NoopAuditService());

    // === BCP 47 归一化 + 唯一性 ===

    [Fact]
    public async Task CreateLanguageAsync_Normalizes_Culture_And_Persists()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;

        var created = await Build(db).CreateLanguageAsync(
            new CreateUiLanguageRequest("es_es", "Español", "Español", "en-US"), 1, CancellationToken.None);

        Assert.Equal("es-ES", created.Culture); // 下划线归一化为连字符、语言小写地区大写
        var stored = await db.UiLanguages.AsNoTracking().SingleAsync(x => x.Culture == "es-ES");
        Assert.Equal("Español", stored.DisplayName);
        Assert.True(stored.Enabled);
    }

    [Fact]
    public async Task CreateLanguageAsync_InvalidCulture_Throws()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;
        await Assert.ThrowsAsync<PlatformLanguageException>(() =>
            Build(db).CreateLanguageAsync(new CreateUiLanguageRequest("!!!", "X", "X"), 1, CancellationToken.None));
    }

    [Fact]
    public async Task CreateLanguageAsync_DuplicateCulture_Throws()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;
        await Assert.ThrowsAsync<PlatformLanguageException>(() =>
            Build(db).CreateLanguageAsync(new CreateUiLanguageRequest("zh-cn", "中文", "简体中文"), 1, CancellationToken.None));
    }

    [Fact]
    public async Task CreateLanguageAsync_RequiresDisplayName_Throws()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;
        await Assert.ThrowsAsync<PlatformLanguageException>(() =>
            Build(db).CreateLanguageAsync(new CreateUiLanguageRequest("it-IT", "  ", "Italiano"), 1, CancellationToken.None));
    }

    // === 复制键集合标记“待翻译” ===

    [Fact]
    public async Task CreateLanguageAsync_CopiesKeySet_AsUntranslated()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;

        var created = await Build(db).CreateLanguageAsync(
            new CreateUiLanguageRequest("es-ES", "Español", "Español", "en-US"), 1, CancellationToken.None);

        var copied = await db.UiTextResources.AsNoTracking().Where(x => x.TenantId == 0 && x.Culture == "es-ES").ToListAsync();
        Assert.Equal(3, copied.Count); // 与 en-US 源一致
        Assert.All(copied, r => Assert.False(r.IsTranslated)); // 标记待翻译
        Assert.Equal(3, created.TotalKeys);
        Assert.Equal(0, created.TranslatedCount);
    }

    [Fact]
    public async Task CreateLanguageAsync_NoSource_StillCreatesLanguage()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;

        // 复制源不存在（zz-ZZ 无基线），语言仍应创建，只是无副本
        var created = await Build(db).CreateLanguageAsync(
            new CreateUiLanguageRequest("af-ZA", "Afrikaans", "Afrikaans", "zz-ZZ"), 1, CancellationToken.None);

        Assert.Equal("af-ZA", created.Culture);
        Assert.Equal(0, created.TotalKeys);
    }

    // === 列表/统计 ===

    [Fact]
    public async Task ListLanguagesAsync_ExcludesDisabled_WhenNotIncluding()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;

        var all = await Build(db).ListLanguagesAsync(true, CancellationToken.None);
        Assert.Contains(all.Languages, x => x.Culture == "fr-FR" && !x.Enabled);

        var enabledOnly = await Build(db).ListLanguagesAsync(false, CancellationToken.None);
        Assert.DoesNotContain(enabledOnly.Languages, x => x.Culture == "fr-FR");
        Assert.All(enabledOnly.Languages, x => Assert.True(x.Enabled));
    }

    [Fact]
    public async Task GetLanguageAsync_ReturnsNull_WhenMissing()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;
        Assert.Null(await Build(db).GetLanguageAsync(999, CancellationToken.None));
    }

    [Fact]
    public async Task GetLanguageAsync_ReturnsStats()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;
        var summary = await Build(db).GetLanguageAsync(2, CancellationToken.None); // en-US
        Assert.NotNull(summary);
        Assert.Equal(3, summary!.TotalKeys);
        Assert.Equal(3, summary.TranslatedCount);
    }

    // === 更新 ===

    [Fact]
    public async Task UpdateLanguageAsync_UpdatesNames()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;

        await Build(db).UpdateLanguageAsync(1, new UpdateUiLanguageRequest(DisplayName: "中文（简体）", NativeName: "漢語"), 1, CancellationToken.None);

        var updated = await db.UiLanguages.AsNoTracking().SingleAsync(x => x.Id == 1);
        Assert.Equal("中文（简体）", updated.DisplayName);
        Assert.Equal("漢語", updated.NativeName);
    }

    // === 启停委托租户关系迁移 ===

    [Fact]
    public async Task SetEnabledAsync_Disable_DelegatesTenantMigration()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;

        // 为租户 10 建立语言关系：T4 后 Ensure 已播种全部启用平台语言（zh-CN + en-US），
        // 故直接修改既有 en-US 行为默认（覆盖 Ensure 的 zh-CN 默认），用于验证停用迁移。
        // 注意：不可再 Add 重复 en-US 行，否则触发 (TenantId, UiLanguageId) 唯一约束。
        var tenantLang = new TenantLanguageService(db, new NoopAuditService());
        await tenantLang.EnsureTenantLanguagesAsync(10, CancellationToken.None);
        var zhRow = await db.TenantUiLanguages.SingleAsync(x => x.TenantId == 10 && x.UiLanguageId == 1, CancellationToken.None);
        zhRow.IsDefault = false;
        var enSetup = await db.TenantUiLanguages.SingleAsync(x => x.TenantId == 10 && x.UiLanguageId == 2, CancellationToken.None);
        enSetup.IsDefault = true;
        await db.SaveChangesAsync();

        // 停用平台 en-US（Id=2）
        await Build(db).SetEnabledAsync(2, false, 1, CancellationToken.None);

        var lang = await db.UiLanguages.AsNoTracking().SingleAsync(x => x.Id == 2);
        Assert.False(lang.Enabled);

        var tenantRows = await db.TenantUiLanguages.AsNoTracking().Where(x => x.TenantId == 10).ToListAsync();
        var enRow = tenantRows.Single(x => x.UiLanguageId == 2);
        Assert.False(enRow.Enabled);
        Assert.False(enRow.IsDefault);
        var newDefault = tenantRows.Single(x => x.IsDefault);
        Assert.Equal(1L, newDefault.UiLanguageId); // 迁移到 zh-CN
    }

    [Fact]
    public async Task SetEnabledAsync_Enable_ReEnablesPlatformLanguage()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;

        await Build(db).SetEnabledAsync(3, true, 1, CancellationToken.None); // fr-FR 原停用
        var lang = await db.UiLanguages.AsNoTracking().SingleAsync(x => x.Id == 3);
        Assert.True(lang.Enabled);
    }

    // === 排序 ===

    [Fact]
    public async Task ReorderLanguagesAsync_ReordersSortOrder()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;

        // 交换 zh-CN(1) 与 en-US(2) 的顺序
        await Build(db).ReorderLanguagesAsync(new List<long> { 2, 1, 3 }, 1, CancellationToken.None);

        var zh = await db.UiLanguages.AsNoTracking().SingleAsync(x => x.Id == 1);
        var en = await db.UiLanguages.AsNoTracking().SingleAsync(x => x.Id == 2);
        Assert.Equal(0, en.SortOrder); // en-US 排到第一位
        Assert.Equal(1, zh.SortOrder); // zh-CN 排到第二位
    }
}
