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

	// ── AUTH-1 回归：路径判定必须大小写不敏感 ────────────────────────────
	// 背景：PathString.StartsWithSegments 默认按 Ordinal 区分大小写，而 ASP.NET 路由匹配大小写不敏感。
	// 若该判定被回退为大小写敏感，将同时产生两类问题：
	//   1) /API/** 变体绕过鉴权与租户数据面隔离，却被路由命中控制器 → 匿名越权 / 跨租户越权；
	//   2) /API/auth/login 进不了匿名白名单 → 客户端无法登录（登录死锁）。
	// 以下用例守护这两类行为，任何回退都会立即让测试失败。

	[Theory]
	[InlineData("/API/ask")]
	[InlineData("/Api/Secret")]
	[InlineData("/API/DATA-SOURCES")]
	public async Task Uppercase_Api_Path_Without_Token_Returns_401(string path)
	{
		var nextCalled = false;
		var mw = Build(_ => { nextCalled = true; return Task.CompletedTask; });
		var ctx = new DefaultHttpContext { Request = { Path = path } };

		await mw.InvokeAsync(ctx);

		Assert.False(nextCalled);
		Assert.Equal(StatusCodes.Status401Unauthorized, ctx.Response.StatusCode);
	}

	[Theory]
	[InlineData("/API/AUTH/LOGIN")]
	[InlineData("/Api/Auth/Login")]
	public async Task Uppercase_Login_Endpoint_Remains_Anonymous(string path)
	{
		var nextCalled = false;
		var mw = Build(_ => { nextCalled = true; return Task.CompletedTask; });
		var ctx = new DefaultHttpContext { Request = { Path = path } };

		await mw.InvokeAsync(ctx);

		// 登录端点必须放行：否则客户端或反向代理改写为大写路径时将无法登录。
		Assert.True(nextCalled);
		Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode);
	}

	[Theory]
	[InlineData("/METRICS")]
	[InlineData("/Metrics")]
	public async Task Uppercase_Diagnostics_Path_Without_Token_Returns_401(string path)
	{
		var nextCalled = false;
		var mw = Build(_ => { nextCalled = true; return Task.CompletedTask; });
		var ctx = new DefaultHttpContext { Request = { Path = path } };

		await mw.InvokeAsync(ctx);

		Assert.False(nextCalled);
		Assert.Equal(StatusCodes.Status401Unauthorized, ctx.Response.StatusCode);
	}

	[Fact]
	public async Task Uppercase_DataPlane_Path_Enforces_Tenant_Isolation()
	{
		var nextCalled = false;
		var mw = Build(_ => { nextCalled = true; return Task.CompletedTask; });
		var token = new TokenService(Key).Issue(9, 11, "carol", new[] { "dashboard:view" });
		var ctx = new DefaultHttpContext
		{
			Request = { Path = "/API/ASK", QueryString = new QueryString("?tenantId=10") }
		};
		ctx.Request.Headers["Authorization"] = "Bearer " + token;

		await mw.InvokeAsync(ctx);

		// 大写数据面路径同样必须纳入租户隔离，否则可用于跨租户越权。
		Assert.False(nextCalled);
		Assert.Equal(StatusCodes.Status403Forbidden, ctx.Response.StatusCode);
		Assert.Equal(9L, ctx.Items[TenantDataPlanePolicy.EffectiveTenantItem]);
	}
}
