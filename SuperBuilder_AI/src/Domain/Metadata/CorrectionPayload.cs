namespace SuperBuilder_AI.Models.Metadata;

/// <summary>
/// 纠错规则载荷（<see cref="QueryCorrectionRule.PayloadJson"/> 的反序列化形态）。
///
/// 按 <see cref="CorrectionKind"/> 只填对应字段：
/// TableOverride → TableName；
/// ValueMap      → ColumnName + ValueMap；
/// FkJoin        → SourceTable/SourceColumn + TargetTable/TargetColumn/TargetDisplayColumn；
/// ColumnDisplay → ColumnName（声明该列应展示文本而非码值）。
/// </summary>
public sealed class CorrectionPayload
{
	/// <summary>表级覆盖目标物理表名（TableOverride）。</summary>
	public string? TableName { get; set; }

	/// <summary>目标列名（ValueMap / ColumnDisplay）。</summary>
	public string? ColumnName { get; set; }

	/// <summary>码值 → 文本映射（ValueMap）。</summary>
	public Dictionary<string, string>? ValueMap { get; set; }

	/// <summary>
	/// 字典分类提示（ColumnDisplay）：如「type 应显示 receipt_type 名称」中的 receipt_type。
	/// 用于在字典表（含跨源 PMIS）中按分类列取值译码。
	///
	/// <para>
	/// 支持多分类，以 <c>,</c> 连接，如 <c>warehousing_type,outbound_type</c> ——
	/// WMS 单据表的 <c>type</c> 单列码值实测跨 5 个分类，单分类会漏译。
	/// </para>
	/// </summary>
	public string? CategoryHint { get; set; }

	/// <summary>外键所在表（FkJoin，可空表示跟随主表）。</summary>
	public string? SourceTable { get; set; }

	/// <summary>外键列（FkJoin）。</summary>
	public string? SourceColumn { get; set; }

	/// <summary>被引用表（FkJoin）。</summary>
	public string? TargetTable { get; set; }

	/// <summary>被引用键列（FkJoin）。</summary>
	public string? TargetColumn { get; set; }

	/// <summary>被引用展示列（FkJoin，如仓库名称列）。</summary>
	public string? TargetDisplayColumn { get; set; }
}
