using SuperBulider_AI.Models.BI;

namespace SuperBulider_AI.Models.BI.Evaluation;

/// <summary>
/// Golden Metric 的数据库无关语义期望。
/// </summary>
public class GoldenMetricExpectation
{
    /// <summary>
    /// 业务语义描述，不绑定 DataSource、MetadataColumnId 或 BusinessKey。
    /// </summary>
    public string SemanticText { get; set; } = string.Empty;

    /// <summary>
    /// 期望聚合方式。
    /// </summary>
    public QueryAggregation Aggregation { get; set; } = QueryAggregation.None;
}
