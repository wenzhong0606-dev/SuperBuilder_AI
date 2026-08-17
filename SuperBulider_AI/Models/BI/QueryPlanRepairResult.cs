namespace SuperBulider_AI.Models.BI;

/// <summary>
/// QueryPlan自动修复结果。
///
/// Phase 2.2.5
///
/// 表示:
///
/// Repair是否成功
///
/// 以及修复后的QueryPlan。
/// </summary>
public class QueryPlanRepairResult
{
	/// <summary>
	/// 是否修复成功。
	/// </summary>
	public bool Success
	{
		get;
		set;
	}



	/// <summary>
	/// 修复后的QueryPlan。
	///
	/// 成功时返回。
	/// </summary>
	public QueryPlan? Plan
	{
		get;
		set;
	}



	/// <summary>
	/// 修复说明。
	///
	/// 用于日志、
	/// AI解释。
	/// </summary>
	public string?
		Reason
	{
		get;
		set;
	}



	/// <summary>
	/// 修复次数。
	///
	/// 防止无限循环。
	/// </summary>
	public int RepairCount
	{
		get;
		set;
	}
}