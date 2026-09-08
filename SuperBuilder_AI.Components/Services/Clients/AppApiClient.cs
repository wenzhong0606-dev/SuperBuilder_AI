using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SuperBuilder_AI.Components.Services;

/// <summary>
/// 应用域客户端实现（M9-01）：发布结构化 App DSL 到 <c>api/apps</c>。
/// 继承 <see cref="ApiClientBase"/> 复用鉴权头/401 回收。
/// </summary>
public sealed class AppApiClient : ApiClientBase, IAppApiClient
{
    public AppApiClient(IHttpClientFactory factory, AppState appState) : base(factory, appState) { }

    /// <summary>发布为应用：将结构化 App DSL 经默认路径（P8 <c>BuildFromDslAsync</c>）保存到 <c>api/apps</c>。</summary>
    public async Task<(bool Ok, string? Code, string? Error)> PublishAppAsync(
        long tenantId, string dslJson, string? code, CancellationToken ct = default)
    {
        var client = CreateClient();
        var body = new { tenantId, dslJson, code };
        var resp = await client.PostAsJsonAsync("api/apps", body, ct);
        if (resp.IsSuccessStatusCode)
        {
            try
            {
                var detail = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
                var c = detail.TryGetProperty("code", out var ce) ? ce.GetString() : code;
                return (true, c ?? code, null);
            }
            catch
            {
                return (true, code, null);
            }
        }
        var err = await resp.Content.ReadAsStringAsync(ct);
        if (resp.StatusCode == HttpStatusCode.Unauthorized) OnUnauthorized();
        return (false, null, $"{(int)resp.StatusCode}: {err}");
    }
}
