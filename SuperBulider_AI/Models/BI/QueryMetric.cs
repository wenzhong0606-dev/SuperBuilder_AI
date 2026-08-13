namespace SuperBulider_AI.Models.BI;

/// <summary>
/// 查询指标。
///
/// 例如:
///
/// 销售金额
/// SUM(Amount)
///
/// 客户数量
/// COUNT(CustomerId)
///
/// </summary>
public class QueryMetric
{

	/// <summary>
	/// 用户描述
	/// </summary>
	public string Name { get; set; }
		= string.Empty;



	/// <summary>
	/// Metadata字段
	/// </summary>
	public string Field { get; set; }
		= string.Empty;



	/// <summary>
	/// 聚合方式
	///
	/// SUM
	/// COUNT
	/// AVG
	/// MAX
	/// MIN
	///
	/// </summary>
	public string Aggregation { get; set; }
		= "NONE";

}
