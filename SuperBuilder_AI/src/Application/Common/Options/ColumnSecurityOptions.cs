using System.Collections.Generic;

namespace SuperBuilder_AI.Application.Common.Options;

/// <summary>
/// 列级安全分类模式（M5-08）。
/// </summary>
public enum ColumnClassifierMode
{
	/// <summary>零拦截：任何列都不视为受限（默认，向后兼容 M5-05 的 DenyNothing）。</summary>
	DenyNothing = 0,

	/// <summary>策略驱动：按内置 PII 启发 + 配置列名 + 租户覆盖判定受限列（真实治理启用）。</summary>
	PolicyDriven = 1
}

/// <summary>
/// 列级安全租户级覆盖（M5-08）：逐租户覆写分类模式与受限列名清单。
/// 未设置的项回退到全局 <see cref="ColumnSecurityOptions"/>。
/// </summary>
public sealed class TenantColumnSecurityOverride
{
	/// <summary>该租户的分类模式；null = 沿用全局。</summary>
	public ColumnClassifierMode? Mode { get; set; }

	/// <summary>该租户额外受限列名（合并到全局 RestrictedColumnNames 之后判定）。</summary>
	public List<string> RestrictedColumnNames { get; set; } = new();
}

/// <summary>
/// 列级安全治理配置（M5-08）。
/// <para>
/// 设计原则：默认 <see cref="ClassifierMode"/> 为 DenyNothing（零默认行为变更），
/// 由平台按环境把配置节 <c>ColumnSecurity:ClassifierMode</c> 切换为 PolicyDriven 来
/// 启用真实治理（替换 M5-05 的 DenyNothing 桩）。逐租户还可经
/// <see cref="TenantOverrides"/> 灰度覆写。
/// </para>
/// </summary>
/// <example>
/// {
///   "ColumnSecurity": {
///     "ClassifierMode": "PolicyDriven",
///     "IncludeBuiltInPiiHeuristics": true,
///     "RestrictedColumnNames": [ "Salary", "BankAccount" ],
///     "TenantOverrides": { "1001": { "RestrictedColumnNames": [ "InternalNote" ] } }
///   }
/// }
/// </example>
public sealed class ColumnSecurityOptions
{
	public const string SectionName = "ColumnSecurity";

	/// <summary>全局分类模式。默认 DenyNothing（零拦截，向后兼容）。</summary>
	public ColumnClassifierMode ClassifierMode { get; set; } = ColumnClassifierMode.DenyNothing;

	/// <summary>显式受限列名（大小写不敏感、按列名精确匹配）。合并内置 PII 启发后判定。</summary>
	public List<string> RestrictedColumnNames { get; set; } = new();

	/// <summary>是否启用内置 PII 列名启发（常见敏感字段如 phone/email/id_card 等）。默认 true。</summary>
	public bool IncludeBuiltInPiiHeuristics { get; set; } = true;

	/// <summary>逐租户覆盖（tenantId → 覆写项）。用于灰度与差异化策略。</summary>
	public Dictionary<long, TenantColumnSecurityOverride> TenantOverrides { get; set; } = new();
}
