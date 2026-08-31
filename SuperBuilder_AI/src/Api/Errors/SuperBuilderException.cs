using System;

namespace SuperBuilder_AI.Api.Errors;

/// <summary>
/// 业务统一异常：携带错误码与（已友好的）中文提示，由
/// <see cref="UnifiedExceptionMiddleware"/> 转换为结构化 <see cref="ApiError"/> 响应。
/// 现阶段中间件已能通过消息模式匹配既有 InvalidOperationException 达成统一编码；
/// 新增业务拒绝点优先使用本类型以获得最精确的错误码与文案。
/// </summary>
public sealed class SuperBuilderException : Exception
{
    /// <summary>统一错误码（如 SB_BI_002）。</summary>
    public string ErrorCode { get; }

    /// <summary>HTTP 状态码（默认 500）。</summary>
    public int StatusCode { get; }

    public SuperBuilderException(string errorCode, string message, int statusCode = 500, Exception? inner = null)
        : base(message, inner)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }

    /// <summary>按错误码构造，文案取 <see cref="ErrorCodes.Message"/> 友好提示。</summary>
    public static SuperBuilderException FromCode(string errorCode, int statusCode = 500, Exception? inner = null) =>
        new(errorCode, ErrorCodes.Message(errorCode), statusCode, inner);
}
