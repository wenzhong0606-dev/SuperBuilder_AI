using Microsoft.JSInterop;
using System.Text.Json;
using SuperBuilder_AI.Components.Localization;
using SuperBuilder_AI.Components.Models;

namespace SuperBuilder_AI.Components.Services;

/// <summary>租户默认语言 + 用户本地偏好的运行时语言服务。</summary>
public sealed class LocalizationService
{
    private readonly IJSRuntime _js;
    private readonly IApiClient _api;
    private long _tenantId;
    private long _userId;
    // 运行时由 /api/localization/(public/)texts 拉取并覆盖；离线时回退到 Keys.Defaults（见 T）。
    private readonly Dictionary<string, Dictionary<string, string>> _strings = new(StringComparer.OrdinalIgnoreCase);

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
        // 1) 运行时按当前文化加载的译文（来自平台基线 + 租户覆盖）。
        if (_strings.TryGetValue(CurrentCulture, out var current) && current.TryGetValue(key, out var value)) return value;
        // 2) 离线回退：zh 系列用中文默认，其余用 en-US 国际默认。
        if (Keys.Defaults.TryGetValue(key, out var d)) return IsZh(CurrentCulture) ? d.ZhCn : d.EnUs;
        // 3) 兜底返回键本身（页面应始终提供 fallback 参数以免暴露原始键）。
        return key;
    }

    private static bool IsZh(string culture) => culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

    public string T(string key, string fallback)
    {
        var value = T(key);
        return value == key ? fallback : value;
    }

    /// <summary>
    /// 按后端错误码取本地化友好提示（ApiError 友好层核心）：
    /// key = <c>"Error." + code</c>（与 <see cref="Keys.Error.SB_*"/> / 后端 <c>ResourceKeys.Error.SB_*</c> 对应），
    /// 命中则返回当前语言的本地化文案；未命中（未知码 / 离线）则回退到服务端下发的 <paramref name="serverMessage"/>。
    /// <paramref name="code"/> 或 <paramref name="serverMessage"/> 为空时直接返回 <paramref name="serverMessage"/>。
    /// 语义：非中文语言下按码本地化，中文或缺失时透传服务端友好文案（与服务端 <c>ErrorCodes.Friendly</c> 一致）。
    /// </summary>
    public string Friendly(string? code, string? serverMessage)
    {
        if (string.IsNullOrWhiteSpace(code)) return serverMessage ?? "";
        return T("Error." + code, serverMessage ?? "");
    }

    public async Task InitializeAsync(long tenantId, long userId, IReadOnlyList<string>? available, string? tenantDefault)
    {
        _tenantId = tenantId; _userId = userId;
        AvailableCultures = available is { Count: > 0 } ? available : new[] { "zh-CN" };
        var fallback = AvailableCultures.Contains(tenantDefault ?? "", StringComparer.OrdinalIgnoreCase) ? tenantDefault! : AvailableCultures[0];

        // M3-G0「用户语言恢复」：已登录优先服务端持久化偏好；其次本机缓存；均缺失回退租户默认。
        string chosen = fallback;
        bool hasServerPreference = false;
        if (userId > 0)
        {
            var serverCulture = await SafeGetUserLanguageAsync();
            if (!string.IsNullOrEmpty(serverCulture) && AvailableCultures.Contains(serverCulture, StringComparer.OrdinalIgnoreCase))
            {
                chosen = serverCulture!;
                hasServerPreference = true;
            }
        }
        if (!hasServerPreference)
        {
            var local = await SafeReadLocalAsync(tenantId, userId);
            if (!string.IsNullOrEmpty(local) && AvailableCultures.Contains(local, StringComparer.OrdinalIgnoreCase))
                chosen = local!;
        }
        CurrentCulture = AvailableCultures.First(c => string.Equals(c, chosen, StringComparison.OrdinalIgnoreCase));
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
        // 先持久化到 localStorage：必须与 ThemeService/AuthStore 一致，作为首个 await 发起 JS 互操作。
        // Blazor Server 中若先 await 其他任务（如 LoadRuntimeTextsAsync 的 HTTP 往返）再调 IJSRuntime，
        // 续体在电路同步上下文上派发 JS 调用会触发 TaskCanceledException，导致语言偏好丢失（刷新即还原）。
        try { await _js.InvokeVoidAsync("localStorage.setItem", $"sb_culture_{tenantId}_{userId}", culture); }
        catch { /* 预渲染/JS 不可用时静默降级 */ }
        // 再加载新文化的运行时译文（失败不影响已切换与已持久化）
        await LoadRuntimeTextsAsync();
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
        var (data, _, _, _) = await _api.GetJsonAsync(url);
        if (data is not { ValueKind: JsonValueKind.Array }) return;
        if (!_strings.TryGetValue(CurrentCulture, out var bundle)) _strings[CurrentCulture] = bundle = new(StringComparer.OrdinalIgnoreCase);
        foreach (var row in data.Value.EnumerateArray())
        {
            if (row.TryGetProperty("resourceKey", out var key) && row.TryGetProperty("value", out var value))
                bundle[key.GetString() ?? string.Empty] = value.GetString() ?? string.Empty;
        }
    }
}
