namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// Ask 审计产出结果（M6-05）。与 M5-09 的 <see cref="QueryPlanDecisionAuditRecord"/> 互补：
/// 前者记录管线内部决策全过程，本记录记录「一次 Ask 调用」的会话级可审计事实。
/// </summary>
public enum AskAuditOutcome
{
	/// <summary>成功返回结果（含已放行执行的明细列表）。</summary>
	Completed = 0,

	/// <summary>执行失败（异常或下游错误）。</summary>
	Failed = 1,

	/// <summary>需要用户确认（Decision Gate 命中 RequireApproval）。</summary>
	RequiresConfirmation = 2,

	/// <summary>进入澄清（Medium / AskClarification）。</summary>
	AskClarification = 3,

	/// <summary>被决策闸门拒绝（Reject）。</summary>
	Rejected = 4,

	/// <summary>用户取消/清空当前澄清会话。</summary>
	Cancelled = 5
}

/// <summary>
/// Ask 请求内分段耗时（M6-05 指标）：LLM/Metadata/Plan/DB/Repair 分段。
/// 以毫秒计；Repair 子阶段包含在 Plan 段内（管线未单独计时，文档化于此）。
/// </summary>
public sealed record AskSegmentTimings(
	long MetadataUnderstandMs,
	long PlanMs,
	long SqlBuildMs,
	long DbExecMs,
	long ResultUnderstandMs)
{
	/// <summary>各段耗时之和（毫秒）。</summary>
	public long TotalMs =>
		MetadataUnderstandMs + PlanMs + SqlBuildMs + DbExecMs + ResultUnderstandMs;
}

/// <summary>
/// 一次 Ask 调用的可审计记录（M6-05）。
///
/// <para>落库前敏感字段（原/重写问题、SQL 摘要、结果样本）须经 <see cref="AskPiiRedactor"/> 脱敏。</para>
/// <para><see cref="Model"/> 与 <see cref="Cost"/> 为预留字段：当前链路未采集模型名与逐请求成本，恒为 null。</para>
/// </summary>
public sealed record AskAuditRecord(
	/// <summary>记录标识。</summary>
	Guid AuditId,
	/// <summary>关联 Id（每次 Ask 调用生成，用于关联管线级决策审计）。</summary>
	string? CorrelationId,
	/// <summary>会话 Id（来自会话服务；首问可能为 null）。</summary>
	string? ConversationId,
	/// <summary>本轮序号（来自会话服务澄清计数）。</summary>
	int TurnIndex,
	/// <summary>原始问题（已脱敏）。</summary>
	string? OriginalQuestion,
	/// <summary>重写后的问题（已脱敏，仅澄清态非空）。</summary>
	string? RewrittenQuestion,
	/// <summary>本轮授权数据源集合。</summary>
	IReadOnlyList<long> AuthorizedDataSourceIds,
	/// <summary>决策类型（取自响应 Explanation.Decision）。</summary>
	QueryPlanDecisionType? DecisionType,
	/// <summary>SQL 摘要（已脱敏，截断至结构，不保留参数值）。</summary>
	string? SqlSummary,
	/// <summary>模型名（预留，恒为 null）。</summary>
	string? Model,
	/// <summary>总耗时（毫秒）。</summary>
	long DurationMs,
	/// <summary>逐请求成本（预留，恒为 null）。</summary>
	decimal? Cost,
	/// <summary>状态（"{ConversationStatus}.{AskBehavior}"）。</summary>
	string Status,
	/// <summary>审计产出结果。</summary>
	AskAuditOutcome Outcome,
	/// <summary>错误信息（失败态非空，已脱敏）。</summary>
	string? ErrorMessage,
	/// <summary>分段耗时（M6-05 指标）。</summary>
	AskSegmentTimings? SegmentTimings,
	/// <summary>结果样本摘要（已脱敏：PII 列值置 ***，非 PII 值截断）。</summary>
	string? ResultSample);
