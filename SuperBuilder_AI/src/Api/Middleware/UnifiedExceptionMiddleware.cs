using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SuperBuilder_AI.Api.Errors;

namespace SuperBuilder_AI.Middleware;

/// <summary>
/// 统一异常中间件（P11 错误治理）。
/// <list type="bullet">
///   <item>捕获管线未处理异常，转换为结构化 <see cref="ApiError"/>（友好中文 + 错误码 + 关联ID），不泄露堆栈；</item>
///   <item>按异常类型/消息映射错误码与 HTTP 状态码（<see cref="ErrorCodes"/>）；</item>
///   <item>按错误码记录结构化日志（关联ID 取自 ObservabilityMiddleware 写入的 <c>HttpContext.Items["CorrelationId"]</c>）；</item>
///   <item>仅拦截异常，不修改成功路径响应，故不影响 Golden 行为契约。</item>
/// </list>
/// 该中间件不改动任何业务抛点，对既有 InvalidOperationException 等按消息模式匹配到具体 BI 错误码，
/// 从而在零产品代码改动的前提下达成「统一错误编码 + 友好提示」。
/// </summary>
public sealed class UnifiedExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public UnifiedExceptionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ILogger<UnifiedExceptionMiddleware> logger)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex, logger);
        }
    }

    private static string CorrelationId(HttpContext context) =>
        context.Items.TryGetValue("CorrelationId", out var v) ? v?.ToString() ?? "" : "";

    private async Task HandleAsync(HttpContext context, Exception ex, ILogger<UnifiedExceptionMiddleware> logger)
    {
        var (code, status, message) = Map(ex);
        var traceId = CorrelationId(context);

        if (status >= 500)
            logger.LogError(ex, "Unhandled exception {Code} {TraceId} {Method} {Path}", code, traceId, context.Request.Method, context.Request.Path);
        else
            logger.LogWarning("Business rejection {Code} {TraceId} {Method} {Path}: {Message}", code, traceId, context.Request.Method, context.Request.Path, message);

        if (context.Response.HasStarted)
        {
            // 响应体已开始写入，无法改写；仅记录日志。
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        var err = new ApiError
        {
            Code = code,
            Message = message,
            TraceId = string.IsNullOrEmpty(traceId) ? null : traceId
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(err, JsonOpts));
    }

    private static (string code, int status, string message) Map(Exception ex) => ex switch
    {
        SuperBuilderException sb => (sb.ErrorCode, sb.StatusCode, sb.Message),
        ArgumentException or ArgumentNullException => (ErrorCodes.BadRequest, 400, ErrorCodes.Message(ErrorCodes.BadRequest)),
        UnauthorizedAccessException => (ErrorCodes.Unauthorized, 401, ErrorCodes.Message(ErrorCodes.Unauthorized)),
        KeyNotFoundException => (ErrorCodes.NotFound, 404, ErrorCodes.Message(ErrorCodes.NotFound)),
        NotSupportedException => (ErrorCodes.Unsupported, 501, ErrorCodes.Message(ErrorCodes.Unsupported)),
        InvalidOperationException ioe => MapInvalidOperation(ioe),
        _ => (ErrorCodes.Internal, 500, ErrorCodes.Message(ErrorCodes.Internal))
    };

    /// <summary>
    /// 对既有 <see cref="InvalidOperationException"/>（多为产品层业务拒绝，未带错误码）按消息模式匹配到具体 BI 错误码，
    /// 实现精确友好提示；无法识别时回落到通用内部错误（500）。
    /// </summary>
    private static (string code, int status, string message) MapInvalidOperation(InvalidOperationException ex)
    {
        var m = ex.Message ?? "";
        if (m.Contains("没有任何可查询字段")) return (ErrorCodes.BiNoQueryableField, 400, ErrorCodes.Message(ErrorCodes.BiNoQueryableField));
        if (m.Contains("没有查询表")) return (ErrorCodes.BiNoQueryTable, 400, ErrorCodes.Message(ErrorCodes.BiNoQueryTable));
        if (m.Contains("不支持") || m.Contains("不受支持")) return (ErrorCodes.BiSemanticUnsupported, 422, ErrorCodes.Message(ErrorCodes.BiSemanticUnsupported));
        if (m.Contains("多个匹配") || (m.Contains("匹配") && m.Contains("Metric"))) return (ErrorCodes.BiMetricAmbiguous, 422, ErrorCodes.Message(ErrorCodes.BiMetricAmbiguous));
        return (ErrorCodes.Internal, 500, ErrorCodes.Message(ErrorCodes.Internal));
    }
}
