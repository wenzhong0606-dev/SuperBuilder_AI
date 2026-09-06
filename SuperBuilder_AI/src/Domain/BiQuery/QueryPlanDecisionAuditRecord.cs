namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// QueryPlan 决策全过程的可审计记录（M5-09 AI Decision Audit）。
///
/// 覆盖一次 QueryPlan 构建的八类可追溯维度：
/// 问题、意图、计划（含 SQL 化要素）、修复、置信度、决策、SQL、模型。
///
/// 设计约束：
/// - 纯数据记录（不持有行为），便于序列化落盘或转发至可观测后端；
/// - <see cref="Sql"/> / <see cref="Model"/> 为预留字段：管线本身不生成 SQL 文本、
///   也不持有 LLM 模型标识，由上游（SQL Builder / 调用方）在可用时填充；
/// - 任何字段在上下文不完整（如 EarlyResponse 短路于早期阶段）时允许为 null，
///   审计采集器需优雅处理。
/// </summary>
public sealed class QueryPlanDecisionAuditRecord
{
	/// <summary>本次审计记录唯一标识。</summary>
	public Guid AuditId { get; set; } = Guid.NewGuid();

	/// <summary>
	/// 请求级关联标识。同一次用户请求（可能跨多次 Pipeline 运行）共享，
	/// 用于把分散的审计记录串联成完整链路。
	/// </summary>
	public string? CorrelationId { get; set; }

	/// <summary>审计时间点（UTC）。</summary>
	public DateTimeOffset EvaluatedAt { get; set; } = DateTimeOffset.UtcNow;

	// =========================================================
	// 1. 问题（Question）
	// =========================================================

	/// <summary>用户原始问题。</summary>
	public string? Question { get; set; }

	// =========================================================
	// 2. 意图（Intent）
	// =========================================================

	/// <summary>意图类型（Detail / Aggregate / Ranking / Comparison / Trend）。</summary>
	public string? IntentType { get; set; }

	/// <summary>意图可读摘要（原问题 + 指标/维度/过滤数量 + Limit）。</summary>
	public string? IntentSummary { get; set; }

	// =========================================================
	// 3. 计划（Plan）——SQL 可重建源
	// =========================================================

	/// <summary>计划涉及的目标表名列表（主表 + 连接表）。</summary>
	public List<string> PlanTableNames { get; set; } = new();

	/// <summary>计划投影字段（列名）列表。</summary>
	public List<string> PlanFields { get; set; } = new();

	/// <summary>计划过滤条件数量。</summary>
	public int PlanFilterCount { get; set; }

	/// <summary>计划排序条件数量。</summary>
	public int PlanOrderCount { get; set; }

	/// <summary>计划连接数量。</summary>
	public int PlanJoinCount { get; set; }

	/// <summary>计划结果行数上限。</summary>
	public int? PlanLimit { get; set; }

	/// <summary>计划是否为聚合查询。</summary>
	public bool PlanIsAggregate { get; set; }

	// =========================================================
	// 4. SQL（预留——管线不生成 SQL 文本）
	// =========================================================

	/// <summary>
	/// 生成的 SQL 文本（预留）。
	/// 管线阶段不产出 SQL，由下游 SQL Builder 在可用时填充本次审计记录。
	/// </summary>
	public string? Sql { get; set; }

	// =========================================================
	// 5. 修复（Repair / Fixes）
	// =========================================================

	/// <summary>是否发生过自动修复。</summary>
	public bool HasRepair { get; set; }

	/// <summary>修复闭环最终状态。</summary>
	public QueryPlanRepairTraceStatus? RepairStatus { get; set; }

	/// <summary>实际执行的修复次数。</summary>
	public int RepairAttempts { get; set; }

	/// <summary>真正修改计划的修复次数。</summary>
	public int RepairChangedPlanCount { get; set; }

	/// <summary>修复闭环停止原因。</summary>
	public string? RepairStopReason { get; set; }

	/// <summary>修复历史条目数量。</summary>
	public int RepairHistoryCount { get; set; }

	// =========================================================
	// 6. 置信度（Confidence）
	// =========================================================

	/// <summary>综合置信度分数（0~1）。</summary>
	public double? ConfidenceScore { get; set; }

	/// <summary>置信度等级。</summary>
	public QueryPlanConfidenceLevel? ConfidenceLevel { get; set; }

	/// <summary>置信度本身是否满足进入 SQL Builder 的基础条件。</summary>
	public bool? CanProceed { get; set; }

	/// <summary>是否为合法明细列表（Medium 亦可执行）。</summary>
	public bool? IsExecutableDetailQuery { get; set; }

	/// <summary>校验错误数量（来自置信度证据）。</summary>
	public int? ValidationErrorCount { get; set; }

	/// <summary>修复计数（来自置信度证据）。</summary>
	public int? RepairCount { get; set; }

	// =========================================================
	// 7. 决策（Decision）
	// =========================================================

	/// <summary>Decision Gate 最终决策状态（5 态枚举）。</summary>
	public QueryPlanDecisionType? DecisionType { get; set; }

	/// <summary>Decision Gate 主要理由。</summary>
	public string? DecisionReason { get; set; }

	/// <summary>是否允许进入 SQL Builder。</summary>
	public bool? ShouldExecute { get; set; }

	/// <summary>是否需要进一步确认/澄清。</summary>
	public bool? RequiresConfirmation { get; set; }

	/// <summary>决策轨迹摘要（置信度/校验/修复等关键证据）。</summary>
	public string? DecisionTraceSummary { get; set; }

	// =========================================================
	// 8. 模型（Model，预留）
	// =========================================================

	/// <summary>
	/// 生成本次计划的 LLM 模型标识（预留）。
	/// 管线上下文当前不持有模型名，由调用方在可用时填充。
	/// </summary>
	public string? Model { get; set; }

	// =========================================================
	// 结果（Outcome）
	// =========================================================

	/// <summary>
	/// 本次 Pipeline 运行的最终结果分类。
	/// Executed / LimitedExecution / RequiresApproval / AskClarification / Rejected /
	/// EarlyResponse（任一阶段短路提前返回）。
	/// </summary>
	public AuditOutcome Outcome { get; set; }

	/// <summary>若结果为 EarlyResponse 或 Rejected，携带的错误/阻断信息。</summary>
	public string? ErrorMessage { get; set; }
}

/// <summary>
/// 审计记录结果分类（M5-09）。
/// </summary>
public enum AuditOutcome
{
	/// <summary>计划被允许并正常构建完成（含 Allow / LimitedExecution 可执行态）。</summary>
	Executed = 0,

	/// <summary>需要人工审批/确认后才可执行。</summary>
	RequiresApproval = 1,

	/// <summary>需向用户澄清意图后再决策。</summary>
	AskClarification = 2,

	/// <summary>Decision Gate 明确拒绝执行。</summary>
	Rejected = 3,

	/// <summary>任一阶段设置 EarlyResponse 提前短路返回。</summary>
	EarlyResponse = 4
}
