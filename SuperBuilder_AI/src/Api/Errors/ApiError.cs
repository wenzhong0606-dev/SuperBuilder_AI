namespace SuperBuilder_AI.Api.Errors;

/// <summary>
/// 统一错误响应体。camelCase 序列化：<c>code</c> / <c>message</c> / <c>traceId</c> / <c>details</c>。
/// 由 <see cref="UnifiedExceptionMiddleware"/> 与控制器早期返回统一产出，前端据此展示友好提示。
/// </summary>
public sealed class ApiError
{
    /// <summary>统一错误码（如 SB_BI_002）。</summary>
    public string Code { get; init; } = "";

    /// <summary>友好中文提示（已对用户可读，不泄露内部细节）。</summary>
    public string Message { get; init; } = "";

    /// <summary>关联 ID（取自 ObservabilityMiddleware 的 CorrelationId），便于运维检索日志。</summary>
    public string? TraceId { get; init; }

    /// <summary>可选技术细节（默认不返回，仅内部排障用）。</summary>
    public string? Details { get; init; }
}
