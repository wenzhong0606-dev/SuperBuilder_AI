using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace SuperBuilder_AI.Api.Security;

public static class GovernanceDataPlanePolicy
{
	public static bool IsGovernancePrincipal(ClaimsPrincipal principal) =>
		principal.Claims.Any(c => c.Type == "perm" && c.Value.StartsWith("platform:", StringComparison.Ordinal));

	public static bool IsForbiddenDataPlane(PathString path)
	{
		if (!path.StartsWithSegments("/api")) return false;
		return !path.StartsWithSegments("/api/tenant-management") &&
			!path.StartsWithSegments("/api/localization") &&
			!path.StartsWithSegments("/api/quota") &&
			!path.StartsWithSegments("/api/audit") &&
			!path.StartsWithSegments("/api/metadata-vector") &&
			!path.StartsWithSegments("/api/auth/me");
	}
}
