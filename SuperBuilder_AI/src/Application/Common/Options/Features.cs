namespace SuperBuilder_AI.Application.Common.Options;

/// <summary>
/// 运行时特性开关（§L.4 两阶段部署 / §L.5b 孤儿引用策略）。
/// 通过 appsettings 的 "Features" 段注入；生产环境默认 MetadataVersionFilterEnabled=false（阶段 A）。
/// </summary>
public class Features
{
	/// <summary>
	/// 元数据版本向量过滤总开关（§L.4）。
	/// <list type="bullet">
	///   <item>阶段 A=false：搜索沿用旧行为全量召回，后台跑 MetadataVectorBackfillJob 打标；</item>
	///   <item>阶段 B=true：严格 version==active 过滤生效，激活须经 VectorBackfillGate。</item>
	/// </list>
	/// </summary>
	public bool MetadataVersionFilterEnabled { get; set; }

	/// <summary>
	/// 孤儿引用处理策略（§L.5b）：Block=阻断激活（默认，fail-safe）；DisableAndAudit=自动停用并落审计。
	/// </summary>
	public string MetadataRemapDropMode { get; set; } = "Block";
}
