using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace SuperBuilder_AI.E2E;

/// <summary>
/// E2E 内 API 调用助手：Phase 1 起浏览器不再持有令牌（令牌仅驻服务端会话，由 <c>sb_sess</c> cookie 间接引用），
/// 故不再从 <c>localStorage['sb_auth_v1']</c> 读取 Bearer。
/// 本助手改为以服务端（C# <see cref="HttpClient"/>）经 <c>/api/auth/login</c> 取令牌
/// （先 <c>/api/auth/tenant-by-code</c> 把编码解析为数字租户 id），再以 Bearer 调用目标 API。
/// <para>
/// 与浏览器会话（UI 登录建立的 <c>sb_sess</c> cookie）相互独立：浏览器侧用于 UI 渲染断言，
/// 本助手用于以 API 驱动方式覆盖业务链中纯后端即可验证的环节（DataSource 创建/扫描、Dashboard 创建、租户隔离校验），
/// 降低 UI locator 脆弱性。Web 宿主不转发 <c>/api/**</c>，故必须显式指向 <see cref="E2EConfig.ApiUrl"/>。
/// </para>
/// </summary>
internal static class E2EApiHelper
{
    private sealed record CachedToken(string Token, long TenantId, long UserId, string Username, DateTime ExpiresAtUtc);

    // 按 用户|编码 缓存令牌，避免每次 API 调用都登录（限流 + 性能）。Phase 1 令牌时效为 60min。
    private static readonly Dictionary<string, CachedToken> _tokenCache = new();
    private static readonly object _cacheLock = new();

    public static async Task<(string? Token, long TenantId, long UserId, string? Username)> GetAuthAsync(IPage page)
        => await GetAuthAsync(E2EConfig.User!, E2EConfig.Password!, E2EConfig.Tenant!);

    /// <summary>以服务端身份登录 API 取令牌（含租户编码→id 解析），并缓存至临近过期。</summary>
    public static async Task<(string? Token, long TenantId, long UserId, string? Username)> GetAuthAsync(string user, string password, string tenantCode)
    {
        var key = $"{user}|{tenantCode}";
        lock (_cacheLock)
        {
            if (_tokenCache.TryGetValue(key, out var cached) && cached.ExpiresAtUtc > DateTime.UtcNow.AddMinutes(2))
                return (cached.Token, cached.TenantId, cached.UserId, cached.Username);
        }

        var apiBase = E2EConfig.ApiUrl.TrimEnd('/');
        using var http = new HttpClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // 1) 租户编码 → 数字 id（API 按数字 TenantId 校验，不包含 platform 排除等 UI 入口限制）
        var codeResp = await http.GetAsync($"{apiBase}/api/auth/tenant-by-code?code={Uri.EscapeDataString(tenantCode)}", cts.Token);
        codeResp.EnsureSuccessStatusCode();
        using (var codeDoc = JsonDocument.Parse(await codeResp.Content.ReadAsStringAsync(cts.Token)))
        {
            var tenantId = codeDoc.RootElement.GetProperty("id").GetInt64();

            // 2) 登录取令牌
            var loginResp = await http.PostAsJsonAsync($"{apiBase}/api/auth/login",
                new { Username = user, TenantId = tenantId, Password = password }, cts.Token);
            loginResp.EnsureSuccessStatusCode();
            using var loginDoc = JsonDocument.Parse(await loginResp.Content.ReadAsStringAsync(cts.Token));
            var root = loginDoc.RootElement;
            var token = root.GetProperty("token").GetString()
                ?? throw new InvalidOperationException("登录响应缺少 token 字段");
            var tid = root.GetProperty("tenantId").GetInt64();
            var uid = root.GetProperty("userId").GetInt64();
            var uname = root.GetProperty("username").GetString() ?? user;
            var expiresIn = root.TryGetProperty("expiresInSeconds", out var e) && e.ValueKind == JsonValueKind.Number ? e.GetInt64() : 3600;
            var cached = new CachedToken(token, tid, uid, uname, DateTime.UtcNow.AddSeconds(expiresIn));
            lock (_cacheLock) _tokenCache[key] = cached;
            return (token, tid, uid, uname);
        }
    }

    public static async Task<(bool Ok, int Status, string Body)> CallApiAsync(IPage page, string method, string path, object? body = null)
        => await CallApiAsync(method, path, body, E2EConfig.User!, E2EConfig.Password!, E2EConfig.Tenant!);

    /// <summary>以服务端身份（默认管理员凭据）调用目标 API，返回 (是否成功, 状态码, 响应体)。</summary>
    public static async Task<(bool Ok, int Status, string Body)> CallApiAsync(string method, string path, object? body, string user, string password, string tenantCode)
    {
        var (token, _, _, _) = await GetAuthAsync(user, password, tenantCode);
        var apiBase = E2EConfig.ApiUrl.TrimEnd('/');
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var url = $"{apiBase}/{path.TrimStart('/')}";
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        HttpResponseMessage resp;
        var verb = method.Trim().ToUpperInvariant();
        if (body is null)
        {
            resp = verb switch
            {
                "GET" => await http.GetAsync(url, cts.Token),
                "DELETE" => await http.DeleteAsync(url, cts.Token),
                _ => await http.SendAsync(new HttpRequestMessage(new HttpMethod(verb), url), cts.Token),
            };
        }
        else
        {
            resp = await http.SendAsync(new HttpRequestMessage(new HttpMethod(verb), url)
            {
                Content = JsonContent.Create(body),
            }, cts.Token);
        }

        var bodyStr = await resp.Content.ReadAsStringAsync(cts.Token);
        return (resp.IsSuccessStatusCode, (int)resp.StatusCode, bodyStr);
    }
}
