using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace SuperBuilder_AI.E2E;

/// <summary>
/// E2E 内浏览器侧 API 调用助手：从 localStorage 会话快照（<c>sb_auth_v1</c>）取 Bearer 令牌与租户上下文，
/// 向 API 基地址发请求。用于以 API 驱动方式覆盖业务链中 UI 形态易变、或纯后端即可验证的环节
/// （DataSource 创建/扫描、Dashboard 创建、租户隔离校验），降低 UI locator 脆弱性。
/// <para>浏览器侧 <c>fetch</c> 为跨域请求（Web 宿主不转发 /api/**），必须显式指向 <see cref="E2EConfig.ApiUrl"/>。</para>
/// </summary>
internal static class E2EApiHelper
{
    public static async Task<(string? Token, long TenantId, long UserId, string? Username)> GetAuthAsync(IPage page)
    {
        var json = await page.EvaluateAsync<string?>("() => localStorage.getItem('sb_auth_v1')");
        if (string.IsNullOrWhiteSpace(json)) return (null, 0, 0, null);
        using var doc = JsonDocument.Parse(json!);
        var root = doc.RootElement;
        var token = root.TryGetProperty("Token", out var t) ? t.GetString() : null;
        var tid = root.TryGetProperty("TenantId", out var ti) && ti.ValueKind == JsonValueKind.Number ? ti.GetInt64() : 0;
        var uid = root.TryGetProperty("UserId", out var ui) && ui.ValueKind == JsonValueKind.Number ? ui.GetInt64() : 0;
        var uname = root.TryGetProperty("Username", out var u) ? u.GetString() : null;
        return (token, tid, uid, uname);
    }

    public static async Task<(bool Ok, int Status, string Body)> CallApiAsync(IPage page, string method, string path, object? body = null)
    {
        var (token, _, _, _) = await GetAuthAsync(page);
        var apiBase = E2EConfig.ApiUrl.TrimEnd('/');
        var result = await page.EvaluateAsync<JsonElement>(
            "async (c) => {" +
            "  const headers = { 'Content-Type': 'application/json' };" +
            "  if (c.token) headers['Authorization'] = 'Bearer ' + c.token;" +
            "  const init = { method: c.method, headers };" +
            "  if (c.body) init.body = JSON.stringify(c.body);" +
            "  const r = await fetch(c.apiBase + '/' + c.path, init);" +
            "  const t = await r.text();" +
            "  return JSON.stringify({ status: r.status, ok: r.ok, body: t });" +
            "}",
            new { apiBase, method, path, token, body });
        var status = result.GetProperty("status").GetInt32();
        var ok = result.GetProperty("ok").GetBoolean();
        var bodyStr = result.GetProperty("body").GetString() ?? "";
        return (ok, status, bodyStr);
    }
}
