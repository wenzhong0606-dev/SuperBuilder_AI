using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Api.Caching;

/// <summary>
/// Ask 响应语义缓存（P11.5.1 性能/成本轨）。
///
/// <para>
/// 以「租户 + 归一化问题」为键缓存 BI 响应，命中则完全跳过
/// 查询理解 → 计划 → 校验 → 修复 → SQL → 执行 → 结果理解 全链路，
/// 直接省下多次 LLM 调用与一次数据库查询。
/// </para>
///
/// <para>
/// 只缓存 <see cref="BIResponse.Success"/> 为 <c>true</c> 的响应：
/// 失败/被 Decision Gate 阻断的结果不缓存，避免把瞬时故障（如 LLM 403 限流）钉死在缓存里。
/// </para>
/// </summary>
public interface IAskResponseCache
{
	/// <summary>尝试读取缓存命中项。</summary>
	/// <param name="tenantId">租户 ID（缓存键隔离维度）。</param>
	/// <param name="question">原始问题（内部做归一化）。</param>
	/// <param name="dataSourceId">数据源 ID（0 表示默认）。</param>
	/// <returns>命中返回缓存响应，否则 null。</returns>
	BIResponse? Get(long tenantId, string question, long dataSourceId);

	/// <summary>写入缓存。仅应在响应成功时调用。</summary>
	void Set(long tenantId, string question, long dataSourceId, BIResponse response);

	/// <summary>累计命中/未命中次数，用于 /metrics 与运维观测。</summary>
	(long Hits, long Misses) Snapshot();

	/// <summary>清空缓存（运维用）。</summary>
	void Clear();
}
