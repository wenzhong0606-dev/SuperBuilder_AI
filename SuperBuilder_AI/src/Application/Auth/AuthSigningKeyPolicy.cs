using System;
using System.Linq;
using System.Text;

namespace SuperBuilder_AI.Services.Auth;

/// <summary>
/// Validates the HMAC signing key before the application registers its token service.
/// Every environment requires an explicit key; non-development environments also
/// require enough entropy for HMAC-SHA256 and reject the former built-in fallback.
/// </summary>
public static class AuthSigningKeyPolicy
{
	public const int MinimumProductionKeyBytes = 32;
	public const int MinimumDistinctCharacters = 12;
	public const string FormerDevelopmentFallback = "dev-insecure-signing-key-P11-change-in-prod";

	public static string Validate(string? signingKey, bool isDevelopment)
	{
		if (string.IsNullOrWhiteSpace(signingKey))
			throw new InvalidOperationException("Auth:SigningKey must be explicitly configured.");

		var key = signingKey.Trim();
		if (!isDevelopment)
		{
			if (string.Equals(key, FormerDevelopmentFallback, StringComparison.Ordinal))
				throw new InvalidOperationException("Auth:SigningKey must not use the former development fallback outside Development.");

			if (Encoding.UTF8.GetByteCount(key) < MinimumProductionKeyBytes)
				throw new InvalidOperationException($"Auth:SigningKey must be at least {MinimumProductionKeyBytes} UTF-8 bytes outside Development.");

			if (key.Distinct().Count() < MinimumDistinctCharacters)
				throw new InvalidOperationException($"Auth:SigningKey must contain at least {MinimumDistinctCharacters} distinct characters outside Development.");
		}

		return key;
	}
}
