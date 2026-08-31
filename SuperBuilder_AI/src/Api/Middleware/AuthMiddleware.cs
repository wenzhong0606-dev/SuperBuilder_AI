using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Services.Auth;

namespace SuperBuilder_AI.Middleware;

/// <summary>
/// 鉴权中间件（P11.0 安全轨道）。
///
/// <para>
/// 从 <c>Authorization: Bearer &lt;token&gt;</c> 或 <c>X-Api-Token</c> 头解析无状态令牌，
/// 校验通过后构造 <see cref="ClaimsPrincipal"/> 并写入 <c>HttpContext.User</c>，
/// 同时把租户 Id 存入 <c>HttpContext.Items["TenantId"]</c> 供下游控制器读取。
/// </para>
///
/// <para>
/// 匿名白名单（无需令牌即可访问）保证既有链路不受影响，尤其是 Golden 运行时
/// <c>/evaluation/**</c> 与静态资源；其余 <c>/api/*</c> 缺少有效令牌一律返回 401。
/// 鉴权失败不影响响应体，也不阻断健康探测。
/// </para>
/// </summary>
public sealed class AuthMiddleware
{
	private readonly RequestDelegate _next;
	private readonly ITokenService _tokenService;

	private const string BearerPrefix = "Bearer ";
	private const string ApiTokenHeader = "X-Api-Token";
	private const string TenantIdItemKey = "TenantId";

	public AuthMiddleware(RequestDelegate next, ITokenService tokenService)
	{
		_next = next;
		_tokenService = tokenService;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		if (IsAnonymousPath(context.Request.Path))
		{
			await _next(context);
			return;
		}

		var raw = ExtractToken(context);
		var principal = raw is null ? null : _tokenService.Validate(raw);

		if (principal is null)
		{
			if (context.Request.Path.StartsWithSegments("/api"))
			{
				context.Response.StatusCode = StatusCodes.Status401Unauthorized;
				await WriteJsonAsync(context, new ApiError { Code = ErrorCodes.Unauthorized, Message = "未授权：缺少或无效的访问令牌。" });
				return;
			}

			// 非 api 的受保护路径（如 /Home）无令牌时放行，交由后续处理（通常是 404）。
			await _next(context);
			return;
		}

		var claims = new List<Claim>
		{
			new(ClaimTypes.NameIdentifier, principal.UserId.ToString()),
			new(ClaimTypes.Name, principal.Username),
			new("tid", principal.TenantId.ToString()),
		};
		foreach (var perm in principal.Permissions)
			claims.Add(new Claim("perm", perm));

		var identity = new ClaimsIdentity(claims, "Bearer");
		context.User = new ClaimsPrincipal(identity);
		context.Items[TenantIdItemKey] = principal.TenantId;

		await _next(context);
	}

	private string? ExtractToken(HttpContext context)
	{
		if (context.Request.Headers.TryGetValue("Authorization", out var auth) &&
			auth.ToString().StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
		{
			return auth.ToString().Substring(BearerPrefix.Length).Trim();
		}

		if (context.Request.Headers.TryGetValue(ApiTokenHeader, out var token) &&
			!string.IsNullOrWhiteSpace(token.ToString()))
		{
			return token.ToString().Trim();
		}

		return null;
	}

	/// <summary>匿名白名单：Golden 运行时、健康探测、登录端点、首页与静态资源。</summary>
	private static bool IsAnonymousPath(PathString path)
	{
		if (path == "/") return true;
		if (path.StartsWithSegments("/evaluation")) return true;
		if (path.StartsWithSegments("/health")) return true;
		if (path.StartsWithSegments("/api/auth/login")) return true;
		if (path.StartsWithSegments("/css")) return true;
		if (path.StartsWithSegments("/js")) return true;
		if (path.StartsWithSegments("/lib")) return true;

		var v = path.Value ?? string.Empty;
		var dot = v.LastIndexOf('.');
		if (dot > 0)
		{
			var ext = v.Substring(dot).ToLowerInvariant();
			return ext is ".css" or ".js" or ".png" or ".jpg" or ".jpeg" or ".gif"
				or ".ico" or ".svg" or ".woff" or ".woff2" or ".ttf" or ".html"
				or ".json" or ".map" or ".webmanifest";
		}

		return false;
	}

	private static async Task WriteJsonAsync(HttpContext context, object payload)
	{
		try
		{
			context.Response.ContentType = "application/json; charset=utf-8";
			await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(payload));
		}
		catch
		{
			// 响应已启动则跳过
		}
	}
}
