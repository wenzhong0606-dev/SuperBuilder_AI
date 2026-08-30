namespace SuperBuilder_AI.Models.Theme;

/// <summary>主题解析来源（用于追溯级联命中的是哪一个层级）。</summary>
public enum ThemeSource
{
	/// <summary>内置默认主题（平台兜底）。</summary>
	BuiltIn = 0,

	/// <summary>租户级默认主题（来自 TenantSetting）。</summary>
	Tenant = 1,

	/// <summary>仪表盘显式指定的主题键。</summary>
	Dashboard = 2,

	/// <summary>工作区级主题（P10 IAM 工作区模型落地后再启用）。</summary>
	Workspace = 3,
}

/// <summary>
/// 运行时解析出的主题上下文（P7.2）。包装已解析的 <see cref="ThemeDsl"/> 及其来源，
/// 作为 <see cref="SuperBuilder_AI.Models.Organization.PlatformContext.Theme"/> 的载体，供渲染层（P7.3）直接消费。
/// <para>本对象只承载<strong>结构化设计令牌</strong>，绝不承载 CSS 字符串或任何标记语言——延续 P6/P7 红线。</para>
/// </summary>
public sealed record ThemeContext
{
	/// <summary>主题键。默认 <see cref="BuiltInThemeKeys.Default"/>。</summary>
	public string Key { get; init; } = BuiltInThemeKeys.Default;

	/// <summary>解析来源（级联命中的层级），用于排障与前端提示。</summary>
	public ThemeSource Source { get; init; } = ThemeSource.BuiltIn;

	/// <summary>已解析的结构化设计令牌（语义色值/字号/间距等纯数据）。</summary>
	public ThemeDsl Dsl { get; init; } = BuiltInThemes.DefaultDsl();

	/// <summary>系统/全局默认主题上下文（内置浅色主题）。</summary>
	public static ThemeContext Default { get; } = new();
}
