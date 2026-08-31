namespace SuperBuilder_AI.Api.Caching;

/// <summary>
/// Ask 语义响应缓存配置（P11.5.1 性能/成本轨）。
///
/// <para>
/// 绑定 <c>appsettings</c> 的 <c>P11Cache:Ask</c> 节；缺省值保守（默认启用、TTL 60s、最多 200 条），
/// 在未显式配置时也能工作。
/// </para>
///
/// <para>
/// <b>零回归</b>：缓存只作用于 <c>POST api/ask</c>（生产问数端点），
/// Golden 回归走独立的 <c>evaluation/golden-runtime</c> 端点，完全不经过本缓存。
/// </para>
/// </summary>
public sealed class AskCacheOptions
{
	/// <summary>配置节名。</summary>
	public const string SectionName = "P11Cache:Ask";

	/// <summary>是否启用缓存。默认 true。</summary>
	public bool Enabled { get; set; } = true;

	/// <summary>缓存有效期（秒）。默认 60；设为 &lt;= 0 视为不缓存。</summary>
	public int TtlSeconds { get; set; } = 60;

	/// <summary>最多缓存条目数（LRU 淘汰）。默认 200；设为 &lt;= 0 表示不限制。</summary>
	public int MaxEntries { get; set; } = 200;
}
