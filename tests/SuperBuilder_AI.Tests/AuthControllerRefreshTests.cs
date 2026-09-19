using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SuperBuilder_AI.Controllers;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.Auth;
using SuperBuilder_AI.Services.Identity;
using SuperBuilder_AI.Services.Localization;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// Phase 2 刷新端点单测：合法刷新返回 200 + 新 access/refresh；伪造/复用令牌返回 401。
/// SQLite 内存库 + 真实服务，不依赖 Moq。
/// </summary>
public sealed class AuthControllerRefreshTests
{
	private const long UserId = 1;
	private const long TenantId = 1001;
	private const string Key = "refresh-test-key";

	private static SuperBIContext CreateContext(out SqliteConnection connection)
	{
		connection = new SqliteConnection("DataSource=:memory:");
		connection.Open();
		var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
		var ctx = new SuperBIContext(options);
		ctx.Database.EnsureCreated();
		return ctx;
	}

	private static IConfiguration Config()
		=> new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Auth:RefreshTokenLifetimeDays"] = "14",
				["Auth:ShowTenantDirectory"] = "false",
			})
			.Build();

	private static async Task SeedUserAsync(SuperBIContext db, string securityStamp)
	{
		db.Tenants.Add(new Tenant { Id = TenantId, TenantCode = "t1001", TenantName = "T1001", Enabled = true });
		db.Users.Add(new User
		{
			Id = UserId,
			TenantId = TenantId,
			Username = "alice",
			Status = UserStatus.Active,
			SecurityStamp = securityStamp,
			PasswordHash = "x",
		});
		await db.SaveChangesAsync();
	}

	private static AuthController Build(SuperBIContext db, IConfiguration config, IRefreshTokenStore refreshStore)
		=> new AuthController(db, new IdentityService(db, new PasswordHasher()), new TokenService(Key), new PasswordHasher(), config, new TenantLanguageService(db, new NoopAuditService()), refreshStore);

	[Fact]
	public async Task Refresh_ValidToken_ReturnsOk_WithNewTokens()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedUserAsync(ctx, "STAMP-A");

		var config = Config();
		var store = new RefreshTokenStore(ctx, config);
		var (plain, _) = await store.CreateAsync(UserId, TenantId, "STAMP-A", null, null, DateTimeOffset.UtcNow.AddDays(14), CancellationToken.None);
		var ctrl = Build(ctx, config, store);

		var result = await ctrl.Refresh(new RefreshRequest { RefreshToken = plain }, CancellationToken.None);

		var ok = Assert.IsType<OkObjectResult>(result);
		var auth = Assert.IsType<AuthResult>(ok.Value);
		Assert.False(string.IsNullOrEmpty(auth.Token));
		Assert.False(string.IsNullOrEmpty(auth.RefreshToken));
		Assert.NotEqual(plain, auth.RefreshToken); // 返回的是轮换后的新刷新令牌
	}

	[Fact]
	public async Task Refresh_BogusToken_ReturnsUnauthorized()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedUserAsync(ctx, "STAMP-A");

		var ctrl = Build(ctx, Config(), new RefreshTokenStore(ctx, Config()));

		var result = await ctrl.Refresh(new RefreshRequest { RefreshToken = "bogus-not-issued" }, CancellationToken.None);

		Assert.IsType<UnauthorizedObjectResult>(result);
	}

	[Fact]
	public async Task Refresh_ReusedOldToken_ReturnsUnauthorized()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedUserAsync(ctx, "STAMP-A");

		var config = Config();
		var store = new RefreshTokenStore(ctx, config);
		var (plain, _) = await store.CreateAsync(UserId, TenantId, "STAMP-A", null, null, DateTimeOffset.UtcNow.AddDays(14), CancellationToken.None);
		var ctrl = Build(ctx, config, store);

		// 首次刷新成功。
		var first = await ctrl.Refresh(new RefreshRequest { RefreshToken = plain }, CancellationToken.None);
		Assert.IsType<OkObjectResult>(first);

		// 旧令牌被轮换后重放 —— 复用检测，应拒绝（401）。
		var reuse = await ctrl.Refresh(new RefreshRequest { RefreshToken = plain }, CancellationToken.None);
		Assert.IsType<UnauthorizedObjectResult>(reuse);
	}
}
