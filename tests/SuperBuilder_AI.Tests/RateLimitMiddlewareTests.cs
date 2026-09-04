using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Middleware;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M0-08 限流中间件测试：键维度（登录后租户+用户 / 登录前可信IP+设备）、
/// 登录路径独立严格阈值、伪造令牌头不得成为限流键、结构化 429 契约。
///
/// 注：<see cref="RateLimitMiddleware"/> 的计数器为进程级静态字典，本类各用例通过
/// 唯一 IP / 租户+用户组合隔离桶，保证确定性、互不污染。
/// </summary>
public sealed class RateLimitMiddlewareTests
{
    private static RateLimitMiddleware Build(RateLimitOptions opts, RequestDelegate? next = null)
        => new RateLimitMiddleware(next ?? (_ => Task.CompletedTask), Options.Create(opts));

    private static DefaultHttpContext AnonymousContext(string ip, string ua, string path = "/api/ask")
    {
        var ctx = new DefaultHttpContext
        {
            Request = { Path = path },
            Response = { Body = new MemoryStream() }
        };
        ctx.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        ctx.Request.Headers["User-Agent"] = ua;
        return ctx;
    }

    [Fact]
    public async Task Anonymous_Exceeding_GlobalLimit_Returns_429_With_StructuredBody()
    {
        var opts = new RateLimitOptions { GlobalLimit = 1, GlobalWindowSeconds = 60, LoginLimit = 1000, LoginWindowSeconds = 60 };
        var mw = Build(opts);

        var ctx1 = AnonymousContext("198.51.100.1", "ua-A");
        await mw.InvokeAsync(ctx1);
        Assert.Equal(StatusCodes.Status200OK, ctx1.Response.StatusCode);

        var ctx2 = AnonymousContext("198.51.100.1", "ua-A");
        await mw.InvokeAsync(ctx2);

        Assert.Equal(StatusCodes.Status429TooManyRequests, ctx2.Response.StatusCode);
        Assert.Equal("60", ctx2.Response.Headers["Retry-After"].ToString());
        Assert.Contains("application/json", ctx2.Response.ContentType ?? string.Empty);
        ctx2.Response.Body.Position = 0;
        var body = JsonSerializer.Deserialize<ApiError>(ctx2.Response.Body);
        Assert.NotNull(body);
        Assert.Equal(ErrorCodes.TooManyRequests, body!.Code);
    }

    [Fact]
    public async Task Forged_ApiToken_Does_Not_Grant_Separate_Bucket()
    {
        var opts = new RateLimitOptions { GlobalLimit = 1, GlobalWindowSeconds = 60, LoginLimit = 1000, LoginWindowSeconds = 60 };
        var mw = Build(opts);

        var ctx1 = AnonymousContext("198.51.100.2", "ua-B");
        ctx1.Request.Headers["X-Api-Token"] = "forged-token-1";
        await mw.InvokeAsync(ctx1);
        Assert.Equal(StatusCodes.Status200OK, ctx1.Response.StatusCode);

        // 同一 IP+设备、但携带不同伪造令牌，仍落入同一匿名桶 → 应被限流
        var ctx2 = AnonymousContext("198.51.100.2", "ua-B");
        ctx2.Request.Headers["X-Api-Token"] = "forged-token-2";
        await mw.InvokeAsync(ctx2);
        Assert.Equal(StatusCodes.Status429TooManyRequests, ctx2.Response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_Request_Is_Keyed_By_TenantAndUser()
    {
        var opts = new RateLimitOptions { GlobalLimit = 1, GlobalWindowSeconds = 60, LoginLimit = 1000, LoginWindowSeconds = 60 };
        var mw = Build(opts);

        var ctx = new DefaultHttpContext
        {
            Request = { Path = "/api/ask" },
            Response = { Body = new MemoryStream() }
        };
        ctx.Request.Headers["User-Agent"] = "any-ua";
        ctx.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.3");
        ctx.Items["TenantId"] = 77L;
        ctx.Items["UserId"] = 88L;
        await mw.InvokeAsync(ctx);
        Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode);

        var ctx2 = new DefaultHttpContext
        {
            Request = { Path = "/api/ask" },
            Response = { Body = new MemoryStream() }
        };
        ctx2.Request.Headers["User-Agent"] = "any-ua";
        ctx2.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.3"); // 同 IP/设备
        ctx2.Items["TenantId"] = 77L;
        ctx2.Items["UserId"] = 88L;
        await mw.InvokeAsync(ctx2);
        // 因已认证，键为 u:77:88（与 IP/UA 无关），第二次超阈值 → 429
        Assert.Equal(StatusCodes.Status429TooManyRequests, ctx2.Response.StatusCode);
    }

    [Fact]
    public async Task Login_Path_Uses_Stricter_LoginLimit()
    {
        var opts = new RateLimitOptions { GlobalLimit = 1000, GlobalWindowSeconds = 60, LoginLimit = 1, LoginWindowSeconds = 60 };
        var mw = Build(opts);

        var ctx1 = AnonymousContext("198.51.100.4", "ua-login", "/api/auth/login");
        await mw.InvokeAsync(ctx1);
        Assert.Equal(StatusCodes.Status200OK, ctx1.Response.StatusCode);

        var ctx2 = AnonymousContext("198.51.100.4", "ua-login", "/api/auth/login");
        await mw.InvokeAsync(ctx2);
        Assert.Equal(StatusCodes.Status429TooManyRequests, ctx2.Response.StatusCode);
    }

    [Fact]
    public async Task NonApi_Path_Bypasses_RateLimit()
    {
        var opts = new RateLimitOptions { GlobalLimit = 1, GlobalWindowSeconds = 60, LoginLimit = 1, LoginWindowSeconds = 60 };
        var mw = Build(opts);
        for (var i = 0; i < 5; i++)
        {
            var ctx = AnonymousContext("203.0.113.9", "ua-X", "/health");
            await mw.InvokeAsync(ctx);
            Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode);
        }
    }
}
