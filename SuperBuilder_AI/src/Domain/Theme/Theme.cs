namespace SuperBuilder_AI.Models.Theme;

/// <summary>
/// 主题持久化实体（P7 Multi-Theme / Style Engine）。
///
/// <para>
/// <strong>持久化策略：DSL 文档整体存储</strong>。整个 <see cref="ThemeDsl"/> 序列化为
/// <see cref="DslJson"/> 存于本表，与 P6 的 <c>Dashboard</c> 一致——主题是一份<em>文档</em>，
/// 令牌集合随版本演进，拆表会导致每次主题扩展都要迁移。
/// </para>
///
/// <para>
/// 高频检索字段（<see cref="Key"/> / <see cref="Name"/>）做冗余列；<see cref="IsBuiltIn"/> 标记平台内置主题。
/// </para>
///
/// <para>
/// 租户作用域：<see cref="TenantId"/> == 0 表示内置/全局模板（对所有租户可见），
/// 与 <c>Dashboard</c> 的全局模板约定一致；不建立指向 <c>Tenant</c> 的外键（Tenant 表无 Id=0 行）。
/// </para>
/// </summary>
public class Theme : BaseEntity
{
	/// <summary>
	/// 所属租户 Id；<c>0</c> 表示内置/全局模板（对所有租户可见）。
	/// </summary>
	public long TenantId { get; set; }

	/// <summary>主题键（同租户内唯一，便于 API 定位与 AI 引用），取值见 <see cref="BuiltInThemeKeys"/>。</summary>
	public string Key { get; set; } = string.Empty;

	/// <summary>主题名称（冗余自 DSL，供列表页展示）。</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>是否平台内置主题（内置主题不可删，TenantId=0）。</summary>
	public bool IsBuiltIn { get; set; }

	/// <summary>DSL 版本号（冗余自 DSL，便于按版本做兼容处理）。</summary>
	public string DslVersion { get; set; } = ThemeDslVersions.Current;

	/// <summary>
	/// DSL 文档（JSON 序列化后的 <see cref="ThemeDsl"/>）。
	/// <strong>只允许结构化令牌，绝不存 CSS 字符串或裸 HTML</strong>。
	/// </summary>
	public string DslJson { get; set; } = string.Empty;
}
