using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using SuperBuilder_AI.Components.Services;
using SuperBuilder_AI.Web.Services;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>Phase 1（M8-05）：WebSessionStore 服务端内存会话存储契约单测。</summary>
public sealed class WebSessionStoreTests
{
	private static WebSessionStore CreateStore(int ttlMinutes)
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Session:TtlMinutes"] = ttlMinutes.ToString()
			})
			.Build();
		return new WebSessionStore(config);
	}

	private static SessionData Sample() => new(
		Token: "jwt", TenantId: 1, HomeTenantId: 1, UserId: 2, Username: "u",
		Permissions: new List<string>(), AvailableCultures: new List<string> { "zh-CN" }, DefaultCulture: "zh-CN");

	[Fact]
	public void Create_then_Get_returns_same_data()
	{
		var store = CreateStore(480);
		var id = store.Create(Sample());

		var loaded = store.Get(id);

		Assert.NotNull(loaded);
		Assert.Equal("jwt", loaded!.Token);
		Assert.Equal(2, loaded.UserId);
	}

	[Fact]
	public void Create_returns_distinct_ids()
	{
		var store = CreateStore(480);
		var a = store.Create(Sample());
		var b = store.Create(Sample());

		Assert.NotEqual(a, b);
	}

	[Fact]
	public void Remove_then_Get_returns_null()
	{
		var store = CreateStore(480);
		var id = store.Create(Sample());

		store.Remove(id);
		var loaded = store.Get(id);

		Assert.Null(loaded);
	}

	[Fact]
	public void Set_overwrites_existing_session()
	{
		var store = CreateStore(480);
		var id = store.Create(Sample());

		store.Set(id, new SessionData(
			Token: "rotated", TenantId: 1, HomeTenantId: 1, UserId: 99, Username: "u2",
			Permissions: new List<string>(), AvailableCultures: new List<string> { "en-US" }, DefaultCulture: "en-US"));

		var loaded = store.Get(id);
		Assert.Equal("rotated", loaded!.Token);
		Assert.Equal(99, loaded.UserId);
	}

	[Fact]
	public void Get_unknown_id_returns_null()
	{
		var store = CreateStore(480);
		Assert.Null(store.Get("does-not-exist"));
	}

	[Fact]
	public void Expired_session_returns_null()
	{
		// TTL=0 → 创建即过期（懒清理）。
		var store = CreateStore(0);
		var id = store.Create(Sample());

		Assert.Null(store.Get(id));
	}
}
