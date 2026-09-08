using System.Text.Json;

namespace SuperBuilder_AI.Components.Services;

/// <summary>
/// 看板域客户端契约（M9-01）：看板/可视化所需的通用读原语（类型化、松类型 JSON、纯文本）。
/// 与 <see cref="IApiClient"/> 中对应读方法签名一致，可独立于其他域单独测试/替换。
/// </summary>
public interface IDashboardApiClient
{
    Task<T?> GetAsync<T>(string relativeUrl, CancellationToken ct = default) where T : class;

    /// <summary>松类型读取：GET 任意端点并以 <see cref="JsonElement"/> 返回（数组或对象皆可），不抛异常。</summary>
    Task<(JsonElement? Data, int Status, string? Error, string? Code)> GetJsonAsync(string relativeUrl, CancellationToken ct = default);

    /// <summary>纯文本读取（如 /metrics 的 Prometheus 文本、/health 的探针响应）。</summary>
    Task<(string? Text, int Status, string? Error)> GetTextAsync(string relativeUrl, CancellationToken ct = default);
}
