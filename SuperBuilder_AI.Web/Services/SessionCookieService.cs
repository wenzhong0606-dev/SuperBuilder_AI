using Microsoft.AspNetCore.Http;

namespace SuperBuilder_AI.Web.Services;

/// <summary>
/// 会话 cookie 写入/清除服务（Phase 1）。
///
/// <para>
/// 仅由普通 HTTP 端点（登录代理 / 登出）调用——Blazor 组件事件运行在 SignalR 电路内，
/// 没有可用的普通 HTTP response 可写 <c>Set-Cookie</c>（见 hardening §8.1(j)），因此本服务
/// 不直接被组件注入使用。
/// </para>
///
/// <para>
/// cookie 属性：<c>HttpOnly; Secure(HTTPS 时); SameSite=Lax; Path=/</c>。
/// <c>Secure</c> 跟随请求是否 HTTPS（本地 http 开发环境也能正常存储，生产 HTTPS 自动启用）。
/// </para>
/// </summary>
public sealed class SessionCookieService
{
    public const string CookieName = "sb_sess";

    public void Set(HttpContext ctx, string sessionId)
    {
        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = ctx.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
        };
        ctx.Response.Cookies.Append(CookieName, sessionId, options);
    }

    public void Clear(HttpContext ctx)
    {
        ctx.Response.Cookies.Delete(CookieName, new CookieOptions
        {
            Path = "/",
            SameSite = SameSiteMode.Lax,
        });
    }

    public string? GetSessionId(HttpContext? ctx) =>
        ctx?.Request.Cookies.TryGetValue(CookieName, out var v) == true ? v : null;
}
