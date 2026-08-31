using SuperBuilder_AI.Components.Models;

namespace SuperBuilder_AI.Components.Services;

/// <summary>
/// 与 SuperBuilder_AI API 通信的客户端契约（RCL 共享，两个 Head 各自注册实现）。
/// 鉴权：登录后写入 <see cref="AppState.Token"/>，每次请求自动附带 Bearer 与 X-Tenant-Id。
/// </summary>
public interface IApiClient
{
    Task<(AuthResult? Result, string? Error)> LoginAsync(string username, long tenantId, CancellationToken ct = default);
    Task<string?> AskRawAsync(string question, long? dataSourceId, CancellationToken ct = default);
    /// <summary>类型化问数：返回 <see cref="BIResponse"/> 并区分传输错误。</summary>
    Task<AskOutcome> AskAsync(string question, long? dataSourceId, CancellationToken ct = default);
    /// <summary>发布为应用：结构化 App DSL 经默认路径（P8）保存到 api/apps。</summary>
    Task<(bool Ok, string? Code, string? Error)> PublishAppAsync(long tenantId, string dslJson, string? code, CancellationToken ct = default);
    Task<T?> GetAsync<T>(string relativeUrl, CancellationToken ct = default) where T : class;
}
