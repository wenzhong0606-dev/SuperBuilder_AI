using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using SuperBuilder_AI.Api.Security;
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

	[Theory]
	[InlineData("/test/understand")]
	[InlineData("/metrics")]
	public async Task Diagnostics_Path_Without_Token_Returns_401(string path)
	{
		var nextCalled = false;
		var mw = Build(_ => { nextCalled = true; return Task.CompletedTask; });
		var ctx = new DefaultHttpContext { Request = { Path = path } };

		await mw.InvokeAsync(ctx);

		Assert.False(nextCalled);
		Assert.Equal(StatusCodes.Status401Unauthorized, ctx.Response.StatusCode);
	}

	[Theory]
	[InlineData("/test/sql")]
	[InlineData("/metrics")]
	[InlineData("/api/metadata-vector/status")]
	[InlineData("/api/metadata-vector/rebuild")]
	public async Task Diagnostics_Path_With_Ordinary_Token_Returns_403(string path)
	{
		var nextCalled = false;
		var mw = Build(_ => { nextCalled = true; return Task.CompletedTask; });
		var token = new TokenService(Key).Issue(9, 11, "carol", new[] { "dashboard:view" });
		var ctx = new DefaultHttpContext { Request = { Path = path } };
		ctx.Request.Headers["Authorization"] = "Bearer " + token;

		await mw.InvokeAsync(ctx);

		Assert.False(nextCalled);
		Assert.Equal(StatusCodes.Status403Forbidden, ctx.Response.StatusCode);
	}

	[Theory]
	[InlineData("/test/sql")]
	[InlineData("/metrics")]
	[InlineData("/api/metadata-vector/status")]
	[InlineData("/api/metadata-vector/rebuild")]
	public async Task Diagnostics_Path_With_Governance_Token_Passes(string path)
	{
		var nextCalled = false;
		var mw = Build(_ => { nextCalled = true; return Task.CompletedTask; });
		var token = new TokenService(Key).Issue(0, 1, "governor", new[] { "platform:diagnostics:manage" });
		var ctx = new DefaultHttpContext { Request = { Path = path } };
		ctx.Request.Headers["Authorization"] = "Bearer " + token;

		await mw.InvokeAsync(ctx);

		Assert.True(nextCalled);
		Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode);
	}

	[Theory]
	[InlineData("/api/ask")]
	[InlineData("/api/metadata")]
	[InlineData("/api/data-sources")]
	public async Task Governance_Token_Is_Forbidden_From_DataPlane(string path)
	{
		var nextCalled = false;
		var mw = Build(_ => { nextCalled = true; return Task.CompletedTask; });
		var token = new TokenService(Key).Issue(1, 1, "governor", new[] { "platform:tenant:manage" });
		var ctx = new DefaultHttpContext { Request = { Path = path } };
		ctx.Request.Headers["Authorization"] = "Bearer " + token;

		await mw.InvokeAsync(ctx);

		Assert.False(nextCalled);
		Assert.Equal(StatusCodes.Status403Forbidden, ctx.Response.StatusCode);
	}

	[Theory]
	[InlineData("/api/ask", "tenantId", "10")]
	[InlineData("/api/business-model/entities", "tenantId", "10")]
	[InlineData("/api/semantic-labels/resolve", "X-Tenant-Id", "10")]
	public async Task DataPlane_CrossTenant_Request_Returns_403_WithoutCallingDownstream(string path, string source, string value)
	{
		var nextCalled = false;
		var mw = Build(_ => { nextCalled = true; return Task.CompletedTask; });
		var token = new TokenService(Key).Issue(9, 11, "carol", new[] { "dashboard:view" });
		var ctx = new DefaultHttpContext { Request = { Path = path } };
		ctx.Request.Headers["Authorization"] = "Bearer " + token;
		if (source == "tenantId") ctx.Request.QueryString = new QueryString("?tenantId=" + value);
		else ctx.Request.Headers[source] = value;

		await mw.InvokeAsync(ctx);

		Assert.False(nextCalled);
		Assert.Equal(StatusCodes.Status403Forbidden, ctx.Response.StatusCode);
		Assert.Equal(9L, ctx.Items[TenantDataPlanePolicy.AuthenticatedTenantItem]);
		Assert.Equal(9L, ctx.Items[TenantDataPlanePolicy.EffectiveTenantItem]);
		Assert.Equal(false, ctx.Items[TenantDataPlanePolicy.TenantSwitchAuthorizedItem]);
	}

	[Fact]
	public async Task DataPlane_SameTenant_Request_Passes_AndStoresEffectiveTenant()
	{
		var nextCalled = false;
		var mw = Build(_ => { nextCalled = true; return Task.CompletedTask; });
		var token = new TokenService(Key).Issue(9, 11, "carol", new[] { "dashboard:view" });
		var ctx = new DefaultHttpContext { Request = { Path = "/api/ask", QueryString = new QueryString("?tenantId=9") } };
		ctx.Request.Headers["Authorization"] = "Bearer " + token;

		await mw.InvokeAsync(ctx);

		Assert.True(nextCalled);
		Assert.Equal(9L, ctx.Items[TenantDataPlanePolicy.EffectiveTenantItem]);
	}
}
