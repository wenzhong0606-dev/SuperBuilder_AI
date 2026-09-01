using System.Collections.Generic;
using System.Text.Json;
using SuperBuilder_AI.Components.Models;

namespace SuperBuilder_AI.Components.Services;

/// <summary>
/// 与 SuperBuilder_AI API 通信的客户端契约（RCL 共享，两个 Head 各自注册实现）。
/// 鉴权：登录后写入 <see cref="AppState.Token"/>，每次请求自动附带 Bearer。
/// 租户恒由令牌承载（SB-P0-02C 已移除 X-Tenant-Id 自动头）。
/// </summary>
public interface IApiClient
{
    Task<(AuthResult? Result, string? Error)> LoginAsync(string username, long tenantId, string? password = null, CancellationToken ct = default);
    Task<string?> AskRawAsync(string question, long? dataSourceId, CancellationToken ct = default);
    /// <summary>类型化问数：返回 <see cref="BIResponse"/> 并区分传输错误。</summary>
    Task<AskOutcome> AskAsync(string question, long? dataSourceId, CancellationToken ct = default);
    /// <summary>
    /// 多轮语义调整：在已有问题（+ 历史上下文）上追加细化指令，重新走完整 BI 链路。
    /// 仅在用户显式发起「细化」时调用（<c>POST api/ask/refine</c>）；默认问数路径 <c>api/ask</c> 不变。
    /// </summary>
    Task<AskOutcome> RefineAsync(string? question, string instruction, IEnumerable<RefineTurn>? history, long? dataSourceId, CancellationToken ct = default);
    /// <summary>发布为应用：结构化 App DSL 经默认路径（P8）保存到 api/apps。</summary>
    Task<(bool Ok, string? Code, string? Error)> PublishAppAsync(long tenantId, string dslJson, string? code, CancellationToken ct = default);
    Task<T?> GetAsync<T>(string relativeUrl, CancellationToken ct = default) where T : class;
    /// <summary>
    /// 松类型读取：GET 任意端点并以 <see cref="JsonElement"/> 返回（数组或对象皆可）。
    /// 不抛异常——HTTP 非 2xx 与网络/解析错误一律通过 err 返回，便于页面优雅降级。
    /// </summary>
    Task<(JsonElement? Data, int Status, string? Error)> GetJsonAsync(string relativeUrl, CancellationToken ct = default);
}
