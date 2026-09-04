using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace SuperBuilder_AI.Middleware;

/// <summary>
/// M1-01：将 EF 乐观并发冲突（<see cref="DbUpdateConcurrencyException"/>）统一映射为 HTTP 409，
/// 避免泄露内部异常细节。必须注册在 <c>UnifiedExceptionMiddleware</c> 之后（更内层），
/// 以优先于全局 catch-all 捕获并发冲突。
/// </summary>
public sealed class ConcurrencyExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public ConcurrencyExceptionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DbUpdateConcurrencyException)
        {
            context.Response.Clear();
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            context.Response.ContentType = "application/json; charset=utf-8";
            var payload = new
            {
                error = "concurrency_conflict",
                message = "数据已被其他请求修改，请刷新后重试。"
            };
            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
    }
}
