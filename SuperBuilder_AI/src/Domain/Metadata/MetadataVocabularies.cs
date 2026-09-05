namespace SuperBuilder_AI.Models.Metadata;

/// <summary>
/// 元数据语义来源（受控词表）。
/// 对应 <see cref="MetadataSemantic.Source"/>，用于区分语义是人工维护还是 AI 推断。
/// </summary>
public enum SemanticSource
{
	/// <summary>人工维护。</summary>
	Manual = 0,

	/// <summary>AI 自动生成。</summary>
	AI = 1,

	/// <summary>从既有样本/规则推断。</summary>
	Inferred = 2
}

/// <summary>
/// 聚合类型（受控词表）。
/// </summary>
public enum AggregationType
{
	None = 0,
	Sum = 1,
	Count = 2,
	Avg = 3,
	Min = 4,
	Max = 5
}

/// <summary>
/// 关系类型（受控词表）。
/// </summary>
public enum RelationshipKind
{
	OneToOne = 0,
	OneToMany = 1,
	ManyToOne = 2,
	ManyToMany = 3
}

/// <summary>
/// 基数（受控词表）。
/// </summary>
public enum CardinalityKind
{
	One = 0,
	Many = 1
}

/// <summary>
/// 物理绑定类型（受控词表）。
/// </summary>
public enum BindingKind
{
	Direct = 0,
	Computed = 1,
	Lookup = 2,
	Expression = 3
}

/// <summary>
/// 物理角色（受控词表）。
/// </summary>
public enum PhysicalRoleKind
{
	Key = 0,
	Measure = 1,
	Dimension = 2,
	Attribute = 3
}

/// <summary>
/// 受控词表校验辅助。
/// </summary>
public static class MetadataVocabularyValidator
{
	/// <summary>校验关系类型字符串是否合法（忽略大小写）。</summary>
	public static bool IsValidRelationshipType(string? value)
		=> TryParse<RelationshipKind>(value);

	/// <summary>校验基数字符串是否合法（忽略大小写）。</summary>
	public static bool IsValidCardinality(string? value)
		=> TryParse<CardinalityKind>(value);

	/// <summary>校验绑定类型字符串是否合法（忽略大小写）。</summary>
	public static bool IsValidBindingType(string? value)
		=> TryParse<BindingKind>(value);

	/// <summary>校验物理角色字符串是否合法（忽略大小写）。</summary>
	public static bool IsValidPhysicalRole(string? value)
		=> TryParse<PhysicalRoleKind>(value);

	/// <summary>校验聚合类型字符串是否合法（忽略大小写）。</summary>
	public static bool IsValidAggregation(string? value)
		=> TryParse<AggregationType>(value);

	private static bool TryParse<TEnum>(string? value)
		where TEnum : struct, Enum
		=> !string.IsNullOrWhiteSpace(value)
			&& Enum.TryParse<TEnum>(value, ignoreCase: true, out _);
}
