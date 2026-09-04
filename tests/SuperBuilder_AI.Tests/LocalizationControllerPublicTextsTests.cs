using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Controllers;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Services.Platform;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M0-08 匿名本地化端点防护：<see cref="LocalizationController.PublicTexts"/> 仅返回平台基线
/// （TenantId==0），不得按任意 tenantId 枚举租户专属文案。
/// SQLite 内存库 + 真实 DbContext，不依赖 Moq。
/// </summary>
public sealed class LocalizationControllerPublicTextsTests
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

    private static LocalizationController Build(SuperBIContext db)
        => new LocalizationController(new LocalizationService(), db, new LocalizationSeedService(db, new LocalizationService()));

    [Fact]
    public async Task PublicTexts_Returns_Only_Platform_Baseline_TenantZero()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = db;

        // 注入基线（TenantId=0）与租户专属（TenantId=5）两条同名键，值不同
        db.UiTextResources.Add(new SuperBuilder_AI.Models.Localization.UiTextResource
        {
            TenantId = 0,
            Culture = "zh-CN",
            ResourceKey = "Login.Title",
            Value = "BASE_VALUE"
        });
        db.UiTextResources.Add(new SuperBuilder_AI.Models.Localization.UiTextResource
        {
            TenantId = 5,
            Culture = "zh-CN",
            ResourceKey = "Login.Title",
            Value = "TENANT_VALUE"
        });
        await db.SaveChangesAsync();

        var ctrl = Build(db);
        var result = await ctrl.PublicTexts("zh-CN", CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;

        Assert.NotNull(result);
        var items = Assert.IsAssignableFrom<System.Collections.IEnumerable>(result!.Value)
            .Cast<object>()
            .Select(o => o.GetType().GetProperty("Value")!.GetValue(o)!.ToString())
            .ToList();
        // 租户专属文案绝不可经由匿名端点泄露
        Assert.DoesNotContain("TENANT_VALUE", items);
        Assert.Contains("BASE_VALUE", items);
    }
}
