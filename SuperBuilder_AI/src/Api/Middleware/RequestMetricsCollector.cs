using System;
using System.Collections.Generic;
using System.Linq;
using SuperBuilder_AI.Interfaces.BI;

namespace SuperBuilder_AI.Middleware;

/// <summary>
/// 请求指标采集器（P11.5.2 安全/运维轨）。
///
/// <para>
/// 按「路由」聚合：<b>请求数</b>、<b>错误数</b>（HTTP &gt;= 500）、<b>P95 延迟</b>、<b>平均延迟</b>、<b>最大延迟</b>。
/// 供 <c>GET /metrics</c> 输出，满足「可观测成熟化」中 metrics（P95 / 计数 / 错误率）一项。
/// </para>
///
/// <para>
/// 设计约束（面向生产安全）：
/// <list type="bullet">
/// <item><b>有界内存</b>：每条路由只保留最近 <see cref="SampleCapacity"/> 个耗时样本（环形覆盖），
/// 路由条目数超过 <see cref="RouteCapacity"/> 后统一归入 <c>__other__</c>，杜绝基数爆炸。</item>
/// <item><b>异常静默</b>：采集失败绝不抛异常、绝不干扰请求链路。</item>
/// <item><b>读多写少</b>：写入走短临界区（lock），读取时复制快照再排序，避免持锁计算。</item>
/// </list>
/// </para>
/// </summary>
public sealed class RequestMetricsCollector : IPipelineMetricsSink
{
	/// <summary>每条路由保留的耗时样本上限。</summary>
	public const int SampleCapacity = 512;

	/// <summary>独立路由条目上限，超出后归入 <c>__other__</c>。</summary>
	public const int RouteCapacity = 200;

	/// <summary>超出路由容量后的聚合桶名。</summary>
	public const string OtherRoute = "__other__";

	private readonly object _gate = new();
	private readonly Dictionary<string, RouteMetrics> _routes =
		new(StringComparer.OrdinalIgnoreCase);

	/// <summary>BI 管线分段延迟样本（键固定，无基数爆炸风险）。</summary>
	private readonly Dictionary<string, RouteMetrics> _stages =
		new(StringComparer.OrdinalIgnoreCase);

	/// <summary>管线结果分类计数（Reject / EarlyReturn / Repair）。</summary>
	private readonly Dictionary<string, long> _outcomes =
		new(StringComparer.OrdinalIgnoreCase);

	/// <summary>记录一次请求。</summary>
	/// <param name="route">路由标识（如 <c>POST /api/ask</c>）。</param>
	/// <param name="statusCode">HTTP 状态码。</param>
	/// <param name="elapsedMs">耗时（毫秒）。</param>
	public void Record(string route, int statusCode, long elapsedMs)
	{
		if (string.IsNullOrWhiteSpace(route)) route = OtherRoute;

		try
		{
			lock (_gate)
			{
				if (!_routes.TryGetValue(route, out var m))
				{
					if (_routes.Count >= RouteCapacity && route != OtherRoute)
						route = OtherRoute;

					if (!_routes.TryGetValue(route, out m))
					{
						m = new RouteMetrics();
						_routes[route] = m;
					}
				}

				m.Count++;
				if (statusCode >= 500) m.Errors++;
				else if (statusCode >= 400) m.ClientErrors++;

				m.TotalMs += elapsedMs;
				if (elapsedMs > m.MaxMs) m.MaxMs = elapsedMs;

				if (m.Samples.Count < SampleCapacity) m.Samples.Add(elapsedMs);
				else
				{
					m.Samples[m.SampleCursor] = elapsedMs;
					m.SampleCursor = (m.SampleCursor + 1) % SampleCapacity;
				}
			}
		}
		catch
		{
			// 指标采集失败不影响主链路
		}
	}

	/// <summary>
	/// 记录一次 BI 管线分段耗时（固定 5 段，复用有界内存 + 异常静默范式）。
	/// 用于 <c>GET /metrics</c> 的 <c>pipelineStages</c> 输出。
	/// </summary>
	/// <param name="stage">分段键（见 <see cref="StageUnderstand"/> 等常量）。</param>
	/// <param name="elapsedMs">耗时（毫秒）。</param>
	public void RecordStage(string stage, long elapsedMs)
	{
		if (string.IsNullOrWhiteSpace(stage)) stage = OtherRoute;

		try
		{
			lock (_gate)
			{
				if (!_stages.TryGetValue(stage, out var m))
				{
					m = new RouteMetrics();
					_stages[stage] = m;
				}

				m.Count++;
				m.TotalMs += elapsedMs;
				if (elapsedMs > m.MaxMs) m.MaxMs = elapsedMs;

				if (m.Samples.Count < SampleCapacity) m.Samples.Add(elapsedMs);
				else
				{
					m.Samples[m.SampleCursor] = elapsedMs;
					m.SampleCursor = (m.SampleCursor + 1) % SampleCapacity;
				}
			}
		}
		catch
		{
			// 指标采集失败不影响主链路
		}
	}

	/// <summary>
	/// 记录一次管线结果分类（Reject / EarlyReturn / Repair）。<c>occurred=false</c> 时忽略，
	/// 便于调用方无条件传入各标志位而无需前置判断。
	/// 用于 <c>GET /metrics</c> 的 <c>outcomes</c> 输出。
	/// </summary>
	/// <param name="outcome">分类键（见 <see cref="OutcomeReject"/> 等常量）。</param>
	/// <param name="occurred">本次是否发生该分类。</param>
	public void RecordOutcome(string outcome, bool occurred)
	{
		if (string.IsNullOrWhiteSpace(outcome) || !occurred) return;

		try
		{
			lock (_gate)
			{
				_outcomes.TryGetValue(outcome, out var c);
				_outcomes[outcome] = c + 1;
			}
		}
		catch
		{
			// 指标采集失败不影响主链路
		}
	}

	/// <summary>输出全部管线分段的指标快照（按请求数降序）。</summary>
	public IReadOnlyList<StageMetricsSnapshot> StageSnapshot()
	{
		try
		{
			List<KeyValuePair<string, RouteMetrics>> copy;
			lock (_gate)
			{
				copy = _stages.ToList();
			}

			return copy
				.Select(kv => new StageMetricsSnapshot(
					kv.Key,
					kv.Value.Count,
					kv.Value.Count == 0 ? 0 : Math.Round(kv.Value.TotalMs / (double)kv.Value.Count, 2),
					Percentile(kv.Value.Samples, 0.95),
					kv.Value.MaxMs))
				.OrderByDescending(s => s.Count)
				.ToList();
		}
		catch
		{
			return Array.Empty<StageMetricsSnapshot>();
		}
	}

	/// <summary>输出管线结果分类计数快照。</summary>
	public IReadOnlyList<OutcomeCounter> OutcomeSnapshot()
	{
		try
		{
			List<KeyValuePair<string, long>> copy;
			lock (_gate)
			{
				copy = _outcomes.ToList();
			}

			return copy
				.Select(kv => new OutcomeCounter(kv.Key, kv.Value))
				.OrderBy(kv => kv.Outcome, StringComparer.OrdinalIgnoreCase)
				.ToList();
		}
		catch
		{
			return Array.Empty<OutcomeCounter>();
		}
	}

	/// <summary>输出全部路由的指标快照（按请求数降序）。</summary>
	public IReadOnlyList<RouteMetricsSnapshot> Snapshot()
	{
		try
		{
			List<KeyValuePair<string, RouteMetrics>> copy;
			lock (_gate)
			{
				copy = _routes.ToList();
			}

			return copy
				.Select(kv => new RouteMetricsSnapshot(
					kv.Key,
					kv.Value.Count,
					kv.Value.Errors,
					kv.Value.ClientErrors,
					kv.Value.Count == 0 ? 0 : Math.Round(kv.Value.TotalMs / (double)kv.Value.Count, 2),
					Percentile(kv.Value.Samples, 0.95),
					kv.Value.MaxMs))
				.OrderByDescending(s => s.Count)
				.ToList();
		}
		catch
		{
			return Array.Empty<RouteMetricsSnapshot>();
		}
	}

	/// <summary>清空所有指标（运维用）。</summary>
	public void Reset()
	{
		try
		{
			lock (_gate)
			{
				_routes.Clear();
				_stages.Clear();
				_outcomes.Clear();
			}
		}
		catch
		{
			// 忽略
		}
	}

	private static double Percentile(List<long> samples, double p)
	{
		if (samples.Count == 0) return 0;

		var sorted = samples.ToArray();
		Array.Sort(sorted);

		var rank = (int)Math.Ceiling(p * sorted.Length) - 1;
		if (rank < 0) rank = 0;
		if (rank >= sorted.Length) rank = sorted.Length - 1;
		return sorted[rank];
	}

	private sealed class RouteMetrics
	{
		public long Count;
		public long Errors;
		public long ClientErrors;
		public long TotalMs;
		public long MaxMs;
		public int SampleCursor;
		public List<long> Samples { get; } = new();
	}
}

/// <summary>单条路由的指标快照。</summary>
/// <param name="Route">路由标识。</param>
/// <param name="Count">请求数。</param>
/// <param name="Errors">服务端错误数（HTTP &gt;= 500）。</param>
/// <param name="ClientErrors">客户端错误数（HTTP 400~499）。</param>
/// <param name="AvgMs">平均耗时（毫秒）。</param>
/// <param name="P95Ms">P95 耗时（毫秒）。</param>
/// <param name="MaxMs">最大耗时（毫秒）。</param>
public sealed record RouteMetricsSnapshot(
	string Route,
	long Count,
	long Errors,
	long ClientErrors,
	double AvgMs,
	double P95Ms,
	long MaxMs)
{
	/// <summary>服务端错误率（0~1，保留 4 位小数）。</summary>
	public double ErrorRate => Count == 0 ? 0 : Math.Round(Errors / (double)Count, 4);
}

/// <summary>单段管线分段延迟快照。</summary>
/// <param name="Stage">分段键。</param>
/// <param name="Count">采样次数。</param>
/// <param name="AvgMs">平均耗时（毫秒）。</param>
/// <param name="P95Ms">P95 耗时（毫秒）。</param>
/// <param name="MaxMs">最大耗时（毫秒）。</param>
public sealed record StageMetricsSnapshot(
	string Stage,
	long Count,
	double AvgMs,
	double P95Ms,
	long MaxMs);

/// <summary>管线结果分类计数快照。</summary>
/// <param name="Outcome">分类键。</param>
/// <param name="Count">发生次数。</param>
public sealed record OutcomeCounter(
	string Outcome,
	long Count);
