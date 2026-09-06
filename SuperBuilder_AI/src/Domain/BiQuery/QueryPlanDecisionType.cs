namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// QueryPlan Decision Gate 最终决策状态（M5-07 状态化）。
///
/// 取代原先「3 值枚举 + ShouldExecute / RequiresConfirmation 双布尔」的
/// 隐式表达，统一为单一、机器可读的 5 态枚举：
///
/// - <see cref="Reject"/>            拒绝执行，禁止进入 SQL Builder；
/// - <see cref="RequireApproval"/>   需要人工审批/确认后才可执行；
/// - <see cref="Allow"/>             允许直接进入 SQL Builder；
/// - <see cref="AskClarification"/>  问题意图歧义，需向用户澄清后再决策；
/// - <see cref="LimitedExecution"/>  允许执行但受限（如行数上限/降级），由治理阶段写入。
///
/// 整型值保持向后兼容：Reject=0 / RequireApproval=1 / Allow=2 与原
/// Reject / Confirm / Proceed 一一对应；AskClarification / LimitedExecution
/// 为新增扩展态，供 M5-05（列级安全）、M5-06（成本治理）等阶段注入。
/// </summary>
public enum QueryPlanDecisionType
{
	/// <summary>
	/// 拒绝继续执行。
	/// QueryPlan 不允许进入 SQL Builder。
	/// </summary>
	Reject = 0,

	/// <summary>
	/// 需要进一步人工审批/确认。
	/// QueryPlan 当前不允许无条件自动执行。
	/// （原 Confirm 状态）
	/// </summary>
	RequireApproval = 1,

	/// <summary>
	/// 允许继续执行。
	/// QueryPlan 可以进入 SQL Builder。
	/// （原 Proceed 状态）
	/// </summary>
	Allow = 2,

	/// <summary>
	/// 问题意图歧义，需要向用户澄清后才能形成可执行决策。
	/// 不进入 SQL Builder。
	/// </summary>
	AskClarification = 3,

	/// <summary>
	/// 允许执行，但受约束（如结果行数上限、降级模型/扫描）。
	/// 由 M5-06 成本治理等阶段在 Decision Gate 之后注入。
	/// </summary>
	LimitedExecution = 4
}
