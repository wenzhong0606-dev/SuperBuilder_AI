using SuperBulider_AI.Models.BI;

namespace SuperBulider_AI.Models.BI.Evaluation;

/// <summary>
/// Golden Order 的语义期望。
/// </summary>
public class GoldenOrderExpectation
{
    /// <summary>
    /// 非指标排序时使用的 MetadataColumn.BusinessKey。
    /// </summary>
    public string? BusinessKey { get; set; }

    /// <summary>
    /// 是否按照指标排序。
    /// </summary>
    public bool IsMetric { get; set; }

    /// <summary>
    /// 指标字段的 MetadataColumn.BusinessKey。
    /// </summary>
    public string? MetricBusinessKey { get; set; }

    /// <summary>
    /// 指标排序使用的聚合方式。
    /// </summary>
    public QueryAggregation Aggregation { get; set; } = QueryAggregation.None;

    /// <summary>
    /// ASC 或 DESC。
    /// </summary>
    public string Direction { get; set; } = "ASC";
}
