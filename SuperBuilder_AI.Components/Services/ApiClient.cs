using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

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

    public async Task<AuthResult?> LoginAsync(string username, long tenantId, CancellationToken ct = default)
    {
        var client = _factory.CreateClient("SuperBuilderApi");
        var resp = await client.PostAsJsonAsync("api/auth/login", new { username, tenantId }, ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<AuthResult>(ct);
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

    public async Task<T?> GetAsync<T>(string relativeUrl, CancellationToken ct = default) where T : class
    {
        var client = CreateClient();
        return await client.GetFromJsonAsync<T>(relativeUrl, ct);
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
