using System.Text.Json.Serialization;

namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// 用户查询意图。
///
/// V2.0。
///
/// QueryIntent 表示 AI 对自然语言的理解结果。
///
/// 注意：
/// QueryIntent 不是最终执行计划。
/// </summary>
public class QueryIntent
{
	/// <summary>
	/// 用户原始问题。
	/// </summary>
	public string OriginalQuestion { get; set; } = string.Empty;

	/// <summary>
	/// 查询类型。
	///
	/// Detail
	/// Aggregate
	/// Ranking
	/// Comparison
	/// Trend
	/// </summary>
	public string IntentType { get; set; } = string.Empty;

	/// <summary>
	/// 查询指标。
	/// </summary>
	public List<QueryMetric> Metrics { get; set; } = new();

	/// <summary>
	/// 查询过滤条件。
	/// </summary>
	public List<QueryFilter> Filters { get; set; } = new();

	/// <summary>
	/// 查询维度。
	///
	/// V2.0 暂时保持 string，
	/// 后续 Semantic Layer 稳定后再迁移成 QueryDimension。
	///
	/// 使用 FlexibleStringListConverter 兼容 LLM 返回
	/// 字符串数组 ["物料"] 或对象数组 [{"semanticText":"物料"}]。
	/// </summary>
	[JsonConverter(typeof(FlexibleStringListConverter))]
	public List<string> Dimensions { get; set; } = new();

	/// <summary>
	/// AI识别的排序字段。
	///
	/// Legacy。
	///
	/// 最终执行时应该转换成 QueryPlan.Orders。
	/// </summary>
	public string? OrderBy { get; set; }

	/// <summary>
	/// AI识别的排序方向。
	///
	/// ASC / DESC
	///
	/// Legacy。
	/// </summary>
	public string? OrderDirection { get; set; }

	/// <summary>
	/// 返回数量。
	///
	/// Legacy。
	///
	/// 最终执行时应该转换成 QueryPlan.Limit。
	/// </summary>
	public int? Limit { get; set; }

	/// <summary>
	/// AI解释。
	/// </summary>
	public string? Explanation { get; set; }

	/// <summary>
	/// P3 业务实体解析提示：AI 理解阶段识别出的候选业务实体及其置信度。
	/// </summary>
	public List<BusinessEntityHint> BusinessEntityHints { get; set; } = new();

	/// <summary>
	/// 是否属于 Ranking 查询。
	/// </summary>
	public bool IsRanking =>
		string.Equals(
			IntentType,
			"Ranking",
			StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// 是否属于 Detail 查询。
	/// </summary>
	public bool IsDetail =>
		string.Equals(
			IntentType,
			"Detail",
			StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// 是否属于 Aggregate 查询。
	/// </summary>
	public bool IsAggregate =>
		string.Equals(
			IntentType,
			"Aggregate",
			StringComparison.OrdinalIgnoreCase);
}
