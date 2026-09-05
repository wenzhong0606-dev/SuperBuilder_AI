using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Controllers;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Localization;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Localization;
using SuperBuilder_AI.Services.Localization;
using SuperBuilder_AI.Services.Platform;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M3-02 平台语言维护：控制器的鉴权声明守卫与端到端路由验证（含新建语言复制键集合待翻译）。
/// 直接构造 <see cref="LocalizationController"/>（真实 DbContext + 真实服务），不依赖 Moq。
/// </summary>
public sealed class LocalizationControllerAdminTests
{
    private static async Task<(SuperBIContext db, SqliteConnection conn)> CreateContextAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
        var ctx = new SuperBIContext(options);
        await ctx.Database.EnsureCreatedAsync();
        return (ctx, connection);
    }

    private static LocalizationController Build(SuperBIContext db, bool withManageClaim)
    {
        var ctrl = new LocalizationController(
            new LocalizationService(),
            db,
            new LocalizationSeedService(db, new LocalizationService()),
            new TenantLanguageService(db, new NoopAuditService()),
            new PlatformLanguageService(db, new TenantLanguageService(db, new NoopAuditService()), new NoopAuditService()));
        var claims = new List<Claim> { new("uid", "1") };
        if (withManageClaim) claims.Add(new Claim("perm", IdentityPermissions.LocalizationManage));
        ctrl.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
        };
        return ctrl;
    }

    private static async Task SeedAsync(SuperBIContext db)
    {
        db.UiLanguages.AddRange(
            new UiLanguage { Culture = "zh-CN", DisplayName = "Chinese", NativeName = "中文", Enabled = true, SortOrder = 0 },
            new UiLanguage { Culture = "en-US", DisplayName = "English", NativeName = "English", Enabled = true, SortOrder = 1 },
            new UiLanguage { Culture = "fr-FR", DisplayName = "French", NativeName = "Français", Enabled = false, SortOrder = 2 });
        db.UiTextResources.AddRange(
            new UiTextResource { TenantId = 0, Culture = "en-US", ResourceKey = "A", Value = "A", IsTranslated = true },
            new UiTextResource { TenantId = 0, Culture = "en-US", ResourceKey = "B", Value = "B", IsTranslated = true },
            new UiTextResource { TenantId = 0, Culture = "en-US", ResourceKey = "C", Value = "C", IsTranslated = true });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task AdminLanguages_Without_Manage_Claim_Returns_Forbid()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn; await using var __ = db;
        await SeedAsync(db);
        var ctrl = Build(db, withManageClaim: false);
        var result = await ctrl.AdminLanguages(CancellationToken.None);
        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(result);
    }

    [Fact]
    public async Task AdminLanguages_Includes_Disabled_With_Correct_Enabled_Flag()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn; await using var __ = db;
        await SeedAsync(db);
        var ctrl = Build(db, withManageClaim: true);
        var result = await ctrl.AdminLanguages(CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
        Assert.NotNull(result);
        var langs = System.Linq.Enumerable.Cast<object>((System.Collections.IEnumerable)result!.Value).ToList();
        // 停用语言也必须出现（includeDisabled=true）
        var fr = langs.FirstOrDefault(l => l.GetType().GetProperty("Culture")!.GetValue(l)!.ToString() == "fr-FR");
        Assert.NotNull(fr);
        Assert.False((bool)fr!.GetType().GetProperty("Enabled")!.GetValue(fr)!);
    }

    [Fact]
    public async Task CreateLanguage_Persists_And_Copies_KeySet_AsUntranslated()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn; await using var __ = db;
        await SeedAsync(db);
        var ctrl = Build(db, withManageClaim: true);

        var create = await ctrl.CreateLanguage(
            new CreateUiLanguageRequest("de-DE", "德语", "Deutsch", "en-US"),
            CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
        Assert.NotNull(create);

        var newLang = await db.UiLanguages.AsNoTracking().FirstOrDefaultAsync(x => x.Culture == "de-DE");
        Assert.NotNull(newLang);

        // 控制器已确保种子补齐 en-US 基线键；新建语言须复制同等数量并标记待翻译。
        var enCount = await db.UiTextResources.AsNoTracking().CountAsync(x => x.TenantId == 0 && x.Culture == "en-US");
        var copied = await db.UiTextResources.AsNoTracking().Where(x => x.TenantId == 0 && x.Culture == "de-DE").ToListAsync();
        Assert.Equal(enCount, copied.Count);
        Assert.All(copied, r => Assert.False(r.IsTranslated));
    }

    [Fact]
    public async Task CreateLanguage_Invalid_Culture_Returns_BadRequest()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn; await using var __ = db;
        await SeedAsync(db);
        var ctrl = Build(db, withManageClaim: true);
        var result = await ctrl.CreateLanguage(
            new CreateUiLanguageRequest("not-a-culture", "X", "X", null),
            CancellationToken.None);
        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result);
    }
}
