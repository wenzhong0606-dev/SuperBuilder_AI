using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Middleware;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M1-01 验收：ConcurrencyExceptionMiddleware 将 EF 乐观并发冲突映射为 HTTP 409，
/// 且不泄露内部异常细节。
/// </summary>
public class ConcurrencyExceptionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ConcurrencyException_Returns409()
    {
        var middleware = new ConcurrencyExceptionMiddleware(_ => throw new DbUpdateConcurrencyException());
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal((int)HttpStatusCode.Conflict, context.Response.StatusCode);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("concurrency_conflict", body);
    }

    [Fact]
    public async Task InvokeAsync_NoException_PassesThrough()
    {
        var middleware = new ConcurrencyExceptionMiddleware(c =>
        {
            c.Response.StatusCode = 200;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
    }
}
