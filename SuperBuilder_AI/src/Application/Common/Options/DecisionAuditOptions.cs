namespace SuperBuilder_AI.Application.Common.Options;

/// <summary>
/// AI Decision Audit 配置（M5-09）。
///
/// 位于配置节 <c>DecisionAudit</c>。默认 <see cref="Mode"/> = None，
/// 即不采集、不输出任何审计记录——与历史行为逐字节等价。
/// 运维通过配置（而非代码）开启审计输出。
/// </summary>
public sealed class DecisionAuditOptions
{
	/// <summary>配置节名称。</summary>
	public const string SectionName = "DecisionAudit";

	/// <summary>
	/// 审计输出模式。
	/// None（默认）：不采集、不输出，零行为变更；
	/// Log：以结构化日志（Information 级）输出审计记录。
	/// </summary>
	public DecisionAuditMode Mode { get; set; } = DecisionAuditMode.None;
}

/// <summary>
/// 审计输出模式（M5-09）。
/// </summary>
public enum DecisionAuditMode
{
	/// <summary>不采集、不输出（默认）。</summary>
	None = 0,

	/// <summary>以结构化日志输出审计记录。</summary>
	Log = 1
}
