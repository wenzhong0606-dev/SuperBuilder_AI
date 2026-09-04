using System;
using System.Collections.Generic;
using SuperBuilder_AI.Middleware;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// RL-2 分布式限流后端 <see cref="RedisRateLimitStore"/> 测试（无需真实 Redis）。
///
/// <para>
/// 通过内存替身 <see cref="FakeRedisCounterDatabase"/> 模拟 Redis 的原子自增与过期语义，
/// 锁定固定窗口的桶键计算与计数行为，确保该实现与 <see cref="MemoryRateLimitStore"/> 语义对齐，
/// 可安全作为多实例部署下的限流后端替换。
/// </para>
/// </summary>
public sealed class RedisRateLimitStoreTests
{
	/// <summary>内存替身：模拟 Redis 的 StringIncrement（原子自增）+ KeyExpire（仅记录，不影响计数重置逻辑）。</summary>
	private sealed class FakeRedisCounterDatabase : IRedisCounterDatabase
	{
		private readonly Dictionary<string, long> _store = new();

		public long StringIncrement(string key)
		{
			_store.TryGetValue(key, out var v);
			v += 1;
			_store[key] = v;
			return v;
		}

		public bool KeyExpire(string key, TimeSpan expiry) => true;
	}

	private static DateTime Utc(int h, int m, int s) =>
		new(2026, 1, 1, h, m, s, DateTimeKind.Utc);

	[Fact]
	public void ComputeBucketKey_SameWindowBoundary_SameKey()
	{
		var w = TimeSpan.FromMinutes(1);
		var k1 = RedisRateLimitStore.ComputeBucketKey("tenant:9", w, Utc(0, 0, 0), "rls:");
		var k2 = RedisRateLimitStore.ComputeBucketKey("tenant:9", w, Utc(0, 0, 30), "rls:");
		var k3 = RedisRateLimitStore.ComputeBucketKey("tenant:9", w, Utc(0, 0, 59), "rls:");

		Assert.Equal(k1, k2);
		Assert.Equal(k1, k3);
	}

	[Fact]
	public void ComputeBucketKey_CrossWindowBoundary_DifferentKey()
	{
		var w = TimeSpan.FromMinutes(1);
		var k1 = RedisRateLimitStore.ComputeBucketKey("tenant:9", w, Utc(0, 0, 59), "rls:");
		var k2 = RedisRateLimitStore.ComputeBucketKey("tenant:9", w, Utc(0, 1, 0), "rls:");

		Assert.NotEqual(k1, k2);
	}

	[Fact]
	public void ComputeBucketKey_InstanceName_Prefixed()
	{
		var w = TimeSpan.FromMinutes(1);
		var k = RedisRateLimitStore.ComputeBucketKey("k", w, Utc(0, 0, 0), "prod:");
		Assert.StartsWith("prod:", k);
	}

	[Fact]
	public void Increment_WithinSameWindow_AccumulatesCount()
	{
		var store = new RedisRateLimitStore(new FakeRedisCounterDatabase(), "rls:");
		var w = TimeSpan.FromMinutes(1);

		var c1 = store.Increment("k", w, Utc(0, 0, 0));
		var c2 = store.Increment("k", w, Utc(0, 0, 10));
		var c3 = store.Increment("k", w, Utc(0, 0, 59));

		Assert.Equal(1, c1.Count);
		Assert.Equal(2, c2.Count);
		Assert.Equal(3, c3.Count);
		// 窗口未切换，Start 应落在同一窗口边界（0:00）。
		Assert.Equal(Utc(0, 0, 0), c3.Start);
	}

	[Fact]
	public void Increment_CrossWindow_ResetsToNewWindow()
	{
		var store = new RedisRateLimitStore(new FakeRedisCounterDatabase(), "rls:");
		var w = TimeSpan.FromMinutes(1);

		// 第一窗口累计到 3。
		store.Increment("k", w, Utc(0, 0, 0));
		store.Increment("k", w, Utc(0, 0, 10));
		var cLast = store.Increment("k", w, Utc(0, 0, 59));
		Assert.Equal(3, cLast.Count);

		// 进入下一窗口：桶键不同，内存替身无该键 → 自增返回 1。
		var cNext = store.Increment("k", w, Utc(0, 1, 0));
		Assert.Equal(1, cNext.Count);
		// Start 对齐到新窗口边界（0:01）。
		Assert.Equal(Utc(0, 1, 0), cNext.Start);
	}

	[Fact]
	public void Increment_DifferentKeys_AreIndependent()
	{
		var store = new RedisRateLimitStore(new FakeRedisCounterDatabase(), "rls:");
		var w = TimeSpan.FromMinutes(1);

		var a = store.Increment("a", w, Utc(0, 0, 0));
		var b = store.Increment("b", w, Utc(0, 0, 0));

		Assert.Equal(1, a.Count);
		Assert.Equal(1, b.Count);
	}
}
