using Microsoft.JSInterop;
using System.Text.Json;
using SuperBuilder_AI.Components.Models;

namespace SuperBuilder_AI.Components.Services;

/// <summary>租户默认语言 + 用户本地偏好的运行时语言服务。</summary>
public sealed class LocalizationService
{
    private readonly IJSRuntime _js;
    private readonly IApiClient _api;
    private long _tenantId;
    private long _userId;
    private readonly Dictionary<string, Dictionary<string, string>> _strings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zh-CN"] = new() { ["app.subtitle"] = "智能问数平台", ["common.settings"] = "个人设置", ["common.logout"] = "退出登录", ["theme.light"] = "切换到浅色", ["theme.dark"] = "切换到深色" },
        ["en-US"] = new() { ["app.subtitle"] = "AI Analytics Platform", ["common.settings"] = "Settings", ["common.logout"] = "Sign out", ["theme.light"] = "Switch to light", ["theme.dark"] = "Switch to dark" },
    };

    public string CurrentCulture { get; private set; } = "zh-CN";
    public IReadOnlyList<string> AvailableCultures { get; private set; } = new[] { "zh-CN" };
    /// <summary>M3-02 平台公开语言目录（culture→显示名/本地名），用于切换器动态展示 NativeName，替代硬编码映射。</summary>
    public IReadOnlyDictionary<string, PublicLanguageView> PublicLanguages { get; private set; }
        = new Dictionary<string, PublicLanguageView>(StringComparer.OrdinalIgnoreCase);
    public event Action? Changed;

    public LocalizationService(IJSRuntime js, IApiClient api) { _js = js; _api = api; }

    /// <summary>M3-02 取语言的本地展示名；缺失时回退文化码。</summary>
    public string NativeName(string culture)
        => PublicLanguages.TryGetValue(culture, out var v) && !string.IsNullOrWhiteSpace(v.NativeName) ? v.NativeName : culture;

    public string T(string key)
    {
        if (_strings.TryGetValue(CurrentCulture, out var current) && current.TryGetValue(key, out var value)) return value;
        return _strings["zh-CN"].TryGetValue(key, out value) ? value : key;
    }

    public string T(string key, string fallback)
    {
        var value = T(key);
        return value == key ? fallback : value;
    }

    public async Task InitializeAsync(long tenantId, long userId, IReadOnlyList<string>? available, string? tenantDefault)
    {
        _tenantId = tenantId; _userId = userId;
        AvailableCultures = available is { Count: > 0 } ? available : new[] { "zh-CN" };
        var fallback = AvailableCultures.Contains(tenantDefault ?? "", StringComparer.OrdinalIgnoreCase) ? tenantDefault! : AvailableCultures[0];

        // M3-G0「用户语言恢复」：已登录优先服务端持久化偏好；其次本机缓存；均缺失回退租户默认。
        string chosen = fallback;
        if (userId > 0)
        {
            var serverCulture = await SafeGetUserLanguageAsync();
            if (!string.IsNullOrEmpty(serverCulture) && AvailableCultures.Contains(serverCulture, StringComparer.OrdinalIgnoreCase))
                chosen = serverCulture!;
        }
        if (chosen == fallback)
        {
            var local = await SafeReadLocalAsync(tenantId, userId);
            if (!string.IsNullOrEmpty(local) && AvailableCultures.Contains(local, StringComparer.OrdinalIgnoreCase))
                chosen = local!;
        }
        CurrentCulture = chosen;
        await LoadPublicLanguagesAsync();
        await LoadRuntimeTextsAsync();
        Changed?.Invoke();
    }

    /// <summary>M3-02 拉取平台公开语言目录（含本地名），填充 <see cref="PublicLanguages"/>；失败不影响主流程。</summary>
    private async Task LoadPublicLanguagesAsync()
    {
        try
        {
            var (list, err) = await _api.GetPublicLanguagesAsync();
            if (err is null && list is { Count: > 0 })
                PublicLanguages = list.ToDictionary(x => x.Culture, x => x, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            // 公开目录取失败则保留空映射，切换器回退展示文化码。
        }
    }

    public async Task SetCultureAsync(long tenantId, long userId, string culture)
    {
        if (!AvailableCultures.Contains(culture, StringComparer.OrdinalIgnoreCase)) return;
        CurrentCulture = culture;
        await LoadRuntimeTextsAsync();
        try { await _js.InvokeVoidAsync("localStorage.setItem", $"sb_culture_{tenantId}_{userId}", culture); } catch { }
        // M3-G0：已登录时同步持久化到服务端，跨设备/清缓存可恢复。
        if (userId > 0) { try { await _api.SetUserLanguageAsync(culture); } catch { } }
        Changed?.Invoke();
    }

    private async Task<string?> SafeGetUserLanguageAsync()
    {
        try { return (await _api.GetUserLanguageAsync()).Culture; }
        catch { return null; }
    }

    private async Task<string?> SafeReadLocalAsync(long tenantId, long userId)
    {
        try { return await _js.InvokeAsync<string?>("localStorage.getItem", $"sb_culture_{tenantId}_{userId}"); }
        catch { return null; }
    }

    private async Task LoadRuntimeTextsAsync()
    {
        var url = _userId <= 0
            ? $"api/localization/public/texts?culture={Uri.EscapeDataString(CurrentCulture)}&tenantId={_tenantId}"
            : $"api/localization/texts?culture={Uri.EscapeDataString(CurrentCulture)}";
        var (data, _, _) = await _api.GetJsonAsync(url);
        if (data is not { ValueKind: JsonValueKind.Array }) return;
        if (!_strings.TryGetValue(CurrentCulture, out var bundle)) _strings[CurrentCulture] = bundle = new(StringComparer.OrdinalIgnoreCase);
        foreach (var row in data.Value.EnumerateArray())
        {
            if (row.TryGetProperty("resourceKey", out var key) && row.TryGetProperty("value", out var value))
                bundle[key.GetString() ?? string.Empty] = value.GetString() ?? string.Empty;
        }
    }
}
