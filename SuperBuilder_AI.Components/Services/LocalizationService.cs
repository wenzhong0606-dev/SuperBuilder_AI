using Microsoft.JSInterop;
using System.Text.Json;

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
    public event Action? Changed;

    public LocalizationService(IJSRuntime js, IApiClient api) { _js = js; _api = api; }

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
        try
        {
            var saved = await _js.InvokeAsync<string?>("localStorage.getItem", $"sb_culture_{tenantId}_{userId}");
            CurrentCulture = AvailableCultures.Contains(saved ?? "", StringComparer.OrdinalIgnoreCase) ? saved! : fallback;
        }
        catch { CurrentCulture = fallback; }
        await LoadRuntimeTextsAsync();
        Changed?.Invoke();
    }

    public async Task SetCultureAsync(long tenantId, long userId, string culture)
    {
        if (!AvailableCultures.Contains(culture, StringComparer.OrdinalIgnoreCase)) return;
        CurrentCulture = culture;
        await LoadRuntimeTextsAsync();
        try { await _js.InvokeVoidAsync("localStorage.setItem", $"sb_culture_{tenantId}_{userId}", culture); } catch { }
        Changed?.Invoke();
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
