using System;
using SuperBuilder_AI.Api.Security;
using Xunit;

namespace SuperBuilder_AI.Tests;

public sealed class CorsOriginPolicyTests
{
	[Fact]
	public void Resolve_AllowsExplicitOpenCorsOnlyInDevelopment()
	{
		var policy = CorsOriginPolicy.Resolve(null, allowAnyOrigin: true, isDevelopment: true);

		Assert.True(policy.AllowAnyOrigin);
		Assert.Empty(policy.AllowedOrigins);
	}

	[Fact]
	public void Resolve_RejectsOpenCorsOutsideDevelopment()
	{
		Assert.Throws<InvalidOperationException>(() =>
			CorsOriginPolicy.Resolve(null, allowAnyOrigin: true, isDevelopment: false));
	}

	[Fact]
	public void Resolve_RequiresAnExplicitPolicyInEveryEnvironment()
	{
		Assert.Throws<InvalidOperationException>(() =>
			CorsOriginPolicy.Resolve(null, allowAnyOrigin: false, isDevelopment: true));
		Assert.Throws<InvalidOperationException>(() =>
			CorsOriginPolicy.Resolve(Array.Empty<string>(), allowAnyOrigin: false, isDevelopment: false));
	}

	[Fact]
	public void Resolve_NormalizesAndDeduplicatesFiniteOrigins()
	{
		var policy = CorsOriginPolicy.Resolve(
			new[] { "https://app.example.com/", " https://APP.example.com " },
			allowAnyOrigin: false,
			isDevelopment: false);

		Assert.False(policy.AllowAnyOrigin);
		Assert.Equal(new[] { "https://app.example.com" }, policy.AllowedOrigins);
	}

	[Theory]
	[InlineData("*")]
	[InlineData("https://*.example.com")]
	[InlineData("https://example.com/path")]
	[InlineData("https://example.com?source=test")]
	[InlineData("file:///tmp/app")]
	[InlineData("https://user:pass@example.com")]
	public void Resolve_RejectsUnsafeOrNonOriginValues(string origin)
	{
		Assert.Throws<InvalidOperationException>(() =>
			CorsOriginPolicy.Resolve(new[] { origin }, allowAnyOrigin: false, isDevelopment: false));
	}

	[Fact]
	public void Resolve_RejectsConflictingOpenAndFiniteConfiguration()
	{
		Assert.Throws<InvalidOperationException>(() =>
			CorsOriginPolicy.Resolve(new[] { "https://app.example.com" }, allowAnyOrigin: true, isDevelopment: true));
	}
}
