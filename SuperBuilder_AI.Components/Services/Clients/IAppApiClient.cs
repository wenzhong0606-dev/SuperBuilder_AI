namespace SuperBuilder_AI.Components.Services;

using SuperBuilder_AI.Components.Services.Models;

/// <summary>
/// 应用域客户端契约（M9-01）：将结构化 App DSL 发布保存为应用。
/// 与 <see cref="IApiClient"/> 中对应方法签名一致，可独立于其他域单独测试/替换。
/// </summary>
public interface IAppApiClient
{
    /// <summary>发布为应用：将结构化 App DSL 经默认路径（P8 <c>BuildFromDslAsync</c>）保存到 <c>api/apps</c>。</summary>
    Task<(bool Ok, string? Code, string? Error)> PublishAppAsync(
        long tenantId, string dslJson, string? code, CancellationToken ct = default);

    /// <summary>M7-11：运行已发布应用（<c>GET api/apps/{code}/render</c>，需 app:view）。</summary>
    Task<AppRenderResult> RenderAppAsync(long tenantId, string code, CancellationToken ct = default);

    /// <summary>M7-11：预览应用草稿（<c>GET api/apps/{code}/preview</c>，需 app:edit）。</summary>
    Task<AppRenderResult> PreviewAppAsync(long tenantId, string code, CancellationToken ct = default);

    /// <summary>C4：对已创建的草稿执行发布（<c>POST api/apps/{code}/publish</c>，需 app:publish）。返回 (Ok, 发布版本号, 错误信息, HTTP 状态)。</summary>
    Task<(bool Ok, int? Version, string? Error, int Status)> PublishExistingAsync(long tenantId, string code, CancellationToken ct = default);
}
