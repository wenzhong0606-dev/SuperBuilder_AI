using Microsoft.AspNetCore.Http;

namespace SuperBuilder_AI.Api.Security;

/// <summary>
/// 在请求内部传递安全审计事实。只允许标量标识，不接收请求体、口令、Token 或连接串。
/// </summary>
public static class SecurityAuditContext
{
	private const string Prefix = "SecurityAudit.";
	public const string ReasonCodeKey = Prefix + "ReasonCode";
	public const string ReasonKey = Prefix + "Reason";
	public const string TenantIdKey = Prefix + "TenantId";
	public const string UserIdKey = Prefix + "UserId";

	public static void Reject(HttpContext? context, string reasonCode, string reason, long? tenantId = null, long? userId = null)
	{
		if (context is null) return;
		context.Items[ReasonCodeKey] = reasonCode;
		context.Items[ReasonKey] = reason;
		if (tenantId.HasValue) context.Items[TenantIdKey] = tenantId.Value;
		if (userId.HasValue) context.Items[UserIdKey] = userId.Value;
	}

	public static string? String(HttpContext context, string key) =>
		context.Items.TryGetValue(key, out var value) ? value?.ToString() : null;

	public static long? Long(HttpContext context, string key) =>
		context.Items.TryGetValue(key, out var value) && long.TryParse(value?.ToString(), out var parsed) ? parsed : null;
}
