namespace SuperBulider_AI.Models.BI;

/// <summary>
/// BI 查询指标。
///
/// V2.0。
///
/// Metric 表示“我要计算什么”，
/// 而不是“我要按照什么排序”。
/// </summary>
public class QueryMetric
{
	/// <summary>
	/// 用户描述。
	///
	/// 例如：
	/// 数量
	/// 库存数量
	/// 入库凭证条数
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// AI 识别出来的业务字段。
	///
	/// 例如：
	/// quantity
	/// 入库凭证
	/// id
	/// </summary>
	public string Field { get; set; } = string.Empty;

	/// <summary>
	/// 聚合方式。
	///
	/// NONE
	/// SUM
	/// COUNT
	/// AVG
	/// MAX
	/// MIN
	/// </summary>
	public string Aggregation { get; set; } = "NONE";

	/// <summary>
	/// 展示别名。
	/// </summary>
	public string? Alias { get; set; }

	/// <summary>
	/// 语义类型。
	///
	/// Quantity
	/// Amount
	/// Count
	/// Ratio
	/// Date
	/// </summary>
	public string? SemanticType { get; set; }

	/// <summary>
	/// 是否作为排序指标。
	///
	/// 例如：
	///
	/// 数量最多的十条
	///
	/// quantity 就是排序指标。
	/// </summary>
	public bool IsOrderingMetric { get; set; }

	/// <summary>
	/// 将字符串聚合方式转换为枚举。
	/// </summary>
	public QueryAggregation GetAggregation()
	{
		return Aggregation?.Trim().ToUpperInvariant() switch
		{
			"SUM" => QueryAggregation.Sum,
			"COUNT" => QueryAggregation.Count,
			"AVG" => QueryAggregation.Average,
			"AVERAGE" => QueryAggregation.Average,
			"MAX" => QueryAggregation.Max,
			"MIN" => QueryAggregation.Min,
			"DISTINCTCOUNT" => QueryAggregation.DistinctCount,
			"DISTINCT_COUNT" => QueryAggregation.DistinctCount,
			_ => QueryAggregation.None
		};
	}
}