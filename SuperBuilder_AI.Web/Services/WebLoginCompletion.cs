using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SuperBuilder_AI.Components.Services;

namespace SuperBuilder_AI.Web.Services;

/// <summary>Web 端 <see cref="ILoginCompletion"/> 实现（Phase 1 + 验收 #4）：经一次性交接码写 cookie。</summary>
public sealed class WebLoginCompletion : ILoginCompletion
{
	private readonly IWebSessionIssuer _issuer;
	private readonly NavigationManager _nav;
	private readonly IJSRuntime _js;

	public WebLoginCompletion(IWebSessionIssuer issuer, NavigationManager nav, IJSRuntime js)
	{
		_issuer = issuer;
		_nav = nav;
		_js = js;
	}

	public async Task CompleteLoginAsync(AuthResult result, string? returnUrl = null)
	{
		var data = SessionData.FromLoginResult(result);
		var code = await _issuer.IssueAsync(data, returnUrl);

		// 验收 #4：登录完成改为浏览器端 POST（带 antiforgery 令牌，令牌由 _Host.cshtml 注入 window.__afToken），
		// 交接码经请求体发送（不再经 URL），服务端校验 antiforgery 后写 httpOnly cookie 并返回重定向目标。
		var redirect = await _js.InvokeAsync<string?>("submitSessionStart", code);
		_nav.NavigateTo(string.IsNullOrEmpty(redirect) ? "/" : redirect, forceLoad: true);
	}

	public async Task LogoutAsync()
	{
		// 验收 #4：登出同样走 POST + antiforgery，避免 GET 改状态与 CSRF。
		await _js.InvokeAsync<bool>("submitLogout");
		_nav.NavigateTo("/login", forceLoad: true);
	}
}
