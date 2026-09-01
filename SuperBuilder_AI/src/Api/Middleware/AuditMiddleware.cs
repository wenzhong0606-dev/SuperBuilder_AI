using System;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Api.Security;
using SuperBuilder_AI.Interfaces.Audit;

namespace SuperBuilder_AI.Middleware;

/// <summary>
/// 自动请求级审计中间件（P10.3 可选接入）。
/// 以非阻塞、异常静默方式记录每个 HTTP 请求的「方法 / 路径 / 状态码 / 耗时 / 租户上下文」，
/// 审计写入失败不影响主链路（不影响 Golden 行为契约）。
/// <para>
/// P0-02B：租户事实源改为 <see cref="TenantDataPlanePolicy.ReadObserved"/>——
/// 认证/生效租户一律取自令牌（或执行层写盘的解析结果），query <c>tenantId</c> 与
/// header <c>X-Tenant-Id</c> 仅作为「请求值(RequestedTenantId)」记录，绝不作为生效租户，
/// 杜绝伪造该值污染审计中的真实执行租户。数据面无合法切换，故
/// <c>TenantSwitchAuthorized</c> 恒为 false；治理角色管理具体租户 B 时另记
/// <c>ManagementTargetTenantId</c> / <c>ManagementAction</c> / <c>ManagementActionAuthorized</c>。
/// </para>
/// </summary>
public sealed class AuditMiddleware
{
    private readonly RequestDelegate _next;

    public AuditMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IAuditLogService audit)
    {
        var started = DateTime.UtcNow;
		Exception? failure = null;
        try
        {
            await _next(context);
        }
		catch (Exception ex)
		{
			failure = ex;
			throw;
		}
        finally
        {
            try
            {
                var elapsedMs = (long)(DateTime.UtcNow - started).TotalMilliseconds;
                // P0-02B：唯一事实源——认证/生效租户取自令牌或执行层写盘结果，绝不用 query/header 值
                var tenantCtx = TenantDataPlanePolicy.ReadObserved(context);
                var statusCode = failure is SuperBuilderException sb ? sb.StatusCode :
				failure is not null ? StatusCodes.Status500InternalServerError : context.Response.StatusCode;
                var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
				long? parsedUserId = long.TryParse(userId, out var uid) ? uid :
					SecurityAuditContext.Long(context, SecurityAuditContext.UserIdKey);
				var authenticatedTenantId = tenantCtx.AuthenticatedTenantId > 0
					? tenantCtx.AuthenticatedTenantId
					: SecurityAuditContext.Long(context, SecurityAuditContext.TenantIdKey) ?? 0;
				var reasonCode = failure is SuperBuilderException securityFailure
					? securityFailure.ErrorCode
					: SecurityAuditContext.String(context, SecurityAuditContext.ReasonCodeKey);
				var reason = failure is SuperBuilderException
					? ErrorCodes.Message(reasonCode!)
					: SecurityAuditContext.String(context, SecurityAuditContext.ReasonKey);
				var traceId = context.Items.TryGetValue("CorrelationId", out var correlation)
					? correlation?.ToString()
					: context.TraceIdentifier;
				var action = ClassifyAction(context, statusCode, reasonCode, tenantCtx.ManagementAction);

                await audit.LogAsync(new AuditLogEntry(
                    // 审计行的 TenantId = 真实操作者租户（认证租户），而非可能被伪造的请求值
                    TenantId: authenticatedTenantId,
                    Action: action,
                    EntityType: action == "http.request" ? "Http" : "SecurityEvent",
                    UserId: parsedUserId,
					Actor: context.User?.Identity?.Name ?? "anonymous",
                    EntityId: $"{context.Request.Method} {context.Request.Path}",
                    BeforeJson: null,
                    AfterJson: JsonSerializer.Serialize(new
                    {
                        statusCode,
                        durationMs = elapsedMs,
                        method = context.Request.Method,
                        path = context.Request.Path.Value,
						traceId,
						reasonCode,
						reason,
                        // P0-02B 双上下文：认证 / 请求（可能伪造）/ 生效 / 切换授权
                        authenticatedTenantId = tenantCtx.AuthenticatedTenantId,
                        requestedTenantId = tenantCtx.RequestedTenantId,
                        effectiveTenantId = tenantCtx.EffectiveTenantId,
                        tenantSwitchAuthorized = tenantCtx.TenantSwitchAuthorized,
                        // 管理面：治理角色操作具体租户 B 的目标与动作（不记为 TenantSwitch）
                        managementTargetTenantId = tenantCtx.ManagementTargetTenantId,
                        managementAction = tenantCtx.ManagementAction,
                        managementActionAuthorized = tenantCtx.ManagementActionAuthorized,
                    }),
                    Result: statusCode < 400 ? "success" : "failure",
					Message: reasonCode,
                    Timestamp: started));
            }
            catch
            {
                // 审计失败不影响主链路
            }
        }
    }

	private static string ClassifyAction(HttpContext context, int statusCode, string? reasonCode, string? managementAction)
	{
		if (!string.IsNullOrWhiteSpace(managementAction)) return $"governance.{managementAction}";
		if (context.User?.Claims.Any(x => x.Type == "perm" && x.Value.StartsWith("platform:", StringComparison.Ordinal)) == true)
			return "governance.http.request";
		if (reasonCode == ErrorCodes.QueryPlanSecurityRejected) return "query-plan.rejected";
		if (reasonCode == ErrorCodes.TenantIsolated) return "tenant.access.denied";
		if (context.Request.Path.StartsWithSegments("/api/auth/login") && statusCode >= 400) return "login.failed";
		if (statusCode is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden) return "authorization.denied";
		return "http.request";
	}
}
