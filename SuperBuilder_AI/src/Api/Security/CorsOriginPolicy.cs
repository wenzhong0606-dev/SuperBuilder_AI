using System;
using System.Collections.Generic;
using System.Linq;

namespace SuperBuilder_AI.Api.Security;

/// <summary>
/// Resolves the configured browser origins before ASP.NET Core builds its CORS policy.
/// Open CORS is an explicit Development-only choice; every other environment must
/// provide a finite list of HTTP(S) origins.
/// </summary>
public sealed record CorsOriginPolicy(bool AllowAnyOrigin, IReadOnlyList<string> AllowedOrigins)
{
	public static CorsOriginPolicy Resolve(
		IEnumerable<string>? configuredOrigins,
		bool allowAnyOrigin,
		bool isDevelopment)
	{
		var origins = (configuredOrigins ?? Array.Empty<string>())
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Select(NormalizeOrigin)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();

		if (allowAnyOrigin && origins.Length > 0)
			throw new InvalidOperationException("Cors:AllowAnyOrigin and Cors:AllowedOrigins cannot be configured together.");

		if (allowAnyOrigin)
		{
			if (!isDevelopment)
				throw new InvalidOperationException("Cors:AllowAnyOrigin is permitted only in Development.");

			return new CorsOriginPolicy(true, Array.Empty<string>());
		}

		if (origins.Length == 0)
			throw new InvalidOperationException("Configure Cors:AllowedOrigins, or explicitly enable Cors:AllowAnyOrigin in Development.");

		return new CorsOriginPolicy(false, origins);
	}

	private static string NormalizeOrigin(string value)
	{
		var origin = value.Trim().TrimEnd('/');
		if (origin.Contains('*', StringComparison.Ordinal))
			throw new InvalidOperationException("Cors:AllowedOrigins does not accept wildcard origins.");

		if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
			(uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
			!string.IsNullOrEmpty(uri.UserInfo) ||
			uri.AbsolutePath != "/" ||
			!string.IsNullOrEmpty(uri.Query) ||
			!string.IsNullOrEmpty(uri.Fragment))
		{
			throw new InvalidOperationException($"Cors origin must be an HTTP(S) origin without path, query, fragment, wildcard, or credentials: {value}");
		}

		return uri.GetLeftPart(UriPartial.Authority);
	}
}
