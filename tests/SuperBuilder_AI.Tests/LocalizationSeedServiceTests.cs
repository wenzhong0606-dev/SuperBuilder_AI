using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models.Localization;
using SuperBuilder_AI.Services.Platform;
using Xunit;

namespace SuperBuilder_AI.Tests;

public sealed class LocalizationSeedServiceTests
{
    [Fact]
    public async Task EnsureSeedAsync_SeedsSupportedLanguagesAndDefaultText()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
        await using var db = new SuperBIContext(options);
        await db.Database.EnsureCreatedAsync();

        var service = new LocalizationSeedService(db, new LocalizationService());
        await service.EnsureSeedAsync();

        var cultures = await db.UiLanguages.Select(x => x.Culture).ToListAsync();
        Assert.Contains("zh-CN", cultures);
        Assert.Contains("en-US", cultures);

        var loginTitle = await db.UiTextResources
            .Where(x => x.TenantId == 0 && x.Culture == "zh-CN" && x.ResourceKey == "Login.Title")
            .Select(x => x.Value).SingleAsync();
        Assert.Equal("登录 / 租户选择", loginTitle);

        // 幂等：再次执行不应产生重复行
        await service.EnsureSeedAsync();
        var count = await db.UiTextResources
            .CountAsync(x => x.TenantId == 0 && x.Culture == "zh-CN" && x.ResourceKey == "Login.Title");
        Assert.Equal(1, count);
    }
}
