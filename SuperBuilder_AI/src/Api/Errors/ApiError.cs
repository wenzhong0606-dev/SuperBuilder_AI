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

    /// <summary>
    /// 拒绝原因码（M7-11 应用运行时）。用于让前端/调用方以<em>结构化</em>方式区分拒绝类型，
    /// 而<strong>不靠解析中文 <c>Message</c></strong>。取值见 <see cref="ErrorDecisions"/>：
    /// <c>PermissionDenied</c> / <c>DataSourceUnauthorized</c> / <c>PolicyBlocked</c> / <c>RequiresClarification</c>。
    /// 仅当拒绝属于这四类之一时填充；普通参数错误可不填。
    /// </summary>
    public string? Decision { get; init; }

    /// <summary>字段级校验明细（DSL/绑定校验失败时使用）。</summary>
    public IReadOnlyList<FieldError>? Errors { get; init; }
}

/// <summary>字段级校验错误（结构化，便于前端定位具体字段）。</summary>
public sealed record FieldError
{
    /// <summary>出错字段路径（如 <c>components[0].binding.filters[1].value</c>）；非字段级错误可为 null。</summary>
    public string? Field { get; init; }

    /// <summary>字段级错误码（如 <c>SB_APP_DSL_INVALID</c>）。</summary>
    public string Code { get; init; } = "";

    /// <summary>字段级友好提示。</summary>
    public string Message { get; init; } = "";
}
