using System.Collections.Generic;
using System.Linq;
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
}
