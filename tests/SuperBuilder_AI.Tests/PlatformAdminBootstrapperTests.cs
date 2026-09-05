using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Services.Auth;
using SuperBuilder_AI.Services.Identity;
using Xunit;

namespace SuperBuilder_AI.Tests;

public sealed class PlatformAdminBootstrapperTests
{
    [Fact]
    public async Task EnsureAsync_CreatesExactlyOnePlatformAdministrator()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
        await using var db = new SuperBIContext(options);
        await db.Database.EnsureCreatedAsync();
        var hasher = new PasswordHasher();
        await new IdentityService(db, hasher).SeedAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PlatformBootstrap:Username"] = "platform-root",
            ["PlatformBootstrap:Password"] = "StrongPass123!",
        }).Build();
        var bootstrapper = new PlatformAdminBootstrapper(db, hasher, config);

        Assert.True(await bootstrapper.EnsureAsync());
        Assert.False(await bootstrapper.EnsureAsync());

        var platformTenantId = await db.Tenants.Where(t => t.TenantCode == "platform").Select(t => t.Id).SingleAsync();
        var user = await db.Users.SingleAsync(u => u.Username == "platform-root");
        Assert.Equal(platformTenantId, user.TenantId);
        Assert.True(hasher.Verify("StrongPass123!", user.PasswordHash));
        var platformRoleId = await db.Roles.Where(r => r.Code == IdentityRoles.PlatformAdmin).Select(r => r.Id).SingleAsync();
        Assert.Single(await db.UserRoles.Where(x => x.UserId == user.Id && x.RoleId == platformRoleId).ToListAsync());
    }

    [Fact]
    public async Task EnsureAsync_WithoutConfiguration_LeavesInteractiveBootstrapPending()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
        await using var db = new SuperBIContext(options);
        await db.Database.EnsureCreatedAsync();
        var hasher = new PasswordHasher();
        await new IdentityService(db, hasher).SeedAsync();
        var bootstrapper = new PlatformAdminBootstrapper(db, hasher, new ConfigurationBuilder().Build());

        Assert.False(await bootstrapper.EnsureAsync());
        Assert.False(await bootstrapper.HasAdministratorAsync());
    }

    [Fact]
    public async Task GetStatusAsync_OnEmptyDatabase_ReturnsNeedsMigration()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
        await using var db = new SuperBIContext(options);
        await db.Database.EnsureCreatedAsync();
        var bootstrapper = new PlatformAdminBootstrapper(db, new PasswordHasher(), new ConfigurationBuilder().Build());

        var status = await bootstrapper.GetStatusAsync();
        Assert.Equal(BootstrapStatus.NeedsMigration, status);
    }

    [Fact]
    public async Task GetStatusAsync_AfterSeedWithoutAdmin_ReturnsNeedsInitialization()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
        await using var db = new SuperBIContext(options);
        await db.Database.EnsureCreatedAsync();
        var hasher = new PasswordHasher();
        await new IdentityService(db, hasher).SeedAsync();
        var bootstrapper = new PlatformAdminBootstrapper(db, hasher, new ConfigurationBuilder().Build());

        var status = await bootstrapper.GetStatusAsync();
        Assert.Equal(BootstrapStatus.NeedsInitialization, status);
        Assert.False(await bootstrapper.HasAdministratorAsync());
    }

    [Fact]
    public async Task GetStatusAsync_WithAdmin_ReturnsReady()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
        await using var db = new SuperBIContext(options);
        await db.Database.EnsureCreatedAsync();
        var hasher = new PasswordHasher();
        await new IdentityService(db, hasher).SeedAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PlatformBootstrap:Username"] = "platform-root",
            ["PlatformBootstrap:Password"] = "StrongPass123!",
        }).Build();
        var bootstrapper = new PlatformAdminBootstrapper(db, hasher, config);

        Assert.True(await bootstrapper.EnsureAsync());
        Assert.Equal(BootstrapStatus.Ready, await bootstrapper.GetStatusAsync());
        Assert.True(await bootstrapper.HasAdministratorAsync());
    }

    [Fact]
    public async Task EnsureAsync_WhenNeedsMigration_DoesNotThrowAndReturnsFalse()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
        await using var db = new SuperBIContext(options);
        await db.Database.EnsureCreatedAsync();
        var bootstrapper = new PlatformAdminBootstrapper(db, new PasswordHasher(), new ConfigurationBuilder().Build());

        // 空库（平台租户缺失）下 EnsureAsync 必须安全返回 false，而非抛出 InvalidOperationException
        var ex = await Record.ExceptionAsync(() => bootstrapper.EnsureAsync());
        Assert.Null(ex);
        Assert.False(await bootstrapper.EnsureAsync());
    }

    [Fact]
    public async Task CreateAsync_Interactive_RejectsMismatchedConfirmPassword()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
        await using var db = new SuperBIContext(options);
        await db.Database.EnsureCreatedAsync();
        var hasher = new PasswordHasher();
        await new IdentityService(db, hasher).SeedAsync();
        var bootstrapper = new PlatformAdminBootstrapper(db, hasher, new ConfigurationBuilder().Build());

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            bootstrapper.CreateAsync("root", "StrongPass123!", "Root", "root@example.com", "DifferentPass1!", CancellationToken.None));
        Assert.Contains("不一致", ex.Message);
        Assert.False(await bootstrapper.HasAdministratorAsync());
    }

    [Fact]
    public async Task CreateAsync_Interactive_RejectsInvalidEmail()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
        await using var db = new SuperBIContext(options);
        await db.Database.EnsureCreatedAsync();
        var hasher = new PasswordHasher();
        await new IdentityService(db, hasher).SeedAsync();
        var bootstrapper = new PlatformAdminBootstrapper(db, hasher, new ConfigurationBuilder().Build());

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            bootstrapper.CreateAsync("root", "StrongPass123!", "Root", "not-an-email", "StrongPass123!", CancellationToken.None));
        Assert.Contains("邮箱", ex.Message);
        Assert.False(await bootstrapper.HasAdministratorAsync());
    }

    [Fact]
    public async Task CreateAsync_Interactive_StoresValidEmail()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
        await using var db = new SuperBIContext(options);
        await db.Database.EnsureCreatedAsync();
        var hasher = new PasswordHasher();
        await new IdentityService(db, hasher).SeedAsync();
        var bootstrapper = new PlatformAdminBootstrapper(db, hasher, new ConfigurationBuilder().Build());

        var (_, userId) = await bootstrapper.CreateAsync("root", "StrongPass123!", "Root", "root@example.com", "StrongPass123!", CancellationToken.None);
        var user = await db.Users.SingleAsync(u => u.Id == userId);
        Assert.Equal("root@example.com", user.Email);
    }

    [Fact]
    public async Task CreateAsync_ConfigPath_AllowsEmptyEmail()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
        await using var db = new SuperBIContext(options);
        await db.Database.EnsureCreatedAsync();
        var hasher = new PasswordHasher();
        await new IdentityService(db, hasher).SeedAsync();
        var bootstrapper = new PlatformAdminBootstrapper(db, hasher, new ConfigurationBuilder().Build());

        // 部署配置路径不传 confirmPassword，邮箱允许为空（兼容 PlatformBootstrap:Email 未设置场景）
        var (_, userId) = await bootstrapper.CreateAsync("root", "StrongPass123!", "Root");
        var user = await db.Users.SingleAsync(u => u.Id == userId);
        Assert.Equal("", user.Email);
    }
}
