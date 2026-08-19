namespace SuperBulider_AI.Models.BI;

/// <summary>
/// QueryPlan Repair Trace。
///
/// Phase 2.3.4
///
/// 用于记录 QueryPlan 自动修复闭环的完整过程。
///
/// QueryPlan
///     ↓
/// Validation
///     ↓
/// Repair
///     ↓
/// Re-Validation
///     ↓
/// PASS / STALL / LOOP / FAILED / MAX_ATTEMPTS
///
/// Trace 本身不参与 Repair 决策。
///
/// 主要用于：
///
/// 1. Explainability
/// 2. Debugging
/// 3. Observability
/// 4. Repair History
/// 5. 后续 AI Feedback / Learning
/// </summary>
public class QueryPlanRepairTrace
{
	/// <summary>
	/// Repair Loop 最终是否成功。
	/// </summary>
	public bool Success
	{
		get;
		set;
	}


	/// <summary>
	/// Repair Loop 最终状态。
	/// </summary>
	public QueryPlanRepairTraceStatus Status
	{
		get;
		set;
	}


	/// <summary>
	/// 实际执行的 Repair 次数。
	/// </summary>
	public int TotalAttempts
	{
		get;
		set;
	}


	/// <summary>
	/// QueryPlan 真正发生变化的次数。
	/// </summary>
	public int ChangedPlanCount
	{
		get;
		set;
	}


	/// <summary>
	/// Repair Loop 最终停止原因。
	/// </summary>
	public string? StopReason
	{
		get;
		set;
	}


	/// <summary>
	/// 每一次 Repair Attempt 的详细记录。
	/// </summary>
	public List<QueryPlanRepairTraceEntry> History
	{
		get;
		set;
	} = new();
}


/// <summary>
/// 单次 QueryPlan Repair Trace。
/// </summary>
public class QueryPlanRepairTraceEntry
{
	/// <summary>
	/// Repair Attempt 编号。
	/// 从 1 开始。
	/// </summary>
	public int Attempt
	{
		get;
		set;
	}


	/// <summary>
	/// RepairService 是否报告成功。
	/// </summary>
	public bool RepairSuccess
	{
		get;
		set;
	}


	/// <summary>
	/// Repair 是否真正修改了 QueryPlan。
	/// </summary>
	public bool PlanChanged
	{
		get;
		set;
	}


	/// <summary>
	/// Repair 后 QueryPlan Validation 是否通过。
	/// </summary>
	public bool ValidationPassed
	{
		get;
		set;
	}


	/// <summary>
	/// Repair 前 QueryPlan Fingerprint。
	/// </summary>
	public string? BeforePlanFingerprint
	{
		get;
		set;
	}


	/// <summary>
	/// Repair 后 QueryPlan Fingerprint。
	/// </summary>
	public string? AfterPlanFingerprint
	{
		get;
		set;
	}


	/// <summary>
	/// Repair 前 Validation Fingerprint。
	/// </summary>
	public string? BeforeValidationFingerprint
	{
		get;
		set;
	}


	/// <summary>
	/// Repair 后 Validation Fingerprint。
	/// </summary>
	public string? AfterValidationFingerprint
	{
		get;
		set;
	}


	/// <summary>
	/// RepairService 实际执行的 Repair Actions。
	/// </summary>
	public List<string> RepairActions
	{
		get;
		set;
	} = new();


	/// <summary>
	/// RepairService 对本次 Repair 的解释。
	/// </summary>
	public string? Explanation
	{
		get;
		set;
	}


	/// <summary>
	/// RepairService 返回的失败原因。
	/// </summary>
	public string? FailureReason
	{
		get;
		set;
	}


	/// <summary>
	/// Pipeline 检测到的 Stall 原因。
	/// </summary>
	public string? StallReason
	{
		get;
		set;
	}


	/// <summary>
	/// Repair 后 Validation Error 数量。
	/// </summary>
	public int ValidationErrorCount
	{
		get;
		set;
	}
}


/// <summary>
/// QueryPlan Repair Trace 最终状态。
/// </summary>
public enum QueryPlanRepairTraceStatus
{
	/// <summary>
	/// 初始 QueryPlan 已通过 Validation，
	/// 不需要 Repair。
	/// </summary>
	NotRequired = 0,


	/// <summary>
	/// QueryPlan 经过 Repair 后通过 Validation。
	/// </summary>
	Repaired = 1,


	/// <summary>
	/// Repair 没有产生有效进展，
	/// Pipeline 主动停止。
	/// </summary>
	Stalled = 2,


	/// <summary>
	/// RepairService 执行失败。
	/// </summary>
	Failed = 3,


	/// <summary>
	/// 达到最大 Repair 次数。
	/// </summary>
	MaxAttemptsReached = 4,


	/// <summary>
	/// QueryPlan Fingerprint 重复，
	/// 检测到 Repair Loop。
	/// </summary>
	LoopDetected = 5
}