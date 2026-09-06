namespace SuperBuilder_AI.Application.Common.Options;

/// <summary>
/// Ask 审计配置（M6-05）。
///
/// 位于配置节 <c>AskAudit</c>。默认 <see cref="Mode"/> = None，
/// 即不采集、不输出任何审计记录——与历史行为逐字节等价。
/// 运维通过配置（而非代码）开启审计输出。
/// </summary>
public sealed class AskAuditOptions
{
	/// <summary>配置节名称。</summary>
	public const string SectionName = "AskAudit";

	/// <summary>
	/// 审计输出模式。
	/// None（默认）：不采集、不输出，零行为变更；
	/// Log：以结构化日志（Information 级）输出审计记录。
	/// </summary>
	public AskAuditMode Mode { get; set; } = AskAuditMode.None;
}

/// <summary>
/// Ask 审计输出模式（M6-05）。
/// </summary>
public enum AskAuditMode
{
	/// <summary>不采集、不输出（默认）。</summary>
	None = 0,

	/// <summary>以结构化日志输出审计记录。</summary>
	Log = 1
}
