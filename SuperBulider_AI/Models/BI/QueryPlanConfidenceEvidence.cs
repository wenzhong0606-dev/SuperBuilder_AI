namespace SuperBulider_AI.Models.BI;

/// <summary>
/// QueryPlan Confidence 的可解释证据。
/// 
/// Phase 2.4
/// 
/// 该对象只描述：
///
/// “为什么这个 QueryPlan 得到这样的 Confidence。”
///
/// 不负责计算 Confidence。
/// 不负责 Decision。
/// </summary>
public sealed class QueryPlanConfidenceEvidence
{
	// ============================================================
	// Semantic Evidence
	// ============================================================

	/// <summary>
	/// 整体 Metadata Semantic Match 分数。
	/// 范围建议为 0~1。
	/// </summary>
	public double SemanticMatchScore { get; set; }

	/// <summary>
	/// Table Match 分数。
	/// </summary>
	public double TableMatchScore { get; set; }

	/// <summary>
	/// Field Match 分数。
	/// </summary>
	public double FieldMatchScore { get; set; }

	/// <summary>
	/// Metric Match 分数。
	/// </summary>
	public double MetricMatchScore { get; set; }

	/// <summary>
	/// Dimension Match 分数。
	/// </summary>
	public double DimensionMatchScore { get; set; }

	/// <summary>
	/// Filter Match 分数。
	/// </summary>
	public double FilterMatchScore { get; set; }


	// ============================================================
	// Candidate Ranking Evidence
	// ============================================================

	/// <summary>
	/// 当前 QueryPlan 相关 Semantic Candidate 的最高评分。
	/// </summary>
	public double CandidateRankingScore { get; set; }

	/// <summary>
	/// Top Candidate 与第二候选之间的分数差。
	/// </summary>
	public double CandidateRankingGap { get; set; }


	// ============================================================
	// Validation Evidence
	// ============================================================

	/// <summary>
	/// Validation 综合评分。
	/// 范围建议为 0~1。
	/// </summary>
	public double ValidationScore { get; set; }

	/// <summary>
	/// Validation Error 数量。
	/// </summary>
	public int ValidationErrorCount { get; set; }

	/// <summary>
	/// Validation Warning 数量。
	/// </summary>
	public int ValidationWarningCount { get; set; }


	// ============================================================
	// Repair Evidence
	// ============================================================

	/// <summary>
	/// 实际执行的 Repair 次数。
	/// </summary>
	public int RepairCount { get; set; }

	/// <summary>
	/// QueryPlan 实际发生变化的次数。
	/// </summary>
	public int ChangedPlanCount { get; set; }

	/// <summary>
	/// Repair 是否发生 Stall。
	/// </summary>
	public bool RepairStalled { get; set; }

	/// <summary>
	/// Repair 是否检测到 Loop。
	/// </summary>
	public bool RepairLoopDetected { get; set; }

	/// <summary>
	/// Repair 是否失败。
	/// </summary>
	public bool RepairFailed { get; set; }

	/// <summary>
	/// 是否达到最大 Repair 次数。
	/// </summary>
	public bool MaxRepairAttemptsReached { get; set; }

	/// <summary>
	/// 当前 Repair Trace 最终状态。
	/// </summary>
	public QueryPlanRepairTraceStatus RepairStatus { get; set; }


	// ============================================================
	// Evidence Availability
	// ============================================================

	/// <summary>
	/// Semantic Evidence 是否真实可用。
	/// </summary>
	public bool SemanticEvidenceAvailable { get; set; }

	/// <summary>
	/// Table Evidence 是否真实可用。
	/// </summary>
	public bool TableEvidenceAvailable { get; set; }

	/// <summary>
	/// Field Evidence 是否真实可用。
	/// </summary>
	public bool FieldEvidenceAvailable { get; set; }

	/// <summary>
	/// Metric Evidence 是否真实可用。
	/// </summary>
	public bool MetricEvidenceAvailable { get; set; }

	/// <summary>
	/// Dimension Evidence 是否真实可用。
	/// </summary>
	public bool DimensionEvidenceAvailable { get; set; }

	/// <summary>
	/// Filter Evidence 是否真实可用。
	/// </summary>
	public bool FilterEvidenceAvailable { get; set; }
}