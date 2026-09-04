using System.Collections.Generic;
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
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M1-02 停用治理：停用租户禁止登录（AuthController.Login 校验租户 Enabled）。
/// SQLite 内存库 + 真实 IdentityService / TokenService，不依赖 Moq。
/// </summary>
public sealed class AuthControllerDisabledTenantTests
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
		=> new AuthController(db, new IdentityService(db, new PasswordHasher()), new TokenService("mw-test-key"), new PasswordHasher(), config);

	private static IConfiguration Config()
		=> new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:ShowTenantDirectory"] = "false" })
			.Build();

	[Fact]
	public async Task Login_DisabledTenant_ReturnsUnauthorized()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		ctx.Tenants.Add(new Tenant { Id = 2, TenantCode = "acme2", TenantName = "Acme2", Enabled = false });
		await ctx.SaveChangesAsync();

		var identity = new IdentityService(ctx, new PasswordHasher());
		var created = await identity.CreateUserAsync(2, "alice", "Alice", "", null);
		Assert.True(created.Success);
		Assert.True((await identity.SetPasswordAsync(2, created.Id!.Value, "password1")).Success);

		var ctrl = Build(ctx, Config());
		var result = await ctrl.Login(
			new LoginRequest { TenantId = 2, Username = "alice", Password = "password1" }, CancellationToken.None);

		var unauthorized = Assert.IsType<Microsoft.AspNetCore.Mvc.UnauthorizedObjectResult>(result);
		Assert.Equal(401, unauthorized.StatusCode);
	}

	[Fact]
	public async Task Login_EnabledTenant_Succeeds()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		ctx.Tenants.Add(new Tenant { Id = 1, TenantCode = "acme", TenantName = "Acme", Enabled = true });
		await ctx.SaveChangesAsync();

		var identity = new IdentityService(ctx, new PasswordHasher());
		var created = await identity.CreateUserAsync(1, "bob", "Bob", "", null);
		Assert.True(created.Success);
		Assert.True((await identity.SetPasswordAsync(1, created.Id!.Value, "password1")).Success);

		var ctrl = Build(ctx, Config());
		var result = await ctrl.Login(
			new LoginRequest { TenantId = 1, Username = "bob", Password = "password1" }, CancellationToken.None);

		Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);
	}
}
