using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Components.Models;

namespace SuperBuilder_AI.Components.Services;

/// <summary>
/// 基于命名 HttpClient 的 API 客户端实现。Head 项目负责注册名为 "SuperBuilderApi" 的 HttpClient（BaseAddress=API 地址）。
/// 租户隔离由 <see cref="AppState.TenantId"/> 经 X-Tenant-Id 头透传，与现有控制器约定一致。
/// </summary>
public sealed class ApiClient : IApiClient
{
    private readonly IHttpClientFactory _factory;
    private readonly AppState _appState;

    public ApiClient(IHttpClientFactory factory, AppState appState)
    {
        _factory = factory;
        _appState = appState;
    }

    private HttpClient CreateClient()
    {
        var client = _factory.CreateClient("SuperBuilderApi");
        client.DefaultRequestHeaders.Authorization = null;
        client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        if (!string.IsNullOrEmpty(_appState.Token))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _appState.Token);
        if (_appState.TenantId > 0)
            client.DefaultRequestHeaders.Add("X-Tenant-Id", _appState.TenantId.ToString());
        return client;
    }

    public async Task<(AuthResult? Result, string? Error)> LoginAsync(string username, long tenantId, CancellationToken ct = default)
    {
        var client = _factory.CreateClient("SuperBuilderApi");
        var resp = await client.PostAsJsonAsync("api/auth/login", new { username, tenantId }, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var (code, msg, _) = ParseApiError(await resp.Content.ReadAsStringAsync(ct));
            return (null, msg ?? $"登录失败（{(int)resp.StatusCode}）。");
        }
        var r = await resp.Content.ReadFromJsonAsync<AuthResult>(ct);
        return (r, null);
    }

    public async Task<string?> AskRawAsync(string question, long? dataSourceId, CancellationToken ct = default)
    {
        var client = CreateClient();
        var payload = new { question, dataSourceId = dataSourceId ?? 0L };
        var resp = await client.PostAsJsonAsync("api/ask", payload, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            return "ERROR " + (int)resp.StatusCode + ": " + err;
        }
        return await resp.Content.ReadAsStringAsync(ct);
    }

    /// <summary>类型化问数：反序列化为 <see cref="BIResponse"/>，并对非成功状态解析统一错误码。</summary>
    public async Task<AskOutcome> AskAsync(string question, long? dataSourceId, CancellationToken ct = default)
    {
        var client = CreateClient();
        var resp = await client.PostAsJsonAsync("api/ask", new { question, dataSourceId = dataSourceId ?? 0L }, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var (code, msg, trace) = ParseApiError(await resp.Content.ReadAsStringAsync(ct));
            return new AskOutcome
            {
                Code = code,
                TraceId = trace,
                Error = msg ?? $"问数失败（{(int)resp.StatusCode}）。"
            };
        }

        var raw = await resp.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrEmpty(raw))
            return new AskOutcome { Error = "请求失败：空响应。" };

        try
        {
            var opt = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var response = JsonSerializer.Deserialize<BIResponse>(raw, opt);
            if (response is null)
                return new AskOutcome { Error = "响应解析失败。" };
            return new AskOutcome { Response = response };
        }
        catch (JsonException ex)
        {
            return new AskOutcome { Error = "响应解析失败：" + ex.Message };
        }
    }

    /// <summary>
    /// 解析后端统一错误体：优先 <see cref="ApiError"/>(code/message/traceId)，
    /// 其次兼容旧 <c>{ error }</c> 形状，最后回退到原始文本（截断避免过长）。
    /// </summary>
    private static (string? Code, string? Message, string? TraceId) ParseApiError(string body)
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
            if (raw.ValueKind == JsonValueKind.Object && raw.TryGetProperty("error", out var ev) && ev.ValueKind == JsonValueKind.String)
                return (null, ev.GetString(), null);
        }
        catch
        {
            // 非 JSON 则回退原始文本
        }
        var trimmed = body.Length > 240 ? body[..240] + "…" : body;
        return (null, trimmed, null);
    }

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
        return (false, null, $"{(int)resp.StatusCode}: {err}");
    }

    public async Task<T?> GetAsync<T>(string relativeUrl, CancellationToken ct = default) where T : class
    {
        var client = CreateClient();
        return await client.GetFromJsonAsync<T>(relativeUrl, ct);
    }

    /// <summary>
    /// 读取任意 JSON 端点为 <see cref="JsonElement"/>，失败时返回错误信息且不抛异常。
    /// 用于在不确定后端 DTO 精确结构时安全渲染列表/详情。
    /// </summary>
    public async Task<(JsonElement? Data, int Status, string? Error)> GetJsonAsync(string relativeUrl, CancellationToken ct = default)
    {
        var client = CreateClient();
        try
        {
            var resp = await client.GetAsync(relativeUrl, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                var (code, msg, _) = ParseApiError(body);
                return (null, (int)resp.StatusCode, msg ?? $"请求失败（{(int)resp.StatusCode}）。");
            }
            if (string.IsNullOrWhiteSpace(body))
                return (null, 200, null);
            using var doc = JsonDocument.Parse(body);
            var el = doc.RootElement.Clone();
            return (el, 200, null);
        }
        catch (Exception ex)
        {
            return (null, 0, "网络或解析错误：" + ex.Message);
        }
    }
}

/// <summary>登录 / 当前用户响应（与 api/auth 的 AuthResult 字段对齐）。</summary>
public sealed class AuthResult
{
    public string? Token { get; set; }
    public int ExpiresInSeconds { get; set; }
    public long TenantId { get; set; }
    public long UserId { get; set; }
    public string Username { get; set; } = "";
    public System.Collections.Generic.List<string>? Permissions { get; set; }
}
