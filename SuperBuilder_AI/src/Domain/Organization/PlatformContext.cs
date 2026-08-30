namespace SuperBuilder_AI.Models.Organization;

using SuperBuilder_AI.Models.Theme;

/// <summary>
/// 平台级运行时上下文（P4 增量建设）。
///
/// 聚合 Tenant / User / Workspace / Locale / Theme，作为所有核心 Runtime 入口的统一上下文载体。
/// 已落地：<see cref="Tenant"/>（P4）、<see cref="Locale"/>（P5）、<see cref="Theme"/>（P7）；
/// 仍为占位：User / Workspace（P10 IAM）。
/// </summary>
public sealed record PlatformContext
{
	/// <summary>当前租户上下文。默认 <see cref="TenantContext.System"/>（全局）。</summary>
	public TenantContext Tenant { get; init; } = TenantContext.System;

	/// <summary>操作用户标识（P10 IAM 填充）。</summary>
	public long? UserId { get; init; }

	/// <summary>工作区标识（P4 后续扩展；P7.2 级联解析暂未启用工作区级主题）。</summary>
	public long? WorkspaceId { get; init; }

	/// <summary>
	/// 语言区域上下文（P5 Multi-Language 填充）。
	/// 默认 <see cref="LocaleContext.Default"/>（zh-CN），与平台既有中文业务语义一致；
	/// 未显式指定语言的路径（含 Golden 运行时）恒取默认值 → 行为与 P5 之前完全一致。
	/// </summary>
	public LocaleContext Locale { get; init; } = LocaleContext.Default;

	/// <summary>
	/// 主题上下文（P7.2 Theme 填充）。默认 <see cref="ThemeContext.Default"/>（内置浅色主题）。
	/// 由 <see cref="SuperBuilder_AI.Interfaces.Theme.IThemeResolver"/> 在运行时解析后注入；
	/// 未显式解析的路径（含 Golden 运行时）恒取内置默认 → 行为与 P7.2 之前完全一致。
	/// </summary>
	public ThemeContext Theme { get; init; } = ThemeContext.Default;

	/// <summary>由租户标识构造一个作用域内的平台上下文（语言区域取 <see cref="LocaleContext.Default"/>）。</summary>
	public static PlatformContext FromTenant(long tenantId, string? tenantCode = null)
		=> new() { Tenant = TenantContext.Scoped(tenantId, tenantCode) };

	/// <summary>
	/// 由租户标识 + 语言区域构造一个作用域内的平台上下文（P5）。
	/// <paramref name="culture"/> 为 null/空白或无法识别时回退到 <see cref="LocaleContext.Default"/>。
	/// </summary>
	public static PlatformContext FromTenant(long tenantId, string? tenantCode, string? culture)
		=> new()
		{
			Tenant = TenantContext.Scoped(tenantId, tenantCode),
			Locale = LocaleContext.FromCulture(culture),
		};

	/// <summary>系统/全局平台上下文（无租户隔离）。</summary>
	public static PlatformContext System { get; } = new() { Tenant = TenantContext.System };
}
