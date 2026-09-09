using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace SuperBuilder_AI.Api.OpenApi;

/// <summary>
/// 为 OpenAPI 文档每个操作补充统一的错误响应说明（401/403/422），
/// 并注册 <see cref="ApiError"/> 错误体 schema，使契约明确呈现「错误说明」。
/// 错误码语义见 <c>SuperBuilder_AI.Api.Errors.ErrorCodes</c>。
/// </summary>
internal sealed class OpenApiErrorResponsesTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();

        if (!document.Components.Schemas.ContainsKey("ApiError"))
        {
            document.Components.Schemas["ApiError"] = BuildApiErrorSchema();
        }

        var apiErrorRef = new OpenApiSchemaReference("ApiError", document);
        var errorContent = new Dictionary<string, OpenApiMediaType>
        {
            ["application/json"] = new OpenApiMediaType { Schema = apiErrorRef }
        };

        foreach (var path in document.Paths.Values)
        {
            if (path.Operations is null) continue;
            foreach (var operation in path.Operations.Values)
            {
                operation.Responses ??= new OpenApiResponses();
                operation.Responses["401"] = new OpenApiResponse
                {
                    Description = "未认证：缺少或无效的 Bearer 令牌（错误码 SB_AUTH_001 等）。",
                    Content = new Dictionary<string, OpenApiMediaType>(errorContent)
                };
                operation.Responses["403"] = new OpenApiResponse
                {
                    Description = "未授权：已认证身份无权访问该资源 / 租户 / 数据（错误码 SB_AUTHZ_002、PolicyBlocked 等）。",
                    Content = new Dictionary<string, OpenApiMediaType>(errorContent)
                };
                operation.Responses["422"] = new OpenApiResponse
                {
                    Description = "语义校验失败：DSL / 绑定不合法、不可知查询或字段级校验错误（错误码 SB_APP_DSL_INVALID、SB_BI_* 等，含 errors 字段）。",
                    Content = new Dictionary<string, OpenApiMediaType>(errorContent)
                };
            }
        }

        return Task.CompletedTask;
    }

    private static OpenApiSchema BuildApiErrorSchema() => new()
    {
        Type = JsonSchemaType.Object,
        Description = "统一错误响应体（camelCase 序列化：code / message / traceId / decision / errors）。" +
                      "由 UnifiedExceptionMiddleware 与控制器早期返回统一产出。",
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["code"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "统一错误码（如 SB_BI_002、SB_AUTHZ_002）。" },
            ["message"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "友好中文提示（已对用户可读，不泄露内部细节）。" },
            ["traceId"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "关联 ID（取自相关性中间件），便于运维检索日志。" },
            ["decision"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Description = "拒绝原因码（M7-11 应用运行时）：PermissionDenied / DataSourceUnauthorized / PolicyBlocked / RequiresClarification；仅拒绝类返回。"
            },
            ["errors"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Array,
                Description = "字段级校验明细（DSL / 绑定校验失败时使用）。",
                Items = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["field"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "出错字段路径（如 components[0].binding.filters[1].value）；非字段级错误可为 null。" },
                        ["code"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "字段级错误码（如 SB_APP_DSL_INVALID）。" },
                        ["message"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "字段级友好提示。" }
                    },
                    Required = new HashSet<string> { "code", "message" }
                }
            }
        },
        Required = new HashSet<string> { "code", "message" }
    };
}
