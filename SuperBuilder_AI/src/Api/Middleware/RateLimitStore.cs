using System;
using System.Collections.Concurrent;
using System.Threading;

namespace SuperBuilder_AI.Middleware;

/// <summary>
/// 限流窗口计数（不可变）。
///
/// <para>
/// 采用不可变结构而非「原地递增」的可变对象：RL-2 修复要点之一是
/// <c>ConcurrentDictionary.AddOrUpdate</c> 的更新委托在并发下可能被重复调用，
/// 若委托内对共享对象做 <c>Count++</c> 这类副作用操作，计数将失真。
/// 不可变结构保证委托无副作用，计数由字典的原子操作保证正确性。
/// </para>
/// </summary>
public readonly record struct RateLimitCounter(DateTime Start, int Count, TimeSpan Window)
{
	/// <summary>窗口是否已过期（过期意味着可重置为新窗口）。</summary>
	public bool IsExpired(DateTime now) => now - Start >= Window;
}

/// <summary>
/// 限流存储抽象（RL-2 可扩展性）。
///
/// <para>
/// 默认实现 <see cref="MemoryRateLimitStore"/> 为进程内存，<b>仅单实例有效</b>：
/// 多实例（负载均衡）部署下各实例独立计数，攻击者将请求分散到不同实例即可绕过限流。
/// 因此本抽象预留替换点——多实例上线前须提供分布式实现（Redis 等）并替换注册。
/// </para>
/// </summary>
public interface IRateLimitStore : IDisposable
{
	/// <summary>
	/// 对指定键原子累加计数并返回累加后的结果；若上次计数已超出窗口则重置为新窗口（计数 1）。
	/// </summary>
	RateLimitCounter Increment(string key, TimeSpan window, DateTime now);
}

/// <summary>
/// 进程内存限流存储（RL-1 修复）。
///
/// <para>
/// 原实现使用 <c>static ConcurrentDictionary</c> 且<b>永不清理</b>：
/// 每个唯一的「IP+设备」或「租户+用户」键永久驻留，长期运行下随匿名客户端数量单调增长 → 内存泄漏。
/// 本实现引入后台定期清理，逐出已过期窗口的条目，使内存占用与「活跃键数」成正比而非与「历史键数」成正比。
/// </para>
/// </summary>
public sealed class MemoryRateLimitStore : IRateLimitStore
{
	private readonly ConcurrentDictionary<string, RateLimitCounter> _counters =
		new(StringComparer.Ordinal);

	private readonly Timer _cleanupTimer;
	private bool _disposed;

	/// <param name="cleanupInterval">过期条目清理间隔（默认 1 分钟）。</param>
	public MemoryRateLimitStore(TimeSpan? cleanupInterval = null)
	{
		var interval = cleanupInterval ?? TimeSpan.FromMinutes(1);
		// 立即执行首次清理，随后按间隔轮询。
		_cleanupTimer = new Timer(_ => Cleanup(), null, interval, interval);
	}

	/// <summary>
	/// 当前驻留的键数量（诊断与测试用）：用于验证过期条目确已被逐出（RL-1），
	/// 若该值随「历史键数」而非「活跃键数」单调增长，即说明仍在泄漏。
	/// </summary>
	public int Count => _counters.Count;

	public RateLimitCounter Increment(string key, TimeSpan window, DateTime now)
	{
		// 委托必须无副作用：返回新实例，绝不修改 existing。
		return _counters.AddOrUpdate(key,
			_ => new RateLimitCounter(now, 1, window),
			(_, existing) => existing.IsExpired(now)
				? new RateLimitCounter(now, 1, window)
				: existing with { Count = existing.Count + 1 });
	}

	/// <summary>逐出所有已过期窗口的条目（键各自持有自己的窗口时长，故逐个判断）。</summary>
	private void Cleanup()
	{
		if (_disposed) return;

		var now = DateTime.UtcNow;
		foreach (var pair in _counters)
		{
			if (pair.Value.IsExpired(now))
				_counters.TryRemove(pair.Key, out _);
		}
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		_cleanupTimer.Dispose();
		_counters.Clear();
	}
}
