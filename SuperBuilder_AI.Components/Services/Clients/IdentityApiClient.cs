using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SuperBuilder_AI.Components.Services;

/// <summary>
/// 身份域客户端实现（M9-01）：登录、租户解析、租户切换、自助注册、自助注册配置、用户语言偏好。
/// 继承 <see cref="ApiClientBase"/> 复用鉴权头/401 回收/错误解析与读原语。
/// </summary>
public sealed class IdentityApiClient : ApiClientBase, IIdentityApiClient
{
    public IdentityApiClient(IHttpClientFactory factory, AppState appState) : base(factory, appState) { }

    public async Task<(AuthResult? Result, string? Error, string? Code)> LoginAsync(string username, long tenantId, string? password = null, CancellationToken ct = default)
    {
        var client = Factory.CreateClient("SuperBuilderApi");
        try
        {
            var resp = await client.PostAsJsonAsync("api/auth/login", new { username, tenantId, password }, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var (code, msg, _) = ParseApiError(await resp.Content.ReadAsStringAsync(ct));
                return (null, msg ?? $"登录失败（{(int)resp.StatusCode}）。", code);
            }
            var r = await resp.Content.ReadFromJsonAsync<AuthResult>(ct);
            return (r, null, null);
        }
        catch (HttpRequestException ex)
        {
            return (null, "无法连接登录服务，请确认 API 服务已启动且地址配置正确。" +
                (string.IsNullOrWhiteSpace(ex.Message) ? "" : $"（{ex.Message}）"), null);
        }
    }

    public async Task<(long Id, string? TenantCode, string? Name, string? Error)> ResolveTenantByCodeAsync(string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return (0, null, null, "租户编码必填。");
        var (data, _, error, _) = await GetJsonAsync($"api/auth/tenant-by-code?code={System.Uri.EscapeDataString(code)}", ct);
        if (data is not { ValueKind: JsonValueKind.Object }) return (0, null, null, error ?? "租户解析失败。");
        var id = data.Value.TryGetProperty("id", out var idEl) && idEl.TryGetInt64(out var idVal) ? idVal : 0L;
        if (id <= 0) return (0, null, null, error ?? "租户解析失败。");
        var tenantCode = data.Value.TryGetProperty("tenantCode", out var tc) ? tc.GetString() : null;
        var name = data.Value.TryGetProperty("name", out var nm) ? nm.GetString() : null;
        return (id, tenantCode, name, null);
    }

    public async Task<(TenantSwitchResult? Result, string? Error, string? Code)> SwitchTenantAsync(long tenantId, CancellationToken ct = default)
    {
        var client = CreateClient();
        var resp = await client.PostAsJsonAsync("api/tenant-membership/switch", new { tenantId }, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var (code, msg, _) = ParseApiError(await resp.Content.ReadAsStringAsync(ct));
            if (resp.StatusCode == HttpStatusCode.Unauthorized) OnUnauthorized();
            return (null, msg ?? $"切换失败（{(int)resp.StatusCode}）。", code);
        }
            var r = await resp.Content.ReadFromJsonAsync<TenantSwitchResult>(ct);
            return (r, null, null);
    }

    public async Task<(SelfRegistrationResult? Result, string? Error, string? Code)> RegisterSelfAsync(
        string tenantCode, string tenantName, string adminUsername, string adminEmail,
        string adminPassword, string? adminDisplayName = null, CancellationToken ct = default)
    {
        var client = Factory.CreateClient("SuperBuilderApi");
        try
        {
            var resp = await client.PostAsJsonAsync("api/self-registration/register", new
            {
                tenantCode,
                tenantName,
                adminUsername,
                adminEmail,
                adminPassword,
                adminDisplayName,
            }, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var (code, msg, _) = ParseApiError(await resp.Content.ReadAsStringAsync(ct));
                return (null, msg ?? $"注册失败（{(int)resp.StatusCode}）。", code);
            }
            var r = await resp.Content.ReadFromJsonAsync<SelfRegistrationResult>(ct);
            return (r, null, null);
        }
        catch (HttpRequestException ex)
        {
            return (null, "无法连接注册服务，请确认 API 服务已启动且地址配置正确。" +
                (string.IsNullOrWhiteSpace(ex.Message) ? "" : $"（{ex.Message}）"), null);
        }
    }

    public async Task<(SelfRegistrationConfigView? Result, string? Error)> GetSelfRegistrationConfigAsync(CancellationToken ct = default)
    {
        var (data, _, err, _) = await GetJsonAsync("api/self-registration/config", ct);
        if (data is not { ValueKind: JsonValueKind.Object })
            return (null, err ?? "无法读取自助注册配置。");
        var v = data.Value;
        var enabled = v.TryGetProperty("enabled", out var e) && e.GetBoolean();
        var approval = v.TryGetProperty("approvalRequired", out var a) && a.GetBoolean();
        var captcha = v.TryGetProperty("requireCaptcha", out var c) && c.GetBoolean();
        var defaultCulture = v.TryGetProperty("defaultCulture", out var dc) ? dc.GetString() ?? "zh-CN" : "zh-CN";
        var domains = v.TryGetProperty("allowedEmailDomains", out var d) && d.ValueKind == JsonValueKind.Array
            ? d.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList()
            : new List<string>();
        var cultures = v.TryGetProperty("defaultAvailableCultures", out var cc) && cc.ValueKind == JsonValueKind.Array
            ? cc.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList()
            : new List<string> { "zh-CN" };
        return (new SelfRegistrationConfigView
        {
            Enabled = enabled,
            AllowedEmailDomains = domains,
            DefaultCulture = defaultCulture,
            DefaultAvailableCultures = cultures,
            ApprovalRequired = approval,
            RequireCaptcha = captcha,
        }, null);
    }

    public async Task<(string? Culture, string? Error)> GetUserLanguageAsync(CancellationToken ct = default)
    {
        var (data, status, error, _) = await GetJsonAsync("api/user/preferences/language", ct);
        if (error != null) return (null, error);
        if (data is not { ValueKind: JsonValueKind.Object }) return (null, null);
        var culture = data.Value.TryGetProperty("culture", out var c) ? c.GetString() : null;
        return (culture, null);
    }

    public async Task<(string? Culture, string? Error, string? Code)> SetUserLanguageAsync(string culture, CancellationToken ct = default)
    {
        var client = CreateClient();
        try
        {
            var resp = await client.PutAsJsonAsync("api/user/preferences/language", new { culture }, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var (code, msg, _) = ParseApiError(await resp.Content.ReadAsStringAsync(ct));
                if (resp.StatusCode == HttpStatusCode.Unauthorized) OnUnauthorized();
                return (null, msg ?? $"保存语言偏好失败（{(int)resp.StatusCode}）。", code);
            }
            var (data, _, err, _) = await GetJsonAsync("api/user/preferences/language", ct);
            var effective = (err == null && data is { ValueKind: JsonValueKind.Object } && data.Value.TryGetProperty("culture", out var c))
                ? c.GetString()
                : culture;
            return (effective, null, null);
        }
        catch (HttpRequestException ex)
        {
            return (null, "无法连接服务，请确认 API 已启动且地址配置正确。" +
                (string.IsNullOrWhiteSpace(ex.Message) ? "" : $"（{ex.Message}）"), null);
        }
    }
}
