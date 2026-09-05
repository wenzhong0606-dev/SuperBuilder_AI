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
/// M3-04 占位符一致性校验：保存租户覆盖译文时须与平台基线 {n} 占位符一致，否则拒绝，避免 string.Format 运行时异常。
/// 直接构造 <see cref="LocalizationController"/>（真实 DbContext + 真实服务），不依赖 Moq。
/// </summary>
public sealed class LocalizationControllerTextsTests
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

    private static LocalizationController Build(SuperBIContext db, long tenantId, params string[] perms)
    {
        var ctrl = new LocalizationController(
            new LocalizationService(),
            db,
            new LocalizationSeedService(db, new LocalizationService()),
            new TenantLanguageService(db, new NoopAuditService()),
            new PlatformLanguageService(db, new TenantLanguageService(db, new NoopAuditService()), new NoopAuditService()));
        var claims = new List<Claim> { new("tid", tenantId.ToString()) };
        foreach (var p in perms) claims.Add(new Claim("perm", p));
        ctrl.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
        };
        return ctrl;
    }

    private static async Task SeedBaselineAsync(SuperBIContext db, string key, string value)
    {
        db.UiTextResources.Add(new UiTextResource { TenantId = 0, Culture = "en-US", ResourceKey = key, Value = value, IsTranslated = true });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task SaveText_TenantOverride_MatchingPlaceholders_Succeeds()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn; await using var __ = db;
        await SeedBaselineAsync(db, "Common.Greeting", "Hello {0}");

        var ctrl = Build(db, tenantId: 10, IdentityPermissions.LocalizationView);
        var result = await ctrl.SaveText("en-US", "Common.Greeting", 10,
            new SaveUiTextRequest("Hi {0}"), CancellationToken.None);
        Assert.IsType<Microsoft.AspNetCore.Mvc.OkResult>(result);

        var saved = await db.UiTextResources.FirstOrDefaultAsync(x => x.TenantId == 10 && x.Culture == "en-US" && x.ResourceKey == "Common.Greeting");
        Assert.NotNull(saved);
        Assert.Equal("Hi {0}", saved!.Value);
    }

    [Fact]
    public async Task SaveText_TenantOverride_MismatchedPlaceholders_Rejected()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn; await using var __ = db;
        await SeedBaselineAsync(db, "Common.Greeting", "Hello {0}");

        var ctrl = Build(db, tenantId: 10, IdentityPermissions.LocalizationView);
        var result = await ctrl.SaveText("en-US", "Common.Greeting", 10,
            new SaveUiTextRequest("Hi there"), CancellationToken.None);
        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result);

        // 未写入
        var saved = await db.UiTextResources.FirstOrDefaultAsync(x => x.TenantId == 10 && x.Culture == "en-US" && x.ResourceKey == "Common.Greeting");
        Assert.Null(saved);
    }

    [Fact]
    public async Task SaveText_PlatformBaseline_SkipsPlaceholderCheck()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn; await using var __ = db;
        await SeedBaselineAsync(db, "Common.Greeting", "Hello {0}");

        var ctrl = Build(db, tenantId: 0, IdentityPermissions.LocalizationManage);
        // 平台基线（tenantId=0）无基线可对比，校验跳过；即使占位符不同也允许（这是新建/修正基线的入口）。
        var result = await ctrl.SaveText("en-US", "Common.Greeting", 0,
            new SaveUiTextRequest("Welcome {0} {1}"), CancellationToken.None);
        Assert.IsType<Microsoft.AspNetCore.Mvc.OkResult>(result);
    }
}
