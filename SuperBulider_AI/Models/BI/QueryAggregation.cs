namespace SuperBulider_AI.Models.BI;

/// <summary>
/// BI 查询聚合方式。
///
/// V2.0:
/// 不再依赖任意字符串判断聚合类型。
/// </summary>
public enum QueryAggregation
{
	/// <summary>
	/// 不聚合。
	/// </summary>
	None = 0,

	/// <summary>
	/// 求和。
	/// </summary>
	Sum,

	/// <summary>
	/// 计数。
	/// </summary>
	Count,

	/// <summary>
	/// 平均值。
	/// </summary>
	Average,

	/// <summary>
	/// 最大值。
	/// </summary>
	Max,

	/// <summary>
	/// 最小值。
	/// </summary>
	Min,

	/// <summary>
	/// 去重计数。
	/// </summary>
	DistinctCount
}