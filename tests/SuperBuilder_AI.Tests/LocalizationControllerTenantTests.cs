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
using SuperBuilder_AI.Models.Localization;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.Localization;
using SuperBuilder_AI.Services.Platform;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M3-03 租户文本覆盖：核心不变量——租户管理员只见其被授权语言（基于 <see cref="TenantUiLanguage"/> 关系），
/// 即便某语言在平台层启用也不得出现在租户语言目录中。直接构造 <see cref="LocalizationController"/>（真实 DbContext）。
/// </summary>
public sealed class LocalizationControllerTenantTests
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

    private static LocalizationController Build(SuperBIContext db, long tenantId, bool platformView)
    {
        var ctrl = new LocalizationController(
            new LocalizationService(),
            db,
            new LocalizationSeedService(db, new LocalizationService()),
            new TenantLanguageService(db, new NoopAuditService()),
            new PlatformLanguageService(db, new TenantLanguageService(db, new NoopAuditService()), new NoopAuditService()));
        var claims = new List<Claim> { new("tid", tenantId.ToString()) };
        if (platformView) claims.Add(new Claim("perm", "platform:tenant:view"));
        ctrl.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
        };
        return ctrl;
    }

    [Fact]
    public async Task Languages_Tenant_Sees_Only_Authorized_Cultures()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn; await using var __ = db;

        // 平台层启用两种语言，但租户 10 仅授权 zh-CN
        db.Tenants.Add(new Tenant { Id = 10, TenantCode = "t10", TenantName = "Tenant 10", Enabled = true });
        db.UiLanguages.AddRange(
            new UiLanguage { Culture = "zh-CN", DisplayName = "Chinese", NativeName = "中文", Enabled = true, SortOrder = 0 },
            new UiLanguage { Culture = "en-US", DisplayName = "English", NativeName = "English", Enabled = true, SortOrder = 1 });
        db.TenantUiLanguages.Add(new TenantUiLanguage
        {
            TenantId = 10, UiLanguageId = 1, Enabled = true, IsDefault = true, SortOrder = 0
        });
        await db.SaveChangesAsync();

        var ctrl = Build(db, tenantId: 10, platformView: false);
        var result = await ctrl.Languages(CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
        Assert.NotNull(result);

        var cultures = System.Linq.Enumerable.Cast<object>((System.Collections.IEnumerable)result!.Value)
            .Select(o => o.GetType().GetProperty("Culture")!.GetValue(o)!.ToString())
            .ToList();
        // 即便 en-US 平台启用，租户 10 未授权 → 不得出现；仅 zh-CN
        Assert.DoesNotContain("en-US", cultures);
        Assert.Contains("zh-CN", cultures);
        Assert.Single(cultures);
    }

    [Fact]
    public async Task Languages_Platform_Sees_All_Enabled_Cultures()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn; await using var __ = db;

        db.UiLanguages.AddRange(
            new UiLanguage { Culture = "zh-CN", DisplayName = "Chinese", NativeName = "中文", Enabled = true, SortOrder = 0 },
            new UiLanguage { Culture = "en-US", DisplayName = "English", NativeName = "English", Enabled = true, SortOrder = 1 });
        await db.SaveChangesAsync();

        var ctrl = Build(db, tenantId: 0, platformView: true);
        var result = await ctrl.Languages(CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;
        Assert.NotNull(result);
        var cultures = System.Linq.Enumerable.Cast<object>((System.Collections.IEnumerable)result!.Value)
            .Select(o => o.GetType().GetProperty("Culture")!.GetValue(o)!.ToString())
            .ToList();
        Assert.Contains("zh-CN", cultures);
        Assert.Contains("en-US", cultures);
    }
}
