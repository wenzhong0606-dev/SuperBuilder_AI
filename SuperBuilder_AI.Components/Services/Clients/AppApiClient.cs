using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Components.Services.Models;

namespace SuperBuilder_AI.Components.Services;

/// <summary>
/// 应用域客户端实现（M9-01）：发布结构化 App DSL 到 <c>api/apps</c>。
/// 继承 <see cref="ApiClientBase"/> 复用鉴权头/401 回收。
/// </summary>
public sealed class AppApiClient : ApiClientBase, IAppApiClient
{
    public AppApiClient(IHttpClientFactory factory, AppState appState, IAuthRefreshCoordinator? coordinator = null) : base(factory, appState, coordinator) { }

    /// <summary>发布为应用：将结构化 App DSL 经默认路径（P8 <c>BuildFromDslAsync</c>）保存到 <c>api/apps</c>。</summary>
    public async Task<(bool Ok, string? Code, string? Error)> PublishAppAsync(
        long tenantId, string dslJson, string? code, CancellationToken ct = default)
    {
        var client = CreateClient();
        var sentWithToken = !string.IsNullOrEmpty(AppState.Token);
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
        if (resp.StatusCode == HttpStatusCode.Unauthorized) OnUnauthorized(sentWithToken);
        return (false, null, $"{(int)resp.StatusCode}: {err}");
    }

    /// <summary>M7-11：运行已发布应用。</summary>
    public async Task<AppRenderResult> RenderAppAsync(long tenantId, string code, CancellationToken ct = default)
        => await RenderOrPreviewAsync("render", tenantId, code, ct);

    /// <summary>M7-11：预览应用草稿。</summary>
    public async Task<AppRenderResult> PreviewAppAsync(long tenantId, string code, CancellationToken ct = default)
        => await RenderOrPreviewAsync("preview", tenantId, code, ct);

    /// <summary>C4：发布已创建的草稿（POST api/apps/{code}/publish）。</summary>
    public async Task<(bool Ok, int? Version, string? Error, int Status)> PublishExistingAsync(long tenantId, string code, CancellationToken ct = default)
    {
        var client = CreateClient();
        var sentWithToken = !string.IsNullOrEmpty(AppState.Token);
        var resp = await client.PostAsync($"api/apps/{Uri.EscapeDataString(code)}/publish?tenantId={tenantId}", null, ct);
        var status = (int)resp.StatusCode;
        if (resp.IsSuccessStatusCode)
        {
            try
            {
                var detail = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
                var v = detail.TryGetProperty("version", out var ve) && ve.ValueKind == JsonValueKind.Number
                    ? ve.GetInt32()
                    : (int?)null;
                return (true, v, null, status);
            }
            catch
            {
                return (true, null, null, status);
            }
        }

        var body = await resp.Content.ReadAsStringAsync(ct);
        if (resp.StatusCode == HttpStatusCode.Unauthorized) OnUnauthorized(sentWithToken);
        var (message, _) = ParseError(body);
        return (false, null, message ?? body, status);
    }

    private async Task<AppRenderResult> RenderOrPreviewAsync(string action, long tenantId, string code, CancellationToken ct)
    {
        var client = CreateClient();
        var sentWithToken = !string.IsNullOrEmpty(AppState.Token);
        var resp = await client.GetAsync($"api/apps/{Uri.EscapeDataString(code)}/{action}?tenantId={tenantId}", ct);
        if (resp.IsSuccessStatusCode)
        {
            try
            {
                var model = await resp.Content.ReadFromJsonAsync<AppRenderDto>(JsonOpts, ct);
                return new AppRenderResult(true, model, (int)resp.StatusCode, null, null);
            }
            catch (Exception ex)
            {
                return new AppRenderResult(false, null, (int)resp.StatusCode, ex.Message, null);
            }
        }

        var body = await resp.Content.ReadAsStringAsync(ct);
        if (resp.StatusCode == HttpStatusCode.Unauthorized) OnUnauthorized(sentWithToken);
        var (message, errorCode) = ParseError(body);
        return new AppRenderResult(false, null, (int)resp.StatusCode, message, errorCode);
    }

    /// <summary>从 <c>{ errors: [...] }</c> 或 <c>{ code, message }</c> 提取服务端错误。</summary>
    private static (string? Message, string? Code) ParseError(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return (null, null);
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
            {
                var first = errors[0];
                var msg = first.ValueKind == JsonValueKind.String ? first.GetString() : first.GetString();
                return (msg, null);
            }
            if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
            {
                var code = root.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
                return (message.GetString(), code);
            }
        }
        catch
        {
            // 非 JSON 或解析失败：返回原文
        }
        return (body, null);
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
}
