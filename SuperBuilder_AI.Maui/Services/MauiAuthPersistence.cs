using System.Text.Json;
using Microsoft.JSInterop;
using SuperBuilder_AI.Components.Services;

namespace SuperBuilder_AI.Maui.Services;

/// <summary>
/// MAUI 端 <see cref="IAuthPersistence"/> 实现（Phase 1，M8-05 加固）。
///
/// <para>
/// MAUI Hybrid 运行在 WebView 内、无 httpOnly cookie 概念，因此沿用改造前的本地存储方案
/// （localStorage 经 <see cref="IJSRuntime"/> 互操作），保持登录持久化行为不变。
/// 令牌存于设备本地存储是 MAUI 既有且计划明确豁免的取舍（见 hardening §3 Phase 1 任务 1）。
/// </para>
/// </summary>
public sealed class MauiAuthPersistence : IAuthPersistence
{
    private readonly IJSRuntime _js;
    private const string Key = "sb_auth_v1";

    public MauiAuthPersistence(IJSRuntime js) => _js = js;

    public async Task SaveAsync(SessionData data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data);
            await _js.InvokeVoidAsync("localStorage.setItem", Key, json);
        }
        catch
        {
            // 存储不可用时静默跳过，不影响登录态内存态。
        }
    }

    public async Task<SessionData?> LoadAsync()
    {
        try
        {
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", Key);
            if (string.IsNullOrEmpty(json)) return null;
            return JsonSerializer.Deserialize<SessionData>(json);
        }
        catch
        {
            return null;
        }
    }

    public async Task ClearAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", Key);
        }
        catch
        {
            // 忽略
        }
    }
}
