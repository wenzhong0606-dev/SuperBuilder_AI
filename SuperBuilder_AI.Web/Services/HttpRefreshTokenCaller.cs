using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Components.Services;

namespace SuperBuilder_AI.Web.Services;

/// <summary>
/// Web 端 <see cref="IRefreshTokenCaller"/> 实现：经命名 HttpClient POST <c>api/auth/refresh</c> 赎回新令牌。
/// 网络异常、非 2xx 或响应缺字段一律返回 <c>null</c>（不抛异常），交由协调器按"刷新失败"处理（后续 401 触发登出）。
/// </summary>
public sealed class HttpRefreshTokenCaller : IRefreshTokenCaller
{
    private readonly IHttpClientFactory _factory;

    public HttpRefreshTokenCaller(IHttpClientFactory factory) => _factory = factory;

    public async Task<RefreshCallResult?> CallRefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        try
        {
            var client = _factory.CreateClient("SuperBuilderApi");
            using var resp = await client.PostAsJsonAsync("api/auth/refresh", new { refreshToken }, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(body)) return null;
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var accessToken = root.TryGetProperty("token", out var t) ? t.GetString() : null;
            var newRefresh = root.TryGetProperty("refreshToken", out var r) ? r.GetString() : null;
            var expires = root.TryGetProperty("expiresInSeconds", out var e) && e.TryGetInt32(out var secs) ? secs : 0;
            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(newRefresh))
                return null;
            return new RefreshCallResult(accessToken, newRefresh, expires);
        }
        catch
        {
            return null;
        }
    }
}
