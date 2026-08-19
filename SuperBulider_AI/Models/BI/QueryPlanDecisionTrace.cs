namespace SuperBulider_AI.Models.BI;

/// <summary>
/// QueryPlan Decision Gate 的完整决策轨迹。
///
/// Phase 2.4
/// QueryPlan Confidence & Decision Gate
///
/// 用于回答：
///
/// “为什么 Decision Gate 最终做出了这个决定？”
///
/// 注意：
///
/// QueryPlanDecision
///     = 最终结果
///
/// QueryPlanDecisionTrace
///     = 决策过程证据
/// </summary>
public sealed class QueryPlanDecisionTrace
{
	/// <summary>
	/// Confidence Score。
	/// </summary>
	public double ConfidenceScore { get; set; }

	/// <summary>
	/// Confidence Level。
	/// </summary>
	public QueryPlanConfidenceLevel ConfidenceLevel { get; set; }

	/// <summary>
	/// 最终 Decision。
	/// </summary>
	public QueryPlanDecisionType Decision { get; set; }

	/// <summary>
	/// 是否允许进入 SQL Builder。
	/// </summary>
	public bool ShouldExecute { get; set; }

	/// <summary>
	/// 是否需要进一步确认。
	/// </summary>
	public bool RequiresConfirmation { get; set; }

	/// <summary>
	/// High Confidence 阈值。
	/// </summary>
	public double HighThreshold { get; set; }

	/// <summary>
	/// Medium Confidence 阈值。
	/// </summary>
	public double MediumThreshold { get; set; }

	/// <summary>
	/// Validation Error 数量。
	/// </summary>
	public int ValidationErrorCount { get; set; }

	/// <summary>
	/// Repair Count。
	/// </summary>
	public int RepairCount { get; set; }

	/// <summary>
	/// Repair Progress Score。
	/// </summary>
	public double RepairProgressScore { get; set; }

	/// <summary>
	/// Repair Stall。
	/// </summary>
	public bool RepairStalled { get; set; }

	/// <summary>
	/// Repair Loop。
	/// </summary>
	public bool RepairLoopDetected { get; set; }

	/// <summary>
	/// Repair Failed。
	/// </summary>
	public bool RepairFailed { get; set; }

	/// <summary>
	/// 是否达到最大 Repair 次数。
	/// </summary>
	public bool MaxRepairAttemptsReached { get; set; }

	/// <summary>
	/// Repair Trace 最终状态。
	/// </summary>
	public QueryPlanRepairTraceStatus RepairStatus { get; set; }

	/// <summary>
	/// Decision Gate 最终原因。
	/// </summary>
	public string? Reason { get; set; }

	/// <summary>
	/// Blocking Reasons。
	/// </summary>
	public List<string> BlockingReasons { get; set; } = new();

	/// <summary>
	/// Decision Gate 评估时间。
	/// </summary>
	public DateTimeOffset EvaluatedAt { get; set; }
}