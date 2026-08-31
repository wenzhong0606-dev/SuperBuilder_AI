using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using SuperBuilder_AI.Middleware;
using SuperBuilder_AI.Services.Auth;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>P11.0 鉴权中间件单元测试（无 DB / 无 LLM，确定性）。</summary>
public class AuthMiddlewareTests
{
	private const string Key = "mw-test-key";
	private static AuthMiddleware Build(RequestDelegate next)
		=> new AuthMiddleware(next, new TokenService(Key));

	[Fact]
	public async Task Evaluation_Path_Is_Anonymous_And_Passes_Through()
	{
		var nextCalled = false;
		var mw = Build(_ => { nextCalled = true; return Task.CompletedTask; });
		var ctx = new DefaultHttpContext { Request = { Path = "/evaluation/golden-runtime/run" } };

		await mw.InvokeAsync(ctx);

		Assert.True(nextCalled);
		Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode);
	}

	[Fact]
	public async Task Api_Path_Without_Token_Returns_401()
	{
		var nextCalled = false;
		var mw = Build(_ => { nextCalled = true; return Task.CompletedTask; });
		var ctx = new DefaultHttpContext { Request = { Path = "/api/secret" } };

		await mw.InvokeAsync(ctx);

		Assert.False(nextCalled);
		Assert.Equal(StatusCodes.Status401Unauthorized, ctx.Response.StatusCode);
	}

	[Fact]
	public async Task Api_Path_With_Valid_Token_Sets_User_And_Passes()
	{
		var nextCalled = false;
		var mw = Build(_ => { nextCalled = true; return Task.CompletedTask; });
		var token = new TokenService(Key).Issue(9, 11, "carol", new[] { "dashboard:view" });

		var ctx = new DefaultHttpContext { Request = { Path = "/api/ask" } };
		ctx.Request.Headers["Authorization"] = "Bearer " + token;

		await mw.InvokeAsync(ctx);

		Assert.True(nextCalled);
		Assert.True(ctx.User.Identity?.IsAuthenticated ?? false);
		Assert.Equal("9", ctx.User.FindFirst("tid")?.Value);
		Assert.Equal("11", ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
	}

	[Fact]
	public async Task Login_Endpoint_Is_Anonymous()
	{
		var nextCalled = false;
		var mw = Build(_ => { nextCalled = true; return Task.CompletedTask; });
		var ctx = new DefaultHttpContext { Request = { Path = "/api/auth/login" } };

		await mw.InvokeAsync(ctx);

		Assert.True(nextCalled);
	}

	[Fact]
	public async Task Static_Asset_Is_Anonymous()
	{
		var nextCalled = false;
		var mw = Build(_ => { nextCalled = true; return Task.CompletedTask; });
		var ctx = new DefaultHttpContext { Request = { Path = "/css/site.css" } };

		await mw.InvokeAsync(ctx);

		Assert.True(nextCalled);
	}
}
