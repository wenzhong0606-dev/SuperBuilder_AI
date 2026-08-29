namespace SuperBuilder_AI.Models.Organization;

/// <summary>
/// 租户级键值配置（P4 Multi-Tenant Platform Core）。
/// 以统一 KV 载体承载 Locale / Theme / Workspace 等平台参数，避免为每种配置单独建表。
/// Key 命名约定按业务域前缀，例如 "locale:culture"、"theme:primary"、"workspace:name"。
/// </summary>
public class TenantSetting : BaseEntity
{
	/// <summary>所属租户 Id。</summary>
	public long TenantId { get; set; }

	/// <summary>配置键（同一租户内唯一）。</summary>
	public string Key { get; set; } = string.Empty;

	/// <summary>配置值（以字符串存储，消费端按 DataType 反序列化）。</summary>
	public string? Value { get; set; }

	/// <summary>值类型提示：string | int | bool | json（便于消费端解析）。</summary>
	public string? DataType { get; set; }

	/// <summary>导航属性：所属租户。</summary>
	public Tenant? Tenant { get; set; }
}
