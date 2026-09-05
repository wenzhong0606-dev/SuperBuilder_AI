using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models.Localization;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M3-04 纵深防御：UiTextResource 全局租户查询过滤器（放行 TenantId==0 平台基线）。
/// 验证未开启作用域时不过滤；开启后仅可见本租户覆盖 + 平台基线，跨租户行被隔离。
/// </summary>
public sealed class UiTextResourceQueryFilterTests
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

    [Fact]
    public async Task Filter_Disabled_When_Scope_Not_Applied()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn; await using var __ = db;
        db.UiTextResources.AddRange(
            new UiTextResource { TenantId = 0, Culture = "en-US", ResourceKey = "K", Value = "v0", IsTranslated = true },
            new UiTextResource { TenantId = 5, Culture = "en-US", ResourceKey = "K", Value = "v5", IsTranslated = true },
            new UiTextResource { TenantId = 10, Culture = "en-US", ResourceKey = "K", Value = "v10", IsTranslated = true });
        await db.SaveChangesAsync();

        // 未开启租户作用域：可见全部三行（含跨租户）。Global filter 为 no-op。
        var all = await db.UiTextResources.ToListAsync();
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task Filter_Enabled_Scopes_To_Tenant_And_PlatformBaseline()
    {
        var (db, conn) = await CreateContextAsync();
        await using var _ = conn; await using var __ = db;
        db.UiTextResources.AddRange(
            new UiTextResource { TenantId = 0, Culture = "en-US", ResourceKey = "K", Value = "v0", IsTranslated = true },
            new UiTextResource { TenantId = 5, Culture = "en-US", ResourceKey = "K", Value = "v5", IsTranslated = true },
            new UiTextResource { TenantId = 10, Culture = "en-US", ResourceKey = "K", Value = "v10", IsTranslated = true });
        await db.SaveChangesAsync();

        db.ApplyTenantScope(10);
        var visible = await db.UiTextResources.ToListAsync();
        // 仅本租户(10) + 平台基线(0)；租户 5 被隔离
        Assert.Equal(2, visible.Count);
        Assert.Contains(visible, x => x.TenantId == 0);
        Assert.Contains(visible, x => x.TenantId == 10);
        Assert.DoesNotContain(visible, x => x.TenantId == 5);

        // 关闭作用域后可再次见全部
        db.ApplyTenantScope(0);
        Assert.Equal(3, (await db.UiTextResources.ToListAsync()).Count);
    }
}
