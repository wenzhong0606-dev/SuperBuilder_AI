namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// QueryPlan Decision Gate 最终决策结果。
/// 
/// Phase 2.4
/// </summary>
public sealed class QueryPlanDecision
{
	/// <summary>
	/// Decision Gate 最终决策状态（M5-07 状态化：单一事实来源）。
	///
	/// <see cref="ShouldExecute"/> / <see cref="RequiresConfirmation"/> 均由该枚举派生，
	/// 不再各自独立赋值，杜绝「枚举与双布尔」漂移。
	/// </summary>
	public QueryPlanDecisionType Decision { get; set; }

	/// <summary>
	/// Decision 所依据的 Confidence。
	/// </summary>
	public QueryPlanConfidence Confidence { get; set; } = new();

	/// <summary>
	/// 是否允许 QueryPlan 进入 SQL Builder。
	/// 由 <see cref="Decision"/> 派生：Allow / LimitedExecution 可进入。
	/// </summary>
	public bool ShouldExecute =>
		Decision is QueryPlanDecisionType.Allow
			or QueryPlanDecisionType.LimitedExecution;

	/// <summary>
	/// 是否需要用户或上层流程进一步确认/澄清。
	/// 由 <see cref="Decision"/> 派生：RequireApproval / AskClarification 需要。
	/// </summary>
	public bool RequiresConfirmation =>
		Decision is QueryPlanDecisionType.RequireApproval
			or QueryPlanDecisionType.AskClarification;

	/// <summary>
	/// Decision Gate 给出的主要原因。
	/// </summary>
	public string? Reason { get; set; }

	// =========================================================
	// 5 态语义访问器（M5-07）
	// =========================================================

	/// <summary>是否拒绝执行（禁止进入 SQL Builder）。</summary>
	public bool IsReject => Decision == QueryPlanDecisionType.Reject;

	/// <summary>是否需要人工审批/确认后才可执行。</summary>
	public bool IsRequireApproval => Decision == QueryPlanDecisionType.RequireApproval;

	/// <summary>是否允许直接进入 SQL Builder。</summary>
	public bool IsAllow => Decision == QueryPlanDecisionType.Allow;

	/// <summary>是否需向用户澄清意图后再决策。</summary>
	public bool IsAskClarification => Decision == QueryPlanDecisionType.AskClarification;

	/// <summary>是否允许但受限执行（如行数上限/降级）。</summary>
	public bool IsLimitedExecution => Decision == QueryPlanDecisionType.LimitedExecution;

	/// <summary>
	/// Decision Gate 完整决策轨迹。
	/// </summary>
	public QueryPlanDecisionTrace Trace { get; set; } = new();
}