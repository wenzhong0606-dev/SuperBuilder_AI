using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Components.Services;

namespace SuperBuilder_AI.Web.Services;

/// <summary>Phase 1 会话 HTTP 端点（仅这些端点写/清 httpOnly cookie，组件事件不直接写 cookie）。</summary>
public static class SessionEndpointRoutes
{
	/// <summary>
	/// 注册 /auth/session/start（交接码消费）与 /auth/session/end（登出）。
	/// 验收 #4：两者均为 POST 且经 antiforgery 校验；handoff code 由请求体承载（不再经 URL 查询参数），
	/// 避免 code 出现在浏览器历史 / 代理日志 / Referer 中；returnUrl 做开放重定向防护。
	/// </summary>
	public static void MapSessionEndpoints(this IEndpointRouteBuilder app)
	{
		// 一次性交接码 → 写入服务端会话 + httpOnly cookie，返回重定向目标（JSON）。
		// code 来自请求体（[FromForm]），returnUrl 由服务端 PendingHandoffStore 暂存，不暴露给客户端。
		// antiforgery 校验改为在 handler 内手动调用 IAntiforgery.ValidateRequestAsync，
		// 规避 minimal API 上 [ValidateAntiforgeryToken] 特性解析对 MVC 程序集的脆弱依赖，
		// 同时不依赖 .NET 版本特定的 .RequireAntiforgery() 扩展。
		app.MapPost("/auth/session/start",
			async (HttpContext ctx, [FromForm] string? code, IWebSessionStore store, PendingHandoffStore pending, SessionCookieService cookie, IAntiforgery antiforgery) =>
		{
			if (!await ValidateAf(ctx, antiforgery))
				return Results.StatusCode(StatusCodes.Status400BadRequest);

			if (string.IsNullOrEmpty(code) || !pending.TryConsume(code, out var data, out var storedReturnUrl))
				return Results.Ok(new { redirect = "/login" });

			// 防会话固定（hardening §8.2(d)）：登录前若已持有旧会话，先使其失效再签发全新会话 id。
			var prior = cookie.GetSessionId(ctx);
			if (prior is not null) store.Remove(prior);

			var target = SafeReturnUrl(storedReturnUrl);
			var sessionId = store.Create(data!);
			cookie.Set(ctx, sessionId);
			return Results.Ok(new { redirect = target });
		});

		// 登出：清服务端会话 + 清 cookie，返回重定向目标（JSON）。
		app.MapPost("/auth/session/end",
			async (HttpContext ctx, IWebSessionStore store, SessionCookieService cookie, IAntiforgery antiforgery) =>
		{
			if (!await ValidateAf(ctx, antiforgery))
				return Results.StatusCode(StatusCodes.Status400BadRequest);

			var id = cookie.GetSessionId(ctx);
			if (id is not null) store.Remove(id);
			cookie.Clear(ctx);
			return Results.Ok(new { redirect = "/login" });
		});
	}

	/// <summary>手动校验 antiforgery：校验失败（缺令牌/令牌不匹配）返回 false，由调用方回 400。</summary>
	private static async Task<bool> ValidateAf(HttpContext ctx, IAntiforgery antiforgery)
	{
		try
		{
			await antiforgery.ValidateRequestAsync(ctx);
			return true;
		}
		catch (AntiforgeryValidationException)
		{
			return false;
		}
	}

	/// <summary>开放重定向防护：仅允许站内相对路径，否则回根。</summary>
	private static string SafeReturnUrl(string? url)
	{
		if (string.IsNullOrEmpty(url)) return "/";
		return Uri.TryCreate(url, UriKind.Relative, out _) ? url! : "/";
	}
}
