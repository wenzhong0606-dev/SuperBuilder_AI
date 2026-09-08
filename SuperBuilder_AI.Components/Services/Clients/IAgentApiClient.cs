using System.Net.Http;

namespace SuperBuilder_AI.Components.Services;

/// <summary>
/// Agent 域客户端契约（M9-01）：Agent/App 构建器所需的通用写原语（POST/PUT/PATCH/DELETE 及底层 Send）。
/// 与 <see cref="IApiClient"/> 中对应写方法签名一致，可独立于其他域单独测试/替换。
/// 当 M7-12（Agent 剩余 6 工具接真实后端）落地时，Agent 专用端点方法可在此接口增量补充。
/// </summary>
public interface IAgentApiClient
{
    /// <summary>通用写操作（POST/PUT/PATCH/DELETE）：成功返回 Ok=true，否则返回 Status、统一错误体中的 message 与错误码 code。</summary>
    Task<(bool Ok, int Status, string? Error, string? Code)> SendAsync(HttpMethod method, string relativeUrl, object? body = null, CancellationToken ct = default);

    /// <summary>POST JSON（body 为 null 时发送空请求）。</summary>
    Task<(bool Ok, int Status, string? Error, string? Code)> PostAsync(string relativeUrl, object? body = null, CancellationToken ct = default);

    /// <summary>PUT JSON（整体更新）。</summary>
    Task<(bool Ok, int Status, string? Error, string? Code)> PutAsync(string relativeUrl, object? body, CancellationToken ct = default);

    /// <summary>PATCH JSON（局部更新，如启用/禁用）。</summary>
    Task<(bool Ok, int Status, string? Error, string? Code)> PatchAsync(string relativeUrl, object? body = null, CancellationToken ct = default);

    /// <summary>DELETE。</summary>
    Task<(bool Ok, int Status, string? Error, string? Code)> DeleteAsync(string relativeUrl, CancellationToken ct = default);
}
