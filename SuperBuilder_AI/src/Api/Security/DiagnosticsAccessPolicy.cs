using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SuperBuilder_AI.Models.Identity;

namespace SuperBuilder_AI.Api.Security;

/// <summary>
/// Central access policy for operational and destructive diagnostics endpoints.
/// The permission comes only from the validated token claims; request headers are
/// never treated as evidence that a caller is on an internal network.
/// </summary>
public static class DiagnosticsAccessPolicy
{
	/// <summary>
	/// 路径前缀匹配（AUTH-1 修复）：统一为大小写不敏感。
	///
	/// <para>
	/// 诊断端点若沿用默认的 Ordinal 区分大小写，<c>/METRICS</c>、<c>/Api/Metadata-Vector</c> 等变体
	/// 将取不到所需权限而绕过守卫，却仍被大小写不敏感的路由命中 → 未授权访问诊断乃至破坏性端点。
	/// </para>
	/// </summary>
	private static bool IsPrefix(PathString path, string prefix) =>
		path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase);

	public static string? GetRequiredPermission(PathString path)
	{
		if (IsPrefix(path, "/metrics"))
			return IdentityPermissions.PlatformDiagnosticsView;

		if (IsPrefix(path, "/test"))
			return IdentityPermissions.PlatformDiagnosticsManage;

		if (IsPrefix(path, "/api/metadata-vector/status"))
			return IdentityPermissions.PlatformDiagnosticsView;

		if (IsPrefix(path, "/api/metadata-vector"))
			return IdentityPermissions.PlatformDiagnosticsManage;

		if (IsPrefix(path, "/metadata") || IsPrefix(path, "/qdrant"))
			return IdentityPermissions.PlatformDiagnosticsManage;

		return null;
	}

	public static bool HasPermission(ClaimsPrincipal principal, string requiredPermission)
	{
		if (principal.HasClaim("perm", requiredPermission))
			return true;

		// Manage is a strict superset of read-only diagnostics access.
		return requiredPermission == IdentityPermissions.PlatformDiagnosticsView &&
			principal.HasClaim("perm", IdentityPermissions.PlatformDiagnosticsManage);
	}
}
