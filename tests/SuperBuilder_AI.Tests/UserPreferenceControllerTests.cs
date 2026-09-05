using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Controllers;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Localization;
using SuperBuilder_AI.Models.Localization;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.Localization;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M3-G0 用户偏好控制器单测：仅认证用户可访问、越界文化回退租户默认。
/// </summary>
public sealed class UserPreferenceControllerTests
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

    private static ClaimsPrincipal Principal(long tenantId, long userId) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim("tid", tenantId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        }, "Bearer"));

    private static UserPreferenceController Build(SuperBIContext db, ClaimsPrincipal? user = null)
    {
        var ctrl = new UserPreferenceController(new UserLanguagePreferenceService(db, new NoopAuditService(), new TenantLanguageService(db, new NoopAuditService())));
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user ?? new ClaimsPrincipal() }
        };
        return ctrl;
    }

    [Fact]
    public async Task Get_RequiresAuthentication_Returns401_WhenAnonymous()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;
        var ctrl = Build(db, null);
        var result = await ctrl.GetLanguage(CancellationToken.None);
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Set_And_Get_RoundTrips_ForAuthenticatedUser()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;
        var ctrl = Build(db, Principal(10, 42));
        var setResult = await ctrl.SetLanguage(new SetLanguageRequest("en-US"), CancellationToken.None) as OkObjectResult;
        Assert.NotNull(setResult);

        var getResult = await ctrl.GetLanguage(CancellationToken.None) as OkObjectResult;
        Assert.NotNull(getResult);
        var culture = getResult!.Value!.GetType().GetProperty("culture")!.GetValue(getResult.Value)!.ToString();
        Assert.Equal("en-US", culture);
    }

    [Fact]
    public async Task Set_OutOfRange_FallsBackToDefault_ButStill200()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;
        var ctrl = Build(db, Principal(10, 42));
        var result = await ctrl.SetLanguage(new SetLanguageRequest("fr-FR"), CancellationToken.None) as OkObjectResult;
        Assert.NotNull(result);
        var culture = result!.Value!.GetType().GetProperty("culture")!.GetValue(result.Value)!.ToString();
        Assert.Equal("zh-CN", culture);
    }

    [Fact]
    public async Task Get_ReturnsTenantDefault_WhenStoredLanguageDisabled()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;
        var ctrl = Build(db, Principal(10, 42));
        // 用户设置启用中的 en-US。
        await ctrl.SetLanguage(new SetLanguageRequest("en-US"), CancellationToken.None);
        // 租户停用 en-US（M3-06：原语言被停用）。
        var rel = await db.TenantUiLanguages.FirstAsync(x => x.TenantId == 10 && x.UiLanguageId == 2, CancellationToken.None);
        rel.Enabled = false;
        await db.SaveChangesAsync();
        // API 应返回有效（回退后的租户默认）文化，而非已停用的 en-US。
        var getResult = await ctrl.GetLanguage(CancellationToken.None) as OkObjectResult;
        Assert.NotNull(getResult);
        var culture = getResult!.Value!.GetType().GetProperty("culture")!.GetValue(getResult.Value)!.ToString();
        Assert.Equal("zh-CN", culture);
    }
}
