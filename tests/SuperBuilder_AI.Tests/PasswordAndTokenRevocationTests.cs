using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SuperBuilder_AI.Controllers;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Middleware;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.Auth;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>P0-04A 正式口令认证 + P0-04B 令牌吊销（SecurityStamp）单测。</summary>
public class PasswordAndTokenRevocationTests
{
	private const string Key = "pw-test-key";

	#region P0-04A PasswordHasher

	[Fact]
	public void PasswordHasher_Hash_Then_Verify_Succeeds()
	{
		var h = new PasswordHasher();
		var hash = h.Hash("s3cret");
		Assert.StartsWith("pbkdf2:", hash);
		Assert.True(h.Verify("s3cret", hash));
	}

	[Fact]
	public void PasswordHasher_WrongPassword_Fails()
	{
		var h = new PasswordHasher();
		var hash = h.Hash("s3cret");
		Assert.False(h.Verify("wrong", hash));
	}

	[Fact]
	public void PasswordHasher_SamePlaintext_Produces_Different_Hash()
	{
		var h = new PasswordHasher();
		var a = h.Hash("s3cret");
		var b = h.Hash("s3cret");
		Assert.NotEqual(a, b); // 随机盐
		Assert.True(h.Verify("s3cret", a));
		Assert.True(h.Verify("s3cret", b));
	}

	[Fact]
	public void PasswordHasher_NullOrMalformed_Safe()
	{
		var h = new PasswordHasher();
		Assert.False(h.Verify(null, null));
		Assert.False(h.Verify("x", null));
		Assert.False(h.Verify("x", "not-a-hash"));
		Assert.False(h.Verify("x", "pbkdf2:notanint:salt:hash"));
	}

	#endregion

	#region P0-04A AuthController password enforcement

	private static SuperBIContext CreateUserContext(string? password, out string stamp)
	{
		var conn = new SqliteConnection("DataSource=:memory:");
		conn.Open();
		var ctx = new SuperBIContext(new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(conn).Options);
		ctx.Database.EnsureCreated();
		stamp = Guid.NewGuid().ToString("N");
		var hasher = new PasswordHasher();
		// M1-02：登录需校验租户存在且启用，因此测试中补建对应启用的租户。
		ctx.Tenants.Add(new Tenant { Id = 1, TenantCode = "t1", TenantName = "T1", Enabled = true });
		ctx.Users.Add(new User
		{
			TenantId = 1,
			Username = "alice",
			Status = UserStatus.Active,
			SecurityStamp = stamp,
			PasswordHash = password is null ? null : hasher.Hash(password),
		});
		ctx.SaveChanges();
		return ctx;
	}

	private sealed class FakeIdentity : IIdentityService
	{
		public Task SeedAsync(CancellationToken ct = default) => Task.CompletedTask;
		public Task<IdentityResult> CreateUserAsync(long t, string u, string d, string e, string[]? r, CancellationToken ct = default) => throw new NotImplementedException();
		public Task<IdentityResult> AssignRoleAsync(long t, long u, string rc, CancellationToken ct = default) => throw new NotImplementedException();
		public Task<IdentityResult> RevokeRoleAsync(long t, long u, string rc, CancellationToken ct = default) => throw new NotImplementedException();
		public Task<IdentityResult> SetPasswordAsync(long t, long u, string p, CancellationToken ct = default) => throw new NotImplementedException();
		public Task<IdentityResult> SetUserStatusAsync(long t, long u, UserStatus newStatus, CancellationToken ct = default) => throw new NotImplementedException();
		public Task<IReadOnlyList<string>> GetPermissionsAsync(long t, long u, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
		public Task<bool> HasPermissionAsync(long t, long u, string pc, CancellationToken ct = default) => Task.FromResult(false);
	}

	[Fact]
	public async Task Login_NullPasswordHash_Is_Rejected_As_NotInitialized()
	{
		var ctx = CreateUserContext(null, out _);
		var ctrl = new AuthController(ctx, new FakeIdentity(), new TokenService(Key), new PasswordHasher(), null!);

		var result = await ctrl.Login(new LoginRequest { Username = "alice", TenantId = 1, Password = "whatever" });

		var obj = Assert.IsType<UnauthorizedObjectResult>(result);
		Assert.Contains("尚未设置口令", obj.Value?.ToString() ?? "");
	}

	[Fact]
	public async Task Login_WrongPassword_Is_Rejected()
	{
		var ctx = CreateUserContext("s3cret", out _);
		var ctrl = new AuthController(ctx, new FakeIdentity(), new TokenService(Key), new PasswordHasher(), null!);

		var result = await ctrl.Login(new LoginRequest { Username = "alice", TenantId = 1, Password = "wrong" });

		Assert.IsType<UnauthorizedObjectResult>(result);
	}

	[Fact]
	public async Task Login_CorrectPassword_Issues_Token_Carrying_SecurityStamp()
	{
		var ctx = CreateUserContext("s3cret", out var stamp);
		var tokenSvc = new TokenService(Key);
		var ctrl = new AuthController(ctx, new FakeIdentity(), tokenSvc, new PasswordHasher(), null!);

		var result = await ctrl.Login(new LoginRequest { Username = "alice", TenantId = 1, Password = "s3cret" });

		var ok = Assert.IsType<OkObjectResult>(result);
		var auth = Assert.IsType<AuthResult>(ok.Value);
		Assert.False(string.IsNullOrEmpty(auth.Token));

		var principal = tokenSvc.Validate(auth.Token);
		Assert.NotNull(principal);
		Assert.Equal(stamp, principal!.SecurityStamp); // P0-04B：令牌携带当前安全戳
	}

	#endregion

	#region P0-04B AuthMiddleware SecurityStamp enforcement

	private static (AuthMiddleware Mw, SuperBIContext Ctx, IServiceProvider Sp) BuildMiddlewareWithUser(string? securityStamp)
	{
		var conn = new SqliteConnection("DataSource=:memory:");
		conn.Open();
		var ctx = new SuperBIContext(new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(conn).Options);
		ctx.Database.EnsureCreated();
		// M1-02：中间件需校验租户启用，因此补建对应启用的租户。
		ctx.Tenants.Add(new Tenant { Id = 9, TenantCode = "t9", TenantName = "T9", Enabled = true });
		ctx.Users.Add(new User
		{
			Id = 11,
			TenantId = 9,
			Username = "carol",
			Status = UserStatus.Active,
			SecurityStamp = securityStamp,
		});
		ctx.SaveChanges();

		var services = new ServiceCollection();
		services.AddSingleton(ctx);
		var sp = services.BuildServiceProvider();

		var mw = new AuthMiddleware(_ => Task.CompletedTask, new TokenService(Key));
		return (mw, ctx, sp);
	}

	private static async Task<int> InvokeWithToken(AuthMiddleware mw, IServiceProvider sp, string? token)
	{
		var httpCtx = new DefaultHttpContext { Request = { Path = "/api/ask" } };
		httpCtx.RequestServices = sp;
		if (token is not null)
			httpCtx.Request.Headers["Authorization"] = "Bearer " + token;
		await mw.InvokeAsync(httpCtx);
		return httpCtx.Response.StatusCode;
	}

	[Fact]
	public async Task Middleware_ValidStamp_Passes()
	{
		var (mw, _, sp) = BuildMiddlewareWithUser("stamp-A");
		var token = new TokenService(Key).Issue(9, 11, "carol", new[] { "dashboard:view" }, "stamp-A");
		Assert.Equal(StatusCodes.Status200OK, await InvokeWithToken(mw, sp, token));
	}

	[Fact]
	public async Task Middleware_RotatedStamp_Returns_401()
	{
		var (mw, ctx, sp) = BuildMiddlewareWithUser("stamp-A");
		var token = new TokenService(Key).Issue(9, 11, "carol", new[] { "dashboard:view" }, "stamp-A");

		// 口令/角色变更导致安全戳轮换
		var user = await ctx.Users.FirstAsync(u => u.Id == 11);
		user.SecurityStamp = "stamp-B";
		await ctx.SaveChangesAsync();

		Assert.Equal(StatusCodes.Status401Unauthorized, await InvokeWithToken(mw, sp, token));
	}

	[Fact]
	public async Task Middleware_LegacyToken_WithoutStamp_Passes()
	{
		var (mw, _, sp) = BuildMiddlewareWithUser("stamp-A");
		// 遗留令牌不携带 Sec（迁移窗口内），不做吊销校验
		var token = new TokenService(Key).Issue(9, 11, "carol", new[] { "dashboard:view" });
		Assert.Equal(StatusCodes.Status200OK, await InvokeWithToken(mw, sp, token));
	}

	[Fact]
	public async Task Middleware_DeletedUser_Returns_401()
	{
		var (mw, ctx, sp) = BuildMiddlewareWithUser("stamp-A");
		var token = new TokenService(Key).Issue(9, 11, "carol", new[] { "dashboard:view" }, "stamp-A");

		var user = await ctx.Users.FirstAsync(u => u.Id == 11);
		ctx.Users.Remove(user);
		await ctx.SaveChangesAsync();

		Assert.Equal(StatusCodes.Status401Unauthorized, await InvokeWithToken(mw, sp, token));
	}

	#endregion
}
