using System;
using StackExchange.Redis;

namespace SuperBuilder_AI.Middleware;

/// <summary>
/// Redis 固定窗口计数所需的极简数据库抽象。
///
/// <para>
/// 仅抽取 <see cref="RedisRateLimitStore"/> 实际用到的两个原语，避免在单元测试中
/// 模拟 <c>StackExchange.Redis</c> 庞大的 <c>IDatabase</c> 接口；测试可注入内存替身。
/// </para>
/// </summary>
public interface IRedisCounterDatabase
{
	/// <summary>原子自增计数，返回自增后的当前值（键不存在时从 0 变 1）。</summary>
	long StringIncrement(string key);

	/// <summary>为键设置过期时间（首次创建窗口时调用，使过期窗口自动逐出）。</summary>
	bool KeyExpire(string key, TimeSpan expiry);
}

/// <summary>
/// 将 <see cref="IDatabase"/> 适配到 <see cref="IRedisCounterDatabase"/>。</summary>
internal sealed class StackExchangeRedisCounterDatabase : IRedisCounterDatabase
{
	private readonly IDatabase _db;

	public StackExchangeRedisCounterDatabase(IDatabase db) => _db = db;

	public long StringIncrement(string key) => _db.StringIncrement(key);

	public bool KeyExpire(string key, TimeSpan expiry) => _db.KeyExpire(key, expiry);
}

/// <summary>
/// RL-2 分布式限流后端：基于 Redis 的固定窗口计数。
///
/// <para>
/// 多实例（负载均衡）部署下，所有实例共享同一 Redis，计数全局一致，
/// 攻击者将无法再把请求分散到不同实例以绕过限流（这是 <see cref="MemoryRateLimitStore"/> 的单实例局限）。
/// </para>
/// <para>
/// 固定窗口语义与 <see cref="MemoryRateLimitStore"/> 对齐：窗口边界按下取整（便于下游稳定计算
/// <c>Retry-After</c>），窗口内的请求原子自增；窗口切换后键不同，自然落在新窗口（计数 1）。
/// </para>
/// <para>
/// 实例生命周期由 DI 持有的 <see cref="IConnectionMultiplexer"/> 负责，本类不释放它（<see cref="Dispose"/> 为空操作）。
/// </para>
/// </summary>
public sealed class RedisRateLimitStore : IRateLimitStore
{
	private readonly IRedisCounterDatabase _db;
	private readonly string _instanceName;

	/// <summary>通过 Redis 连接复用器构造（生产路径）。</summary>
	public RedisRateLimitStore(IConnectionMultiplexer multiplexer, string? instanceName = null)
		: this(new StackExchangeRedisCounterDatabase(multiplexer.GetDatabase()), instanceName)
	{
	}

	/// <summary>通过抽象数据库构造（测试可注入内存替身；亦可作为自定义 Redis 适配的扩展点）。</summary>
	public RedisRateLimitStore(IRedisCounterDatabase db, string? instanceName = null)
	{
		_db = db;
		_instanceName = instanceName ?? "rls:";
	}

	/// <summary>计算固定窗口桶键（按下取整到窗口边界），提取为公共静态方法以便单元测试与复用。</summary>
	public static string ComputeBucketKey(string key, TimeSpan window, DateTime now, string instanceName)
	{
		var bucket = now.Ticks - (now.Ticks % window.Ticks);
		return $"{instanceName}{key}:{bucket}";
	}

	public RateLimitCounter Increment(string key, TimeSpan window, DateTime now)
	{
		// 窗口边界按 now 的 Kind 对齐，避免与下游 IsExpired（基于同一 now 语义）产生时区错位。
		var bucketKey = ComputeBucketKey(key, window, now, _instanceName);
		var count = _db.StringIncrement(bucketKey);
		if (count == 1) _db.KeyExpire(bucketKey, window);

		var bucketStart = new DateTime(now.Ticks - (now.Ticks % window.Ticks), now.Kind);
		return new RateLimitCounter(bucketStart, (int)count, window);
	}

	public void Dispose()
	{
		// 复用 DI 持有的 IConnectionMultiplexer，不在此释放。
	}
}
