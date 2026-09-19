using Microsoft.AspNetCore.Components;
using SuperBuilder_AI.Components.Services;

namespace SuperBuilder_AI.Maui.Services;

/// <summary>
/// MAUI 端 <see cref="ILoginCompletion"/> 实现（Phase 1）。
///
/// <para>
/// <see cref="AuthStore.SetFromLoginAsync"/> 已通过 <see cref="MauiAuthPersistence"/> 将令牌写入本地存储，
/// 故登录完成后仅需导航到目标页；登出则清本地存储并跳登录。
/// </para>
/// </summary>
public sealed class MauiLoginCompletion : ILoginCompletion
{
    private readonly AuthStore _session;
    private readonly NavigationManager _nav;

    public MauiLoginCompletion(AuthStore session, NavigationManager nav)
    {
        _session = session;
        _nav = nav;
    }

    public Task CompleteLoginAsync(AuthResult result, string? returnUrl = null)
    {
        _nav.NavigateTo(returnUrl ?? "/ask");
        return Task.CompletedTask;
    }

    public async Task LogoutAsync()
    {
        await _session.ClearAsync();
        _nav.NavigateTo("/login");
    }
}
