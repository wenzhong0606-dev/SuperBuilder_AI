namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// QueryPlan Validation Pipeline 最终结果。
///
/// Phase 2.3.4
///
/// Pipeline 不仅返回最终 QueryPlan 和 ValidationResult，
/// 同时返回完整的 RepairTrace。
///
/// 结构：
///
/// QueryPlanValidationPipelineResult
///     │
///     ├── Plan
///     │
///     ├── ValidationResult
///     │
///     └── RepairTrace
///           │
///           ├── Status
///           ├── TotalAttempts
///           ├── ChangedPlanCount
///           ├── StopReason
///           └── History
/// </summary>
public class QueryPlanValidationPipelineResult
{
	/// <summary>
	/// Pipeline 最终产生的 QueryPlan。
	///
	/// 如果初始 Validation 已经通过，
	/// 则为原始 QueryPlan。
	///
	/// 如果经过 Repair，
	/// 则为最后一次 Repair 后的 QueryPlan。
	/// </summary>
	public QueryPlan Plan
	{
		get;
		set;
	} = null!;


	/// <summary>
	/// Pipeline 最终 ValidationResult。
	///
	/// 注意：
	///
	/// 如果 Pipeline 因 Stall / Failed / MaxAttempts
	/// 停止，则这里可能仍然是 Invalid。
	/// </summary>
	public QuerySemanticValidationResult ValidationResult
	{
		get;
		set;
	} = null!;


	/// <summary>
	/// QueryPlan Repair Trace。
	///
	/// 包含整个 Repair Loop 的结构化执行历史。
	/// </summary>
	public QueryPlanRepairTrace RepairTrace
	{
		get;
		set;
	} = new();
}