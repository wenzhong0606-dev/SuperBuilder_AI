using SuperBulider_AI.Models.BI;

namespace SuperBulider_AI.Models.BI.Evaluation;

/// <summary>
/// Golden Order 的数据库无关语义期望。
/// </summary>
public class GoldenOrderExpectation
{
    /// <summary>
    /// 非指标排序时使用的业务语义描述。
    /// </summary>
    public string? SemanticText { get; set; }

    /// <summary>
    /// 是否按照指标排序。
    /// </summary>
    public bool IsMetric { get; set; }

    /// <summary>
    /// 指标排序时使用的业务语义描述。
    /// </summary>
    public string? MetricSemanticText { get; set; }

    /// <summary>
    /// 指标排序使用的聚合方式。
    /// </summary>
    public QueryAggregation Aggregation { get; set; } = QueryAggregation.None;

    /// <summary>
    /// ASC 或 DESC。
    /// </summary>
    public string Direction { get; set; } = "ASC";
}
