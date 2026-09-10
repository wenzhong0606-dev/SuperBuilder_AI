using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Components.Models;

namespace SuperBuilder_AI.Components.Services;

/// <summary>
/// 所有聚焦 API 客户端的共享基类（M9-01：拆分 God ApiClient）。
/// 持有命名 HttpClient 工厂与 AppState，统一提供：鉴权 Bearer 头注入、
/// 401 会话失效回收（<see cref="OnUnauthorized"/>）、统一错误体解析（<see cref="ParseApiError"/>），
/// 以及通用读（<see cref="GetAsync{T}"/>/<see cref="GetJsonAsync"/>/<see cref="GetTextAsync"/>）
/// 与写（<see cref="SendAsync"/>/Post/Put/Patch/Delete）原语。
/// </summary>
/// <remarks>
/// 各域客户端（Identity/Admin/BI/App/Dashboard/Agent/DataSource）继承此类并仅补充本域方法，
/// 从而每个域都能以独立的、可 mock 的客户端进行单元测试，互不牵连。
/// 租户隔离由服务端从令牌 <c>tid</c> 声明派生（数据面单租户恒等），前端不再发送 X-Tenant-Id 头（SB-P0-02C）。
/// </remarks>
public abstract class ApiClientBase
{
    protected readonly IHttpClientFactory Factory;
    protected readonly AppState AppState;

    protected ApiClientBase(IHttpClientFactory factory, AppState appState)
    {
        Factory = factory;
        AppState = appState;
    }


    /// <summary>
    /// 创建注入鉴权头的 HttpClient。租户恒由令牌承载（SB-P0-02C 已移除 X-Tenant-Id 自动头）。
    /// </summary>
    protected HttpClient CreateClient()
    {
        var client = Factory.CreateClient("SuperBuilderApi");
        client.DefaultRequestHeaders.Authorization = null;
        if (!string.IsNullOrEmpty(AppState.Token))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AppState.Token);
        return client;
    }

    /// <summary>
    /// 会话失效回收：收到 401 时清空本地 token 并通知壳层跳登录。
    /// 仅当「该请求发出时确实携带了令牌」且当前仍为已登录态才触发。
    /// </summary>
    /// <remarks>
    /// <b>为什么必须校验"请求带了令牌"：</b>页面自身的 <c>OnAfterRenderAsync(firstRender)</c> 早于
    /// <c>MainLayout</c> 的异步会话自举（子组件 <c>OnAfterRender</c> 先于父布局），此时
    /// <see cref="AppState.Token"/> 尚为空 → 请求不带 Bearer → 服务端返回 401。
    /// 若该 401 在自举完成之后才被处理，仅按 <c>IsAuthenticated</c> 判定就会把这个"未登录时发出的 401"
    /// 当成"已登录会话过期"，从而 <b>清掉刚还原成功的有效会话并强制跳转登录页</b>——
    /// 表现为硬加载 / F5 某些深层路由时被登出（非确定性竞态）。
    /// </remarks>
    protected void OnUnauthorized(bool requestCarriedToken)
    {
        // 请求未携带令牌 → 401 只说明"当时未登录"，不能据此推断已登录会话已失效
        if (!requestCarriedToken) return;
        if (!AppState.IsAuthenticated) return;
        AppState.ClearSession();
        AppState.NotifySessionExpired();
    }

    /// <summary>
    /// 解析后端统一错误体：优先 <see cref="ApiError"/>(code/message/traceId)，
    /// 其次兼容旧 <c>{ error }</c> 形状，最后回退到原始文本（截断避免过长）。
    /// </summary>
    protected static (string? Code, string? Message, string? TraceId) ParseApiError(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return (null, null, null);
        try
        {
            var opt = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var e = JsonSerializer.Deserialize<ApiError>(body, opt);
            if (e is not null && (!string.IsNullOrEmpty(e.Code) || !string.IsNullOrEmpty(e.Message)))
                return (e.Code, e.Message, e.TraceId);
            // 兼容旧 { error: "..." }
            var raw = JsonSerializer.Deserialize<JsonElement>(body, opt);
            if (raw.ValueKind == JsonValueKind.Object)
            {
                if (raw.TryGetProperty("error", out var ev) && ev.ValueKind == JsonValueKind.String)
                    return (null, ev.GetString(), null);
                // 后端写操作普遍以 { errors: ["..."] } 扁平数组返回，逐条合并为可读文本
                if (raw.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Array)
                {
                    var msgs = new List<string>();
                    foreach (var item in errs.EnumerateArray())
                        if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                            msgs.Add(item.GetString()!);
                    if (msgs.Count > 0)
                        return (null, string.Join("；", msgs), null);
                }
            }
        }
        catch
        {
            // 非 JSON 则回退原始文本
        }
        var trimmed = body.Length > 240 ? body[..240] + "…" : body;
        return (null, trimmed, null);
    }

    /// <summary>类型化读取：GET 任意端点并反序列化为 T，失败时返回 null 且不抛异常。</summary>
    public async Task<T?> GetAsync<T>(string relativeUrl, CancellationToken ct = default) where T : class
    {
        var client = CreateClient();
        var sentWithToken = !string.IsNullOrEmpty(AppState.Token);
        try
        {
            var resp = await client.GetAsync(relativeUrl, ct);
            if (resp.StatusCode == HttpStatusCode.Unauthorized) OnUnauthorized(sentWithToken);
            if (!resp.IsSuccessStatusCode) return null;
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(body)) return null;
            return JsonSerializer.Deserialize<T>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 松类型读取：GET 任意端点并以 <see cref="JsonElement"/> 返回（数组或对象皆可）。
    /// 不抛异常——HTTP 非 2xx 与网络/解析错误一律通过 err 返回；非 2xx 时同时透传后端错误码 <c>code</c>。
    /// </summary>
    public async Task<(JsonElement? Data, int Status, string? Error, string? Code)> GetJsonAsync(string relativeUrl, CancellationToken ct = default)
    {
        var client = CreateClient();
        var sentWithToken = !string.IsNullOrEmpty(AppState.Token);
        try
        {
            var resp = await client.GetAsync(relativeUrl, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                var (code, msg, _) = ParseApiError(body);
                if (resp.StatusCode == HttpStatusCode.Unauthorized) OnUnauthorized(sentWithToken);
                return (null, (int)resp.StatusCode, msg ?? $"请求失败（{(int)resp.StatusCode}）。", code);
            }
            if (string.IsNullOrWhiteSpace(body))
                return (null, 200, null, null);
            using var doc = JsonDocument.Parse(body);
            var el = doc.RootElement.Clone();
            return (el, 200, null, null);
        }
        catch (Exception ex)
        {
            return (null, 0, "网络或解析错误：" + ex.Message, null);
        }
    }

    /// <summary>纯文本读取：不解析 JSON，供 /health、/metrics 等非 JSON 端点使用。</summary>
    public async Task<(string? Text, int Status, string? Error)> GetTextAsync(string relativeUrl, CancellationToken ct = default)
    {
        var client = CreateClient();
        var sentWithToken = !string.IsNullOrEmpty(AppState.Token);
        try
        {
            var resp = await client.GetAsync(relativeUrl, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                if (resp.StatusCode == HttpStatusCode.Unauthorized) OnUnauthorized(sentWithToken);
                return (null, (int)resp.StatusCode, $"请求失败（{(int)resp.StatusCode}）。");
            }
            return (body, (int)resp.StatusCode, null);
        }
        catch (Exception ex)
        {
            return (null, 0, "网络错误：" + ex.Message);
        }
    }

    /// <summary>
    /// 通用写操作：统一处理 401 回收与错误体解析，避免每个页面重复实现。DELETE 与 GET 不发送请求体。
    /// </summary>
    public async Task<(bool Ok, int Status, string? Error, string? Code)> SendAsync(
        HttpMethod method, string relativeUrl, object? body = null, CancellationToken ct = default)
    {
        var client = CreateClient();
        var sentWithToken = !string.IsNullOrEmpty(AppState.Token);
        try
        {
            using var req = new HttpRequestMessage(method, relativeUrl);
            if (body is not null && method != HttpMethod.Get && method != HttpMethod.Delete)
                req.Content = JsonContent.Create(body);

            var resp = await client.SendAsync(req, ct);
            if (resp.StatusCode == HttpStatusCode.Unauthorized) OnUnauthorized(sentWithToken);
            if (!resp.IsSuccessStatusCode)
            {
                var raw = await resp.Content.ReadAsStringAsync(ct);
                var (code, msg, _) = ParseApiError(raw);
                return (false, (int)resp.StatusCode, msg ?? $"请求失败（{(int)resp.StatusCode}）。", code);
            }
            return (true, (int)resp.StatusCode, null, null);
        }
        catch (Exception ex)
        {
            return (false, 0, "网络错误：" + ex.Message, null);
        }
    }

    public Task<(bool Ok, int Status, string? Error, string? Code)> PostAsync(string relativeUrl, object? body = null, CancellationToken ct = default)
        => SendAsync(HttpMethod.Post, relativeUrl, body, ct);

    public Task<(bool Ok, int Status, string? Error, string? Code)> PutAsync(string relativeUrl, object? body, CancellationToken ct = default)
        => SendAsync(HttpMethod.Put, relativeUrl, body, ct);

    public Task<(bool Ok, int Status, string? Error, string? Code)> PatchAsync(string relativeUrl, object? body = null, CancellationToken ct = default)
        => SendAsync(HttpMethod.Patch, relativeUrl, body, ct);

    public Task<(bool Ok, int Status, string? Error, string? Code)> DeleteAsync(string relativeUrl, CancellationToken ct = default)
        => SendAsync(HttpMethod.Delete, relativeUrl, null, ct);
}
