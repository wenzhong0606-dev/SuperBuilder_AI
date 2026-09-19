using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Moq;
using StackExchange.Redis;
using SuperBuilder_AI.Components.Services;
using SuperBuilder_AI.Web.Services;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>Phase 3（M8-05）：RedisWebSessionStore 后端契约单测。
/// 用 Moq 模拟 IConnectionMultiplexer / IDatabase（本类直接调用 *Async 接口方法并以 GetAwaiter().GetResult() 阻塞，
/// 故此处验证 StringSetAsync / StringGetAsync / KeyDeleteAsync / SetAddAsync / SetMembersAsync / SetRemoveAsync / KeyExpireAsync）。
/// 键空间前缀与序列化内容通过 db.Invocations 捕获后断言，避免对值类型 RedisKey/RedisValue 使用回调匹配器的不稳定。</summary>
public sealed class RedisWebSessionStoreTests
{
	private const string PrefixData = "sb:sess:data:";
	private const string PrefixUser = "sb:sess:user:";

	private static SessionData Sample(long userId = 2) => new(
		Token: "jwt", TenantId: 1, HomeTenantId: 1, UserId: userId, Username: "u",
		Permissions: new List<string>(), AvailableCultures: new List<string> { "zh-CN" }, DefaultCulture: "zh-CN");

	private static IConfiguration Config(int ttl = 480) =>
		new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?> { ["Session:TtlMinutes"] = ttl.ToString() })
			.Build();

	private static (RedisWebSessionStore store, Mock<IDatabase> db) Create()
	{
		var mux = new Mock<IConnectionMultiplexer>();
		var db = new Mock<IDatabase>();
		mux.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);
		var store = new RedisWebSessionStore(mux.Object, Config());
		return (store, db);
	}

	private static string KeyOf(Mock<IDatabase> db, string method, int occurrence = 0)
	{
		var call = db.Invocations.Where(i => i.Method.Name == method).Skip(occurrence).First();
		return ((RedisKey)call.Arguments[0]!).ToString();
	}

	[Fact]
	public void Create_writes_data_key_and_registers_user_index()
	{
		var (store, db) = Create();
		db.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);
		db.Setup(d => d.SetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);
		db.Setup(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);

		var id = store.Create(Sample());

		Assert.False(string.IsNullOrEmpty(id));
		var dataKey = KeyOf(db, "StringSetAsync");
		var userKey = KeyOf(db, "SetAddAsync");
		Assert.StartsWith(PrefixData, dataKey);
		Assert.StartsWith(PrefixUser, userKey);
		Assert.Equal(id, dataKey.Substring(PrefixData.Length));
	}

	[Fact]
	public void Create_serializes_session_data_to_json()
	{
		var (store, db) = Create();
		db.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);
		db.Setup(d => d.SetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);
		db.Setup(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);

		store.Create(Sample(userId: 7));

		var call = db.Invocations.First(i => i.Method.Name == "StringSetAsync");
		var value = ((RedisValue)call.Arguments[1]!).ToString();
		var round = JsonSerializer.Deserialize<SessionData>(value);
		Assert.NotNull(round);
		Assert.Equal("jwt", round!.Token);
		Assert.Equal(7, round.UserId);
	}

	[Fact]
	public void Get_missing_returns_null()
	{
		var (store, db) = Create();
		db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(RedisValue.Null);

		Assert.Null(store.Get("nope"));
	}

	[Fact]
	public void Get_deserializes_stored_json()
	{
		var (store, db) = Create();
		var json = (RedisValue)JsonSerializer.Serialize(Sample(userId: 9));
		db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(json);

		var loaded = store.Get("abc");

		Assert.NotNull(loaded);
		Assert.Equal(9, loaded!.UserId);
		Assert.Equal("jwt", loaded.Token);
	}

	[Fact]
	public void Get_corrupt_json_deletes_key_and_returns_null()
	{
		var (store, db) = Create();
		db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync((RedisValue)"not-json");
		db.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);

		Assert.Null(store.Get("bad"));
		Assert.StartsWith(PrefixData, KeyOf(db, "KeyDeleteAsync"));
	}

	[Fact]
	public void Remove_deletes_data_and_user_index_entry()
	{
		var (store, db) = Create();
		var json = (RedisValue)JsonSerializer.Serialize(Sample(userId: 4));
		db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(json);
		db.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);
		db.Setup(d => d.SetRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);

		store.Remove("sid-1");

		Assert.Equal(PrefixData + "sid-1", KeyOf(db, "KeyDeleteAsync"));
		var removeCall = db.Invocations.First(i => i.Method.Name == "SetRemoveAsync");
		Assert.Equal(PrefixUser + "4", ((RedisKey)removeCall.Arguments[0]!).ToString());
		Assert.Equal("sid-1", ((RedisValue)removeCall.Arguments[1]!).ToString());
	}

	[Fact]
	public void RemoveByUserId_deletes_all_sessions_and_index()
	{
		var (store, db) = Create();
		db.Setup(d => d.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
			.ReturnsAsync(new RedisValue[] { "sid-a", "sid-b" });
		db.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);

		var count = store.RemoveByUserId(4);

		Assert.Equal(2, count);
		// 2 个 data key + 1 个 user key = 3 次删除。
		Assert.Equal(3, db.Invocations.Count(i => i.Method.Name == "KeyDeleteAsync"));
	}
}
