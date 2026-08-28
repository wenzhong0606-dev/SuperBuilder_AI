namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// Query Repair结果。
///
/// Phase 2.2.5
/// </summary>
public class QueryRepairResult
{
	/// <summary>
	/// 是否发生修复。
	/// </summary>
	public bool IsRepaired { get; set; }


	/// <summary>
	/// 修复后的动作列表。
	/// </summary>
	public List<QueryRepairAction> Actions { get; set; }
		= new();


	/// <summary>
	/// 修复说明。
	/// </summary>
	public string? Explanation { get; set; }
}