using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Api.Security;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Identity;
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
	private const string UserIdItemKey = "UserId";

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

		var diagnosticsPermission = DiagnosticsAccessPolicy.GetRequiredPermission(context.Request.Path);
		if (principal is null)
		{
			if (context.Request.Path.StartsWithSegments("/api") || diagnosticsPermission is not null)
			{
				SecurityAuditContext.Reject(context, ErrorCodes.Unauthorized, "missing-or-invalid-token");
				context.Response.StatusCode = StatusCodes.Status401Unauthorized;
				await WriteJsonAsync(context, new ApiError { Code = ErrorCodes.Unauthorized, Message = "未授权：缺少或无效的访问令牌。" });
				return;
			}

			// 非 api 的受保护路径（如 /Home）无令牌时放行，交由后续处理（通常是 404）。
			await _next(context);
			return;
		}

		// P0-04B：令牌吊销校验。令牌携带安全戳时，与当前用户库中的安全戳比对；
		// 不一致（口令/角色变更已轮换，或用户已删除）即视为已吊销，返回 401。
		// 遗留令牌（未携带安全戳）不做此校验，避免阻断既有会话（迁移窗口内自然随 60min 过期）。
		if (principal.SecurityStamp is not null)
		{
			var db = context.RequestServices.GetService<SuperBIContext>();
			if (db is not null)
			{
				var currentStamp = await db.Users.AsNoTracking()
					.Where(u => u.Id == principal.UserId && u.TenantId == principal.TenantId)
					.Select(u => u.SecurityStamp)
					.FirstOrDefaultAsync();
				if (currentStamp != principal.SecurityStamp)
				{
					SecurityAuditContext.Reject(context, ErrorCodes.Unauthorized, "token-revoked", principal.TenantId, principal.UserId);
					context.Response.StatusCode = StatusCodes.Status401Unauthorized;
					await WriteJsonAsync(context, new ApiError
					{
						Code = ErrorCodes.Unauthorized,
						Message = "未授权：令牌已失效，请重新登录。"
					});
					return;
				}
			}
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
		context.Items[UserIdItemKey] = principal.UserId;
		var executionIdentity = context.RequestServices?.GetService<IDataSourceExecutionIdentityAccessor>();
		if (executionIdentity is not null)
			executionIdentity.Current = new DataSourceExecutionIdentity(principal.TenantId, principal.UserId);

		if (TenantDataPlanePolicy.IsDataPlanePath(context.Request.Path))
		{
			var queryTenant = context.Request.Query.ContainsKey("tenantId")
				? TenantDataPlanePolicy.ParseRequestedTenant(context.Request.Query["tenantId"].ToString())
				: null;
			var headerTenant = context.Request.Headers.ContainsKey("X-Tenant-Id")
				? TenantDataPlanePolicy.ParseRequestedTenant(context.Request.Headers["X-Tenant-Id"].ToString())
				: null;
			var tenantResolution = TenantDataPlanePolicy.Resolve(context.User, queryTenant, headerTenant);
			TenantDataPlanePolicy.Store(context, tenantResolution);
			if (!tenantResolution.Authorized)
			{
				SecurityAuditContext.Reject(context, ErrorCodes.TenantIsolated, "cross-tenant-data-plane", principal.TenantId, principal.UserId);
				context.Response.StatusCode = StatusCodes.Status403Forbidden;
				await WriteJsonAsync(context, new ApiError
				{
					Code = ErrorCodes.TenantIsolated,
					Message = "禁止：数据面请求租户必须与认证租户一致。"
				});
				return;
			}
		}

		if (diagnosticsPermission is not null &&
			!DiagnosticsAccessPolicy.HasPermission(context.User, diagnosticsPermission))
		{
			SecurityAuditContext.Reject(context, ErrorCodes.Forbidden, $"missing-permission:{diagnosticsPermission}", principal.TenantId, principal.UserId);
			context.Response.StatusCode = StatusCodes.Status403Forbidden;
			await WriteJsonAsync(context, new ApiError
			{
				Code = ErrorCodes.Forbidden,
				Message = $"禁止：缺少 {diagnosticsPermission} 权限。"
			});
			return;
		}

		if (GovernanceDataPlanePolicy.IsGovernancePrincipal(context.User) &&
			GovernanceDataPlanePolicy.IsForbiddenDataPlane(context.Request.Path))
		{
			SecurityAuditContext.Reject(context, ErrorCodes.Forbidden, "governance-data-plane-denied", principal.TenantId, principal.UserId);
			context.Response.StatusCode = StatusCodes.Status403Forbidden;
			await WriteJsonAsync(context, new ApiError
			{
				Code = ErrorCodes.Forbidden,
				Message = "禁止：平台治理身份不得访问租户数据面。"
			});
			return;
		}

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
		if (path.StartsWithSegments("/api/auth/login-options")) return true;
		if (path.StartsWithSegments("/api/platform-bootstrap")) return true;
		// 登录前只开放只读的公共语言入口；管理接口仍需解析认证身份。
		if (path.StartsWithSegments("/api/localization/public")) return true;
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
