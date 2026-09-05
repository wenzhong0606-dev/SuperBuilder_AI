using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SuperBuilder_AI.Controllers;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.Auth;
using SuperBuilder_AI.Services.Identity;
using SuperBuilder_AI.Services.Localization;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M0-08 登录租户目录枚举防护：<see cref="AuthController.LoginOptions"/> 默认隐藏完整租户目录，
/// 开启后排除 platform 租户、仅返回启用租户、支持按名称/编码搜索。
/// SQLite 内存库 + 真实 DbContext，不依赖 Moq。
/// </summary>
public sealed class AuthControllerLoginOptionsTests
{
    private static SuperBIContext CreateContext(out SqliteConnection connection)
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
        var ctx = new SuperBIContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static AuthController Build(SuperBIContext db, IConfiguration config)
        => new AuthController(db, new IdentityService(db, new PasswordHasher()), new TokenService("mw-test-key"), new PasswordHasher(), config, new TenantLanguageService(db, new NoopAuditService()));

    private static async Task SeedTenantsAsync(SuperBIContext db)
    {
        db.Tenants.AddRange(new Tenant { TenantCode = "acme", TenantName = "Acme Corp", Enabled = true });
        db.Tenants.AddRange(new Tenant { TenantCode = "globex", TenantName = "Globex Inc", Enabled = true });
        db.Tenants.AddRange(new Tenant { TenantCode = "platform", TenantName = "Platform", Enabled = true });
        db.Tenants.AddRange(new Tenant { TenantCode = "disabled", TenantName = "Disabled Co", Enabled = false });
        await db.SaveChangesAsync();
    }

    private static IConfiguration Config(bool showDirectory)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:ShowTenantDirectory"] = showDirectory.ToString().ToLowerInvariant()
            })
            .Build();

    [Fact]
    public async Task Hidden_ByDefault_Returns_Empty_Array()
    {
        var ctx = CreateContext(out var connection);
        await using var _ = connection;
        await using var __ = ctx;
        await SeedTenantsAsync(ctx);

        var ctrl = Build(ctx, Config(showDirectory: false));
        var result = await ctrl.LoginOptions(null, CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;

        Assert.NotNull(result);
        var items = Assert.IsAssignableFrom<System.Collections.IEnumerable>(result!.Value).Cast<object>().ToList();
        Assert.Empty(items);
    }

    [Fact]
    public async Task Shown_Excludes_Platform_And_Disabled_Tenants()
    {
        var ctx = CreateContext(out var connection);
        await using var _ = connection;
        await using var __ = ctx;
        await SeedTenantsAsync(ctx);

        var ctrl = Build(ctx, Config(showDirectory: true));
        var result = await ctrl.LoginOptions(null, CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;

        Assert.NotNull(result);
        var items = Assert.IsAssignableFrom<System.Collections.IEnumerable>(result!.Value).Cast<object>().ToList();
        Assert.Equal(2, items.Count);
        // 仅 acme / globex 两条启用且非 platform 租户
        Assert.All(items, item =>
        {
            var code = item.GetType().GetProperty("TenantCode")!.GetValue(item)!.ToString();
            Assert.NotEqual("platform", code);
            Assert.NotEqual("disabled", code);
        });
    }

    [Fact]
    public async Task Shown_Supports_NameOrCode_Search()
    {
        var ctx = CreateContext(out var connection);
        await using var _ = connection;
        await using var __ = ctx;
        await SeedTenantsAsync(ctx);

        var ctrl = Build(ctx, Config(showDirectory: true));
        var result = await ctrl.LoginOptions("glob", CancellationToken.None) as Microsoft.AspNetCore.Mvc.OkObjectResult;

        Assert.NotNull(result);
        var items = Assert.IsAssignableFrom<System.Collections.IEnumerable>(result!.Value).Cast<object>().ToList();
        Assert.Single(items);
        var code = items[0].GetType().GetProperty("TenantCode")!.GetValue(items[0])!.ToString();
        Assert.Equal("globex", code);
    }
}
