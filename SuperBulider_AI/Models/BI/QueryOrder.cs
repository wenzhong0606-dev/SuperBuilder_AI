namespace SuperBulider_AI.Models.BI;

/// <summary>
/// 查询排序。
///
/// V2.0 中 OrderBy 不再放在 QueryIntent 中作为最终执行信息。
/// </summary>
public class QueryOrder
{
	/// <summary>
	/// MetadataColumn Id。
	/// </summary>
	public long MetadataColumnId { get; set; }

	/// <summary>
	/// 实际字段名称。
	/// </summary>
	public string Field { get; set; } = string.Empty;

	/// <summary>
	/// 排序方向。
	///
	/// ASC
	/// DESC
	/// </summary>
	public string Direction { get; set; } = "ASC";

	/// <summary>
	/// 是否按照指标排序。
	///
	/// 例如：
	///
	/// 数量最多
	///
	/// IsMetric = true
	/// </summary>
	public bool IsMetric { get; set; }

	/// <summary>
	/// 对应的指标名称。
	/// </summary>
	public string? MetricName { get; set; }

	/// <summary>
	/// 排序时使用的聚合。
	///
	/// 例如：
	///
	/// SUM(quantity)
	/// COUNT(id)
	/// </summary>
	public QueryAggregation Aggregation { get; set; }
		= QueryAggregation.None;
}