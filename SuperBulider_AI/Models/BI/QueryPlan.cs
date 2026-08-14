namespace SuperBulider_AI.Models.BI;

/// <summary>
/// BI 查询执行计划。
///
/// V2.0。
///
/// QueryIntent:
///     AI理解层
///
/// QueryPlan:
///     确定后的执行层
/// </summary>
public class QueryPlan
{
	/// <summary>
	/// 原始AI意图。
	///
	/// 保留用于诊断和追踪。
	/// </summary>
	public QueryIntent? Intent { get; set; }

	/// <summary>
	/// 数据源Id。
	/// </summary>
	public long DataSourceId { get; set; }

	/// <summary>
	/// 查询表。
	/// </summary>
	public List<QueryTable> Tables { get; set; } = new();

	/// <summary>
	/// 查询字段。
	/// </summary>
	public List<QueryField> Fields { get; set; } = new();

	/// <summary>
	/// 查询指标。
	///
	/// V2.0。
	/// </summary>
	public List<QueryMetric> Metrics { get; set; } = new();

	/// <summary>
	/// 查询维度。
	///
	/// V2.0。
	/// </summary>
	public List<QueryDimension> Dimensions { get; set; } = new();

	/// <summary>
	/// 查询过滤条件。
	/// </summary>
	public List<QueryFilter> Filters { get; set; } = new();

	/// <summary>
	/// 查询排序。
	///
	/// V2.0。
	/// </summary>
	public List<QueryOrder> Orders { get; set; } = new();

	/// <summary>
	/// 查询JOIN。
	/// </summary>
	public List<QueryJoin> Joins { get; set; } = new();

	/// <summary>
	/// 是否需要聚合。
	/// </summary>
	public bool IsAggregate { get; set; }

	/// <summary>
	/// 是否 DISTINCT。
	/// </summary>
	public bool Distinct { get; set; }

	/// <summary>
	/// 返回数量。
	///
	/// V2.0。
	/// </summary>
	public int? Limit { get; set; }

	/// <summary>
	/// 是否属于 Ranking / TopN 查询。
	/// </summary>
	public bool IsRanking { get; set; }

	/// <summary>
	/// 是否属于明细 TopN。
	///
	/// 例如：
	///
	/// 数量最多的十条入库凭证
	///
	/// 这种查询：
	///
	/// Aggregation = NONE
	/// ORDER BY quantity DESC
	/// LIMIT 10
	/// </summary>
	public bool IsDetailRanking { get; set; }

	/// <summary>
	/// 是否属于聚合 Ranking。
	///
	/// 例如：
	///
	/// 数量最多的十个物料
	///
	/// SUM(quantity)
	/// GROUP BY material
	/// ORDER BY SUM(quantity) DESC
	/// LIMIT 10
	/// </summary>
	public bool IsAggregateRanking { get; set; }
}