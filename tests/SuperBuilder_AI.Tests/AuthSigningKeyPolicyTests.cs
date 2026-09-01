using System;
using SuperBuilder_AI.Services.Auth;
using Xunit;

namespace SuperBuilder_AI.Tests;

public sealed class AuthSigningKeyPolicyTests
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void Validate_RequiresExplicitKeyInEveryEnvironment(string? key)
	{
		Assert.Throws<InvalidOperationException>(() => AuthSigningKeyPolicy.Validate(key, isDevelopment: true));
		Assert.Throws<InvalidOperationException>(() => AuthSigningKeyPolicy.Validate(key, isDevelopment: false));
	}

	[Fact]
	public void Validate_AllowsExplicitDevelopmentKey()
	{
		const string key = "local-development-key";

		Assert.Equal(key, AuthSigningKeyPolicy.Validate(key, isDevelopment: true));
	}

	[Theory]
	[InlineData("too-short")]
	[InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
	[InlineData(AuthSigningKeyPolicy.FormerDevelopmentFallback)]
	public void Validate_RejectsWeakOrFormerFallbackOutsideDevelopment(string key)
	{
		Assert.Throws<InvalidOperationException>(() => AuthSigningKeyPolicy.Validate(key, isDevelopment: false));
	}

	[Fact]
	public void Validate_AllowsStrongExplicitKeyOutsideDevelopment()
	{
		const string key = "0123456789abcdef0123456789abcdef";

		Assert.Equal(key, AuthSigningKeyPolicy.Validate(key, isDevelopment: false));
	}

	[Fact]
	public void TokenService_HasNoMissingKeyFallback()
	{
		Assert.Throws<ArgumentException>(() => new TokenService(null));
		Assert.Throws<ArgumentException>(() => new TokenService(" "));
	}
}
