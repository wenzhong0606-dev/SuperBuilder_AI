using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Controllers;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.Auth;
using SuperBuilder_AI.Services.Identity;
using SuperBuilder_AI.Services.Localization;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M6 登录兜底：<see cref="AuthController.TenantByCode"/> 按已知编码解析租户（不枚举目录），
/// 排除 platform 与已停用租户；空编码返回 400、不存在/停用返回 404、命中返回 200 + Id/Code/Name。
/// SQLite 内存库 + 真实 DbContext，不依赖 Moq。
/// </summary>
public sealed class AuthControllerTenantByCodeTests
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
        => new AuthController(db, new IdentityService(db, new PasswordHasher()), new TokenService("mw-test-key"), new PasswordHasher(), config, new TenantLanguageService(db, new NoopAuditService()), new RefreshTokenStore(db, config));

    private static async Task SeedTenantsAsync(SuperBIContext db)
    {
        db.Tenants.AddRange(new Tenant { TenantCode = "acme", TenantName = "Acme Corp", Enabled = true });
        db.Tenants.AddRange(new Tenant { TenantCode = "platform", TenantName = "Platform", Enabled = true });
        db.Tenants.AddRange(new Tenant { TenantCode = "disabled", TenantName = "Disabled Co", Enabled = false });
        await db.SaveChangesAsync();
    }

    private static IConfiguration Config()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:ShowTenantDirectory"] = "false" })
            .Build();

    private static long IdOf(object value) => (long)value.GetType().GetProperty("Id")!.GetValue(value)!;

    [Fact]
    public async Task Empty_Code_Returns_BadRequest()
    {
        var ctx = CreateContext(out var connection);
        await using var _ = connection;
        await using var __ = ctx;
        await SeedTenantsAsync(ctx);

        var ctrl = Build(ctx, Config());
        var result = await ctrl.TenantByCode("", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Existing_Enabled_Tenant_Returns_Ok_With_Id()
    {
        var ctx = CreateContext(out var connection);
        await using var _ = connection;
        await using var __ = ctx;
        await SeedTenantsAsync(ctx);

        var ctrl = Build(ctx, Config());
        var result = await ctrl.TenantByCode("acme", CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        Assert.True(IdOf(result!.Value!) > 0);
        Assert.Equal("acme", result.Value!.GetType().GetProperty("TenantCode")!.GetValue(result.Value)!.ToString());
    }

    [Fact]
    public async Task Disabled_Tenant_Returns_NotFound()
    {
        var ctx = CreateContext(out var connection);
        await using var _ = connection;
        await using var __ = ctx;
        await SeedTenantsAsync(ctx);

        var ctrl = Build(ctx, Config());
        var result = await ctrl.TenantByCode("disabled", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Platform_Tenant_Returns_NotFound()
    {
        var ctx = CreateContext(out var connection);
        await using var _ = connection;
        await using var __ = ctx;
        await SeedTenantsAsync(ctx);

        var ctrl = Build(ctx, Config());
        var result = await ctrl.TenantByCode("platform", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Unknown_Code_Returns_NotFound()
    {
        var ctx = CreateContext(out var connection);
        await using var _ = connection;
        await using var __ = ctx;
        await SeedTenantsAsync(ctx);

        var ctrl = Build(ctx, Config());
        var result = await ctrl.TenantByCode("nope", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
