namespace SuperBulider_AI.Models.BI;

/// <summary>
/// QueryPlan Explainability 聚合结果。
///
/// Phase 2.5:
/// 将 QueryPlan Pipeline 已经产生的
/// Validation / Repair / Confidence / Decision
/// 聚合为统一的解释上下文。
///
/// 本模型不重新计算任何决策结果。
/// </summary>
public sealed class QueryPlanExplanation
{
	/// <summary>
	/// 用户原始问题。
	/// </summary>
	public string? Question
	{
		get;
		set;
	}

	/// <summary>
	/// 最终 QueryPlan。
	///
	/// 如果 QueryPlan 经过 Repair，
	/// 此处应为 Repair 后的最终 Plan。
	/// </summary>
	public QueryPlan? Plan
	{
		get;
		set;
	}

	/// <summary>
	/// 最终 QueryPlan Validation Result。
	/// </summary>
	public QuerySemanticValidationResult? ValidationResult
	{
		get;
		set;
	}

	/// <summary>
	/// QueryPlan Repair Trace。
	/// </summary>
	public QueryPlanRepairTrace? RepairTrace
	{
		get;
		set;
	}

	/// <summary>
	/// QueryPlan Confidence。
	/// </summary>
	public QueryPlanConfidence? Confidence
	{
		get;
		set;
	}

	/// <summary>
	/// QueryPlan Decision。
	/// </summary>
	public QueryPlanDecision? Decision
	{
		get;
		set;
	}

	/// <summary>
	/// Explainability 汇总结果。
	/// </summary>
	public QueryPlanExplanationSummary Summary
	{
		get;
		set;
	} = new();
}


/// <summary>
/// QueryPlan Explainability 汇总信息。
///
/// 注意：
/// 本对象不是新的事实来源。
///
/// 所有事实均来自：
/// - ValidationResult
/// - RepairTrace
/// - Confidence
/// - Decision
/// </summary>
public sealed class QueryPlanExplanationSummary
{
	/// <summary>
	/// QueryPlan 是否通过最终 Validation。
	/// </summary>
	public bool? IsValid
	{
		get;
		set;
	}

	/// <summary>
	/// 是否发生过 Repair。
	/// </summary>
	public bool HasRepair
	{
		get;
		set;
	}

	/// <summary>
	/// Repair 次数。
	/// </summary>
	public int RepairCount
	{
		get;
		set;
	}

	/// <summary>
	/// Repair 是否导致 QueryPlan 发生变化。
	/// </summary>
	public bool PlanChangedByRepair
	{
		get;
		set;
	}

	/// <summary>
	/// Confidence 分数。
	/// </summary>
	public double? ConfidenceScore
	{
		get;
		set;
	}

	/// <summary>
	/// Decision 是否允许执行。
	/// </summary>
	public bool? ShouldExecute
	{
		get;
		set;
	}

	/// <summary>
	/// Decision 是否要求人工确认。
	/// </summary>
	public bool? RequiresConfirmation
	{
		get;
		set;
	}

	/// <summary>
	/// Decision 原因。
	/// </summary>
	public string? DecisionReason
	{
		get;
		set;
	}

	/// <summary>
	/// Confidence / Decision 的解释原因。
	/// </summary>
	public List<string> Reasons
	{
		get;
		set;
	} = new();

	/// <summary>
	/// 阻止自动执行的原因。
	/// </summary>
	public List<string> BlockingReasons
	{
		get;
		set;
	} = new();

	/// <summary>
	/// Validation Error 数量。
	/// </summary>
	public int ValidationErrorCount
	{
		get;
		set;
	}

	/// <summary>
	/// Validation Warning 数量。
	/// </summary>
	public int ValidationWarningCount
	{
		get;
		set;
	}

	/// <summary>
	/// 是否存在可供 Explainability 使用的上下文。
	///
	/// 注意：
	/// true 不代表 QueryPlan 可以执行。
	/// </summary>
	public bool IsExplainable
	{
		get;
		set;
	}
}