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
	public static string? GetRequiredPermission(PathString path)
	{
		if (path.StartsWithSegments("/metrics"))
			return IdentityPermissions.PlatformDiagnosticsView;

		if (path.StartsWithSegments("/test"))
			return IdentityPermissions.PlatformDiagnosticsManage;

		if (path.StartsWithSegments("/api/metadata-vector/status"))
			return IdentityPermissions.PlatformDiagnosticsView;

		if (path.StartsWithSegments("/api/metadata-vector"))
			return IdentityPermissions.PlatformDiagnosticsManage;

		if (path.StartsWithSegments("/metadata") || path.StartsWithSegments("/qdrant"))
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
