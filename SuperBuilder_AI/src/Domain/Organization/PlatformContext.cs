namespace SuperBuilder_AI.Models.Organization;

/// <summary>
/// 平台级运行时上下文（P4 增量建设）。
///
/// 聚合 Tenant / User / Workspace / Locale / Theme，作为所有核心 Runtime 入口的统一上下文载体。
/// 当前阶段先落地 <see cref="Tenant"/>；User / Workspace / Locale / Theme 为占位属性，
/// 后续由 P5（Multi-Language）/ P7（Theme）/ P10（IAM）填充。
/// </summary>
public sealed record PlatformContext
{
	/// <summary>当前租户上下文。默认 <see cref="TenantContext.System"/>（全局）。</summary>
	public TenantContext Tenant { get; init; } = TenantContext.System;

	/// <summary>操作用户标识（P10 IAM 填充）。</summary>
	public long? UserId { get; init; }

	/// <summary>工作区标识（P4 后续扩展）。</summary>
	public long? WorkspaceId { get; init; }

	/// <summary>语言区域，如 zh-CN / en-US（P5 Multi-Language 填充）。</summary>
	public string? Locale { get; init; }

	/// <summary>主题标识（P7 Theme 填充）。</summary>
	public string? Theme { get; init; }

	/// <summary>由租户标识构造一个作用域内的平台上下文。</summary>
	public static PlatformContext FromTenant(long tenantId, string? tenantCode = null)
		=> new() { Tenant = TenantContext.Scoped(tenantId, tenantCode) };

	/// <summary>系统/全局平台上下文（无租户隔离）。</summary>
	public static PlatformContext System { get; } = new() { Tenant = TenantContext.System };
}
