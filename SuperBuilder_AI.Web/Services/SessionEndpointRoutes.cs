using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using SuperBuilder_AI.Components.Services;

namespace SuperBuilder_AI.Web.Services;

/// <summary>Phase 1 会话 HTTP 端点（仅这些端点写/清 httpOnly cookie，组件事件不直接写 cookie）。</summary>
public static class SessionEndpointRoutes
{
    /// <summary>注册 /auth/session/start（交接码消费）与 /auth/session/end（登出）。</summary>
    public static void MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        // 一次性交接码 → 写入服务端会话 + httpOnly cookie，再重定向到目标页。
        app.MapGet("/auth/session/start", (HttpContext ctx, string? code, string? returnUrl,
            IWebSessionStore store, PendingHandoffStore pending, SessionCookieService cookie) =>
        {
            if (string.IsNullOrEmpty(code) || !pending.TryConsume(code, out var data, out var storedReturnUrl))
                return Results.Redirect("/login");

            // 防会话固定（hardening §8.2(d)）：登录前若已持有旧会话，先使其失效再签发全新会话 id。
            var prior = cookie.GetSessionId(ctx);
            if (prior is not null) store.Remove(prior);

            var target = SafeReturnUrl(storedReturnUrl ?? returnUrl);
            var sessionId = store.Create(data!);
            cookie.Set(ctx, sessionId);
            return Results.Redirect(target);
        });

        // 登出：清服务端会话 + 清 cookie，重定向登录页。
        app.MapGet("/auth/session/end", (HttpContext ctx, IWebSessionStore store, SessionCookieService cookie) =>
        {
            var id = cookie.GetSessionId(ctx);
            if (id is not null) store.Remove(id);
            cookie.Clear(ctx);
            return Results.Redirect("/login");
        });
    }

    /// <summary>开放重定向防护：仅允许站内相对路径，否则回根。</summary>
    private static string SafeReturnUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return "/";
        return Uri.TryCreate(url, UriKind.Relative, out _) ? url! : "/";
    }
}
