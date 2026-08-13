using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Text.Json.Serialization;


namespace SuperBulider_AI.Models.BI;

/// <summary>
/// 用户查询意图模型。
///
/// AI理解用户问题后的结构化结果。
///
/// 后续流程:
///
/// User Question
///       ↓
/// QueryIntent
///       ↓
/// QueryPlan
///       ↓
/// SQL
///
/// </summary>
public class QueryIntent
{
	/// <summary>
	/// 用户原始问题
	/// </summary>
	public string OriginalQuestion { get; set; } = string.Empty;


	/// <summary>
	/// 查询类型
	///
	/// Detail:
	/// 明细查询
	///
	/// Aggregate:
	/// 聚合统计
	///
	/// Ranking:
	/// 排名查询
	///
	/// Comparison:
	/// 对比分析
	///
	/// Trend:
	/// 趋势分析
	///
	/// </summary>
	public string IntentType { get; set; } = string.Empty;



	/// <summary>
	/// 查询指标
	/// </summary>
	public List<QueryMetric> Metrics { get; set; }
		= new();



	/// <summary>
	/// 查询过滤条件
	/// </summary>
	public List<QueryFilter> Filters { get; set; }
		= new();



	/// <summary>
	/// 分组字段
	/// </summary>
	public List<string> Dimensions { get; set; }
		= new();



	/// <summary>
	/// 排序字段
	/// </summary>
	public string? OrderBy { get; set; }



	/// <summary>
	/// 排序方向
	/// ASC / DESC
	/// </summary>
	public string? OrderDirection { get; set; }



	/// <summary>
	/// 返回数量限制
	/// </summary>
	public int? Limit { get; set; }



	/// <summary>
	/// AI解释
	/// </summary>
	public string? Explanation { get; set; }
}
