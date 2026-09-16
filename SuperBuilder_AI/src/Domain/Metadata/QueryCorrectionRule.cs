using System;

namespace SuperBuilder_AI.Models.Metadata;

/// <summary>自主学习纠错规则类型。</summary>
public enum CorrectionKind
{
	/// <summary>表级覆盖：下次查询强制锁定目标物理表。</summary>
	TableOverride = 1,

	/// <summary>值映射：code→label 逐行替换单元格。</summary>
	ValueMap = 2,

	/// <summary>外键链接：补充字段→名称列绑定（同源或跨源）。</summary>
	FkJoin = 3,

	/// <summary>列展示：声明某列应显示其文本而非码值。</summary>
	ColumnDisplay = 4,
}

/// <summary>
/// 自主学习纠错规则：用户显式纠正后落库，下次查询按 (TenantId, UserId, 归一化问句) 匹配回放。
/// 严格 tenant+user 隔离；应用前校验目标数据源 ∈ 已授权集合，杜绝越权与跨租户污染。
/// </summary>
public class QueryCorrectionRule : BaseEntity
{
	/// <summary>所属租户（隔离根）。</summary>
	public long TenantId { get; set; }

	/// <summary>纠正归属用户（与租户共同隔离规则）。</summary>
	public long UserId { get; set; }

	/// <summary>规则作用的数据源（可选；表级/字典级可限定具体数据源）。</summary>
	public long? DataSourceId { get; set; }

	/// <summary>触发模式：归一化后的问句子串/关键词；匹配即回放。</summary>
	public string TriggerPattern { get; set; } = string.Empty;

	/// <summary>规则类型（CorrectionKind）。</summary>
	public CorrectionKind Kind { get; set; }

	/// <summary>规则载荷 JSON（按 Kind 解析：表名 / 值映射 / FK绑定 / 列声明）。</summary>
	public string PayloadJson { get; set; } = string.Empty;

	/// <summary>命中次数（用于置信与排序）。</summary>
	public int HitCount { get; set; }

	/// <summary>最近一次命中时间(UTC)。</summary>
	public DateTime? LastMatchedAt { get; set; }
}
