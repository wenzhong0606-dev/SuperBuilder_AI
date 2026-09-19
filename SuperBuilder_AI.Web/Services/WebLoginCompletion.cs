using Microsoft.AspNetCore.Components;
using SuperBuilder_AI.Components.Services;

namespace SuperBuilder_AI.Web.Services;

/// <summary>Web 端 <see cref="ILoginCompletion"/> 实现（Phase 1）：经一次性交接码写 cookie。</summary>
public sealed class WebLoginCompletion : ILoginCompletion
{
    private readonly IWebSessionIssuer _issuer;
    private readonly NavigationManager _nav;

    public WebLoginCompletion(IWebSessionIssuer issuer, NavigationManager nav)
    {
        _issuer = issuer;
        _nav = nav;
    }

    public async Task CompleteLoginAsync(AuthResult result, string? returnUrl = null)
    {
        var data = SessionData.FromLoginResult(result);
        var code = await _issuer.IssueAsync(data, returnUrl);
        _nav.NavigateTo($"/auth/session/start?code={code}&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}", forceLoad: true);
    }

    public Task LogoutAsync()
    {
        _nav.NavigateTo("/auth/session/end", forceLoad: true);
        return Task.CompletedTask;
    }
}
