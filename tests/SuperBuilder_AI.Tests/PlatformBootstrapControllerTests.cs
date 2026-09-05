using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Controllers;
using SuperBuilder_AI.Services.Auth;
using SuperBuilder_AI.Services.Identity;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>M2-03：安全初始化端点的关键护栏——匿名开关、本机 Loopback 强制、一次性关闭。</summary>
public sealed class PlatformBootstrapControllerTests
{
    private sealed class Fixture : IDisposable
    {
        public SqliteConnection Connection { get; }
        public SuperBIContext Db { get; }
        public PlatformAdminBootstrapper Bootstrapper { get; }

        public Fixture(IConfiguration config)
        {
            Connection = new SqliteConnection("DataSource=:memory:");
            Connection.Open();
            var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(Connection).Options;
            Db = new SuperBIContext(options);
            Db.Database.EnsureCreated();
            var hasher = new PasswordHasher();
            new IdentityService(Db, hasher).SeedAsync().GetAwaiter().GetResult();
            Bootstrapper = new PlatformAdminBootstrapper(Db, hasher, config);
        }

        public void Dispose()
        {
            Db.Dispose();
            Connection.Dispose();
        }
    }

    private static PlatformBootstrapController Build(IConfiguration config, PlatformAdminBootstrapper bootstrapper, IPAddress? remoteIp)
    {
        var ctrl = new PlatformBootstrapController(bootstrapper, config);
        var ctx = new DefaultHttpContext();
        if (remoteIp is not null) ctx.Connection.RemoteIpAddress = remoteIp;
        ctrl.ControllerContext = new ControllerContext { HttpContext = ctx };
        return ctrl;
    }

    private static IConfiguration ConfigWith(string key, string value) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [key] = value }).Build();

    [Fact]
    public async Task Create_AnonymousDisabled_Returns403()
    {
        var config = ConfigWith("PlatformBootstrap:AllowAnonymous", "false");
        using var fx = new Fixture(config);
        var ctrl = Build(config, fx.Bootstrapper, IPAddress.Loopback);

        var result = await ctrl.Create(
            new PlatformBootstrapRequest("root", "StrongPass123!", "Root", "root@example.com", "StrongPass123!"), CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
        Assert.False(await fx.Bootstrapper.HasAdministratorAsync());
    }

    [Fact]
    public async Task Create_NonLoopback_Returns403()
    {
        var config = new ConfigurationBuilder().Build();
        using var fx = new Fixture(config);
        var ctrl = Build(config, fx.Bootstrapper, IPAddress.Parse("192.168.1.5"));

        var result = await ctrl.Create(
            new PlatformBootstrapRequest("root", "StrongPass123!", "Root", "root@example.com", "StrongPass123!"), CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
        Assert.False(await fx.Bootstrapper.HasAdministratorAsync());
    }

    [Fact]
    public async Task Create_Loopback_Allowed_CreatesAdministrator()
    {
        var config = new ConfigurationBuilder().Build();
        using var fx = new Fixture(config);
        var ctrl = Build(config, fx.Bootstrapper, IPAddress.Loopback);

        var result = await ctrl.Create(
            new PlatformBootstrapRequest("root", "StrongPass123!", "Root", "root@example.com", "StrongPass123!"), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.True(await fx.Bootstrapper.HasAdministratorAsync());
    }

    [Fact]
    public async Task Create_SecondCall_AfterSuccess_ReturnsConflict()
    {
        var config = new ConfigurationBuilder().Build();
        using var fx = new Fixture(config);
        var ctrl = Build(config, fx.Bootstrapper, IPAddress.Loopback);

        await ctrl.Create(
            new PlatformBootstrapRequest("root", "StrongPass123!", "Root", "root@example.com", "StrongPass123!"), CancellationToken.None);
        var second = await ctrl.Create(
            new PlatformBootstrapRequest("root2", "StrongPass123!", "Root2", "root2@example.com", "StrongPass123!"), CancellationToken.None);

        var obj = Assert.IsAssignableFrom<ObjectResult>(second);
        Assert.Equal(StatusCodes.Status409Conflict, obj.StatusCode);
    }

    [Fact]
    public async Task Status_ReportsAnonymousAllowedFlag()
    {
        var config = ConfigWith("PlatformBootstrap:AllowAnonymous", "false");
        using var fx = new Fixture(config);
        var ctrl = Build(config, fx.Bootstrapper, IPAddress.Loopback);

        var result = await ctrl.Status(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var anonProp = ok.Value!.GetType().GetProperty("anonymousAllowed");
        Assert.NotNull(anonProp);
        Assert.False((bool)anonProp!.GetValue(ok.Value)!);
    }
}
