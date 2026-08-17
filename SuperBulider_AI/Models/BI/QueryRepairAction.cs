namespace SuperBulider_AI.Models.BI;

/// <summary>
/// Query 修复动作。
///
/// Phase 2.2.5
///
/// 用于描述：
///
/// Validation失败
///      ↓
/// Repair策略
/// </summary>
public class QueryRepairAction
{
	/// <summary>
	/// 修复类型。
	/// </summary>
	public string Type { get; set; }
		= string.Empty;


	/// <summary>
	/// 原始错误编码。
	/// </summary>
	public string ErrorCode { get; set; }
		= string.Empty;


	/// <summary>
	/// 修复说明。
	/// </summary>
	public string Description { get; set; }
		= string.Empty;
}