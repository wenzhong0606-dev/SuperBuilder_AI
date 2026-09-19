using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.Auth;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// Phase 2 刷新令牌存储单测：轮换、复用检测（吊销整链）、过期、未知、安全戳变更拒绝。
/// SQLite 内存库 + 真实 SuperBIContext + 真实 RefreshTokenStore，不依赖 Moq。
/// </summary>
public sealed class RefreshTokenStoreTests
{
	private const long UserId = 1;
	private const long TenantId = 1001;

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
			.AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:RefreshTokenLifetimeDays"] = "14" })
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

	private static RefreshTokenStore Store(SuperBIContext db) => new(db, Config());

	[Fact]
	public async Task Create_ThenRedeem_Success_AndNewTokenRedeemable()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedUserAsync(ctx, "STAMP-A");

		var store = Store(ctx);
		var (plain, family) = await store.CreateAsync(UserId, TenantId, "STAMP-A", null, null, DateTimeOffset.UtcNow.AddDays(14), CancellationToken.None);

		var outcome = await store.RedeemAsync(plain, null, null, CancellationToken.None);
		Assert.Equal(RefreshRedeemStatus.Success, outcome.Status);
		Assert.NotNull(outcome.NewRefreshToken);
		Assert.Equal(family, outcome.FamilyId);
		Assert.Equal(UserId, outcome.UserId);

		// 新令牌可继续赎回（再次轮换）。
		var second = await store.RedeemAsync(outcome.NewRefreshToken!, null, null, CancellationToken.None);
		Assert.Equal(RefreshRedeemStatus.Success, second.Status);
	}

	[Fact]
	public async Task Redeem_OldTokenAgain_DetectsReuse_AndRevokesFamily()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedUserAsync(ctx, "STAMP-A");

		var store = Store(ctx);
		var (plain, _) = await store.CreateAsync(UserId, TenantId, "STAMP-A", null, null, DateTimeOffset.UtcNow.AddDays(14), CancellationToken.None);
		var success = await store.RedeemAsync(plain, null, null, CancellationToken.None);
		Assert.Equal(RefreshRedeemStatus.Success, success.Status);

		// 攻击者在旧令牌被轮换后重放 —— 复用检测。
		var reuse = await store.RedeemAsync(plain, null, null, CancellationToken.None);
		Assert.Equal(RefreshRedeemStatus.ReuseDetected, reuse.Status);

		// 复用检测吊销整条 family：合法的「新」令牌也应失效（强制重新登录）。
		var newTokenReuse = await store.RedeemAsync(success.NewRefreshToken!, null, null, CancellationToken.None);
		Assert.Equal(RefreshRedeemStatus.ReuseDetected, newTokenReuse.Status);
	}

	[Fact]
	public async Task Redeem_ExpiredToken_ReturnsExpired()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedUserAsync(ctx, "STAMP-A");

		var store = Store(ctx);
		// 已过期（创建时过期时间在过去）。
		var (plain, _) = await store.CreateAsync(UserId, TenantId, "STAMP-A", null, null, DateTimeOffset.UtcNow.AddDays(-1), CancellationToken.None);

		var outcome = await store.RedeemAsync(plain, null, null, CancellationToken.None);
		Assert.Equal(RefreshRedeemStatus.Expired, outcome.Status);
	}

	[Fact]
	public async Task Redeem_UnknownToken_ReturnsUnknown()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedUserAsync(ctx, "STAMP-A");

		var store = Store(ctx);
		var outcome = await store.RedeemAsync("not-a-real-issued-token", null, null, CancellationToken.None);
		Assert.Equal(RefreshRedeemStatus.Unknown, outcome.Status);
	}

	[Fact]
	public async Task Redeem_AfterSecurityStampChange_ReturnsStampMismatch()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedUserAsync(ctx, "STAMP-A");

		var store = Store(ctx);
		var (plain, _) = await store.CreateAsync(UserId, TenantId, "STAMP-A", null, null, DateTimeOffset.UtcNow.AddDays(14), CancellationToken.None);

		// 口令/角色变更后安全戳轮换（P0-04B）。
		var user = await ctx.Users.FirstAsync(u => u.Id == UserId);
		user.SecurityStamp = "STAMP-B";
		await ctx.SaveChangesAsync();

		var outcome = await store.RedeemAsync(plain, null, null, CancellationToken.None);
		Assert.Equal(RefreshRedeemStatus.StampMismatch, outcome.Status);
	}
}
