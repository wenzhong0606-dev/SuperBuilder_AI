namespace SuperBuilder_AI.Components.Services;

/// <summary>
/// 应用域客户端契约（M9-01）：将结构化 App DSL 发布保存为应用。
/// 与 <see cref="IApiClient"/> 中对应方法签名一致，可独立于其他域单独测试/替换。
/// </summary>
public interface IAppApiClient
{
    /// <summary>发布为应用：将结构化 App DSL 经默认路径（P8 <c>BuildFromDslAsync</c>）保存到 <c>api/apps</c>。</summary>
    Task<(bool Ok, string? Code, string? Error)> PublishAppAsync(
        long tenantId, string dslJson, string? code, CancellationToken ct = default);
}
