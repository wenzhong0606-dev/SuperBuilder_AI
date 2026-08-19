using SuperBulider_AI.Models.BI;

namespace SuperBulider_AI.Models.BI.Evaluation;

/// <summary>
/// Golden Metric 的语义期望。
/// </summary>
public class GoldenMetricExpectation
{
    /// <summary>
    /// MetadataColumn.BusinessKey。
    /// </summary>
    public string BusinessKey { get; set; } = string.Empty;

    /// <summary>
    /// 期望聚合方式。
    /// </summary>
    public QueryAggregation Aggregation { get; set; } = QueryAggregation.None;
}
