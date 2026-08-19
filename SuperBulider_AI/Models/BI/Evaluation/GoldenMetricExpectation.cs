using SuperBulider_AI.Models.BI;

namespace SuperBulider_AI.Models.BI.Evaluation;

/// <summary>
/// Golden Metric 的数据库无关语义期望。
/// </summary>
public class GoldenMetricExpectation
{
    /// <summary>
    /// 业务语义描述。
    /// </summary>
    public string SemanticText { get; set; } = string.Empty;

    /// <summary>
    /// Phase 2.4：可选的期望物理字段。
    /// 为 null 时不进行 Field 断言。
    /// </summary>
    public string? Field { get; set; }

    /// <summary>
    /// 期望聚合方式。
    /// </summary>
    public QueryAggregation Aggregation { get; set; } = QueryAggregation.None;
}
