namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// QueryPlan 置信度评估结果。
/// 
/// Phase 2.4
/// QueryPlan Confidence & Decision Gate
/// </summary>
public sealed class QueryPlanConfidence
{
	/// <summary>
	/// QueryPlan 综合置信度。
	/// 范围：0~1。
	/// </summary>
	public double Score { get; set; }

	/// <summary>
	/// QueryPlan 置信度等级。
	/// </summary>
	public QueryPlanConfidenceLevel Level { get; set; }

	/// <summary>
	/// Confidence 本身是否满足进入 SQL Builder 的基础条件。
	///
	/// 注意：
	/// 最终是否允许执行，必须由 Decision Gate 决定。
	/// </summary>
	public bool CanProceed { get; set; }

	/// <summary>
	/// Confidence 的详细证据。
	/// </summary>
	public QueryPlanConfidenceEvidence Evidence { get; set; } = new();

	/// <summary>
	/// 支持当前 Confidence 结论的原因。
	/// </summary>
	public List<string> Reasons { get; set; } = new();

	/// <summary>
	/// 阻止 QueryPlan 自动执行的原因。
	/// </summary>
	public List<string> BlockingReasons { get; set; } = new();
}