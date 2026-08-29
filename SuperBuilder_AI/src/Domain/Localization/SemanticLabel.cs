namespace SuperBuilder_AI.Models.Localization;

/// <summary>
/// 业务语义多语言标签（P5 Multi-Language Runtime）。
///
/// 把"同一语义概念在不同语言下的表述"独立建模，避免把多语言文本硬编码进
/// <c>MetadataSemantic</c> 或 <c>BusinessEntity</c> 的单一列里：
///
/// <list type="bullet">
/// <item><c>销售额</c> / <c>Sales Amount</c> / <c>売上高</c> / <c>매출액</c> → 同一 Concept 的 4 条标签。</item>
/// <item>新增语言只需插行，不动表结构、不动既有语义数据（P5 对既有链路零侵入）。</item>
/// </list>
///
/// <para>
/// 采用"概念类型 + 概念 Id"的弱多态关联（而非为每个宿主实体建一张标签表）：
/// 一套标签机制同时服务字段级语义（<c>MetadataSemantic</c>）与业务实体（P3 <c>BusinessEntity*</c>）。
/// 代价是放弃数据库外键约束，由应用层保证引用完整性——与本平台"业务库动态、无法建跨库 FK"的现状一致。
/// </para>
/// </summary>
public class SemanticLabel : BaseEntity
{
	/// <summary>
	/// 所属租户 Id；<c>0</c> 表示全局共享标签（对所有租户可见，跨租户复用同一份译文）。
	/// 刻意使用非可空 <c>long</c>：P4.3 的教训是 <c>long?</c> 列参与 <c>HasQueryFilter</c>
	/// 的 <c>||</c> 表达式会产生被提升的 <c>bool?</c>，触发 EF "Nullable object must have a value"。
	/// </summary>
	public long TenantId { get; set; }

	/// <summary>概念类型，取值见 <see cref="SemanticConceptTypes"/>（如 <c>MetadataSemantic</c>）。</summary>
	public string ConceptType { get; set; } = string.Empty;

	/// <summary>概念实体主键（对应 <see cref="ConceptType"/> 所指实体的 Id）。</summary>
	public long ConceptId { get; set; }

	/// <summary>归一化语言标签，如 <c>zh-CN</c> / <c>en-US</c>。</summary>
	public string Culture { get; set; } = string.Empty;

	/// <summary>标签种类，取值见 <see cref="SemanticLabelKinds"/>（如 <c>DisplayName</c>）。</summary>
	public string LabelKind { get; set; } = string.Empty;

	/// <summary>该语言下的标签文本。</summary>
	public string Value { get; set; } = string.Empty;

	/// <summary>标签来源：<c>Manual</c>（人工维护）/ <c>AI</c>（模型生成）/ <c>Import</c>（批量导入）。</summary>
	public string? Source { get; set; }

	/// <summary>同义词等多值标签的展示顺序（小者优先）。</summary>
	public int SortOrder { get; set; }
}

/// <summary>
/// 语义概念类型常量（P5）。
/// 集中定义以避免魔法字符串散落，新增宿主实体时在此登记即可。
/// </summary>
public static class SemanticConceptTypes
{
	/// <summary>字段级语义（<c>MetadataSemantic</c>）。</summary>
	public const string MetadataSemantic = "MetadataSemantic";

	/// <summary>业务实体（P3 <c>BusinessEntity</c>）。</summary>
	public const string BusinessEntity = "BusinessEntity";

	/// <summary>业务实体属性（P3 <c>BusinessEntityAttribute</c>）。</summary>
	public const string BusinessEntityAttribute = "BusinessEntityAttribute";

	/// <summary>业务实体指标（P3 <c>BusinessEntityMetric</c>）。</summary>
	public const string BusinessEntityMetric = "BusinessEntityMetric";

	/// <summary>业务域（P3 <c>BusinessDomain</c>）。</summary>
	public const string BusinessDomain = "BusinessDomain";
}

/// <summary>
/// 标签种类常量（P5）。
/// </summary>
public static class SemanticLabelKinds
{
	/// <summary>展示名（如"销售额"）。</summary>
	public const string DisplayName = "DisplayName";

	/// <summary>描述文本（如"表示订单交易产生的销售金额"）。</summary>
	public const string Description = "Description";

	/// <summary>同义词（可多值，按 <see cref="SemanticLabel.SortOrder"/> 排序）。</summary>
	public const string Synonym = "Synonym";

	/// <summary>示例问句（用于增强语义检索）。</summary>
	public const string Question = "Question";
}
