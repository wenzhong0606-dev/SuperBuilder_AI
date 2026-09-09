using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System.Collections.Generic;

namespace SuperBuilder_AI.Api.OpenApi;

/// <summary>
/// 为 OpenAPI 文档注入 Bearer 鉴权方案与全局安全要求。
/// 本平台鉴权由 <c>AuthMiddleware</c> 解析 <c>Authorization: Bearer &lt;token&gt;</c> 或
/// <c>X-Api-Token</c> 头实现（非标准 ASP.NET Core 认证方案），故以 HTTP Bearer 语义在契约中声明。
/// </summary>
internal sealed class OpenApiSecurityTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        try
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
            {
                ["bearerAuth"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "无状态 Bearer 令牌。由请求头 Authorization: Bearer <token> 或 X-Api-Token 提供；" +
                                  "AuthMiddleware 解析后注入身份与租户上下文。除显式匿名白名单端点（如 /health、/login）外，所有 /api 端点均要求有效令牌。"
                }
            };

            var requirement = new OpenApiSecurityRequirement
            {
                { new OpenApiSecuritySchemeReference("bearerAuth", document), new List<string>() }
            };

            foreach (var path in document.Paths.Values)
            {
                if (path.Operations is null) continue;
                foreach (var operation in path.Operations.Values)
                {
                    operation.Security ??= new List<OpenApiSecurityRequirement>();
                    operation.Security.Add(requirement);
                }
            }

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"OpenApiSecurityTransformer 失败：{ex.Message}", ex);
        }
    }
}
