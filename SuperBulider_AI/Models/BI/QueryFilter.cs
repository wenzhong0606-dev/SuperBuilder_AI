namespace SuperBulider_AI.Models.BI;

/// <summary>
/// 查询过滤条件。
///
/// 用户:
/// 2025年的订单
///
/// 转换:
///
/// OrderDate >= 2025-01-01
///
/// </summary>
public class QueryFilter
{

	/// <summary>
	/// Metadata字段
	/// </summary>
	public string Field { get; set; }
		= string.Empty;



	/// <summary>
	/// 比较符
	///
	/// =
	/// >
	/// <
	/// >=
	/// <=
	/// LIKE
	///
	/// </summary>
	public string Operator { get; set; }
		= "=";



	/// <summary>
	/// 条件值
	/// </summary>
	public string Value { get; set; }
		= string.Empty;

}
