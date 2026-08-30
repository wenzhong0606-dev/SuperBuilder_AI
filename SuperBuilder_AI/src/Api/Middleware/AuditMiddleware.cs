using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using SuperBuilder_AI.Interfaces.Audit;

namespace SuperBuilder_AI.Middleware;

/// <summary>
/// 自动请求级审计中间件（P10.3 可选接入）。
/// 以非阻塞、异常静默方式记录每个 HTTP 请求的「方法 / 路径 / 状态码 / 耗时 / 租户」，
/// 审计写入失败不影响主链路（不影响 Golden 行为契约）。
/// 租户从 query <c>tenantId</c> 或 header <c>X-Tenant-Id</c> 提取，缺失则记为平台级(0)。
/// </summary>
public sealed class AuditMiddleware
{
    private readonly RequestDelegate _next;

    public AuditMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IAuditLogService audit)
    {
        var started = DateTime.UtcNow;
        try
        {
            await _next(context);
        }
        finally
        {
            try
            {
                var elapsedMs = (long)(DateTime.UtcNow - started).TotalMilliseconds;
                var tenantId = ResolveTenantId(context);
                var statusCode = context.Response.StatusCode;
                await audit.LogAsync(new AuditLogEntry(
                    TenantId: tenantId,
                    Action: "http.request",
                    EntityType: "Http",
                    Actor: "http",
                    EntityId: $"{context.Request.Method} {context.Request.Path}",
                    BeforeJson: null,
                    AfterJson: JsonSerializer.Serialize(new
                    {
                        statusCode,
                        durationMs = elapsedMs,
                        method = context.Request.Method,
                        path = context.Request.Path.Value,
                    }),
                    Result: statusCode < 400 ? "success" : "failure",
                    Message: null,
                    Timestamp: started));
            }
            catch
            {
                // 审计失败不影响主链路
            }
        }
    }

    private static long ResolveTenantId(HttpContext context)
    {
        if (context.Request.Query.TryGetValue("tenantId", out var q) && long.TryParse(q.ToString(), out var qt))
            return qt;
        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var h) && long.TryParse(h.ToString(), out var ht))
            return ht;
        return 0;
    }
}
