using System;
using System.Collections.Generic;
using SuperBuilder_AI.Components.Services;
using SuperBuilder_AI.Web.Services;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>Phase 1（M8-05）：一次性登录交接码（handoff code）安全契约单测。</summary>
public sealed class PendingHandoffStoreTests
{
	private static SessionData Sample() => new(
		Token: "jwt", TenantId: 1, HomeTenantId: 1, UserId: 2, Username: "u",
		Permissions: new List<string>(), AvailableCultures: new List<string> { "zh-CN" }, DefaultCulture: "zh-CN");

	[Fact]
	public void Store_then_TryConsume_returns_data_and_returnUrl()
	{
		var store = new PendingHandoffStore();
		store.Store("code-1", Sample(), "/ask", TimeSpan.FromSeconds(60));

		var ok = store.TryConsume("code-1", out var data, out var returnUrl);

		Assert.True(ok);
		Assert.NotNull(data);
		Assert.Equal("jwt", data!.Token);
		Assert.Equal("/ask", returnUrl);
	}

	[Fact]
	public void Consumed_code_cannot_be_consumed_again()
	{
		var store = new PendingHandoffStore();
		store.Store("code-1", Sample(), null, TimeSpan.FromSeconds(60));
		store.TryConsume("code-1", out _, out _);

		var second = store.TryConsume("code-1", out _, out _);

		Assert.False(second); // 一次性：重放拒绝
	}

	[Fact]
	public void Unknown_code_rejected()
	{
		var store = new PendingHandoffStore();
		var ok = store.TryConsume("nope", out var data, out var returnUrl);

		Assert.False(ok);
		Assert.Null(data);
		Assert.Null(returnUrl);
	}

	[Fact]
	public void Expired_code_rejected()
	{
		var store = new PendingHandoffStore();
		store.Store("code-exp", Sample(), null, TimeSpan.FromSeconds(-1)); // 已过期

		var ok = store.TryConsume("code-exp", out _, out _);

		Assert.False(ok);
	}
}
