using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SuperBuilder_AI.Middleware;

/// <summary>
/// 可观测性中间件（P10.5 Enterprise SaaS 治理面）。
/// 为每个 HTTP 请求分配/透传关联 ID（<c>X-Correlation-Id</c>），
/// 记录请求入口与响应出口日志（方法 / 路径 / 租户 / 状态码 / 耗时），
/// 全程非阻塞、异常静默，不修改响应体，不影响 Golden 行为契约。
/// 租户从 query <c>tenantId</c> 或 header <c>X-Tenant-Id</c> 提取，缺失则记为平台级(0)。
/// </summary>
public sealed class ObservabilityMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationHeader = "X-Correlation-Id";
    private const string CorrelationItemKey = "CorrelationId";

    public ObservabilityMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ILogger<ObservabilityMiddleware> logger)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        context.Items[CorrelationItemKey] = correlationId;
        TrySetResponseHeader(context, correlationId);

        var tenantId = ResolveTenantId(context);
        var started = DateTime.UtcNow;

        try
        {
            TryLog(logger, LogLevel.Information,
                "REQ {CorrelationId} {Method} {Path} tenant={Tenant}",
                correlationId, context.Request.Method, context.Request.Path, tenantId);
            await _next(context);
        }
        finally
        {
            try
            {
                var elapsedMs = (long)(DateTime.UtcNow - started).TotalMilliseconds;
                var statusCode = context.Response.StatusCode;
                TryLog(logger, LogLevel.Information,
                    "RES {CorrelationId} {StatusCode} {ElapsedMs}ms",
                    correlationId, statusCode, elapsedMs);
            }
            catch
            {
                // 日志失败不影响主链路
            }
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationHeader, out var incoming) &&
            !string.IsNullOrWhiteSpace(incoming.ToString()))
        {
            return incoming.ToString()!;
        }
        return Guid.NewGuid().ToString("N");
    }

    private static void TrySetResponseHeader(HttpContext context, string correlationId)
    {
        try
        {
            if (!context.Response.HasStarted)
                context.Response.Headers[CorrelationHeader] = correlationId;
        }
        catch
        {
            // 响应已启动则跳过，不影响主链路
        }
    }

    private static void TryLog(ILogger logger, LogLevel level, string template, params object[] args)
    {
        try
        {
            logger.Log(level, template, args);
        }
        catch
        {
            // 异常静默
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
