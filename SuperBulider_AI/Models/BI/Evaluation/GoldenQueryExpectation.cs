namespace SuperBulider_AI.Models.BI.Evaluation;

/// <summary>
/// Golden Query Case 的数据库无关期望查询语义。
///
/// 对可选标量属性：null 表示不参与本 Case 的评价。
/// 对可选集合：null 表示不参与评价；空集合表示必须为空；非空集合表示必须匹配。
/// DataSourceId 属于运行时 Evaluation Context，不属于 Universal Golden Ground Truth。
/// </summary>
public class GoldenQueryExpectation
{
    public string? IntentType { get; set; }

    public List<GoldenMetricExpectation>? Metrics { get; set; }

    public List<GoldenDimensionExpectation>? Dimensions { get; set; }

    public List<GoldenFilterExpectation>? Filters { get; set; }

    public List<GoldenTableExpectation>? Tables { get; set; }

    public List<GoldenJoinExpectation>? Joins { get; set; }

    public List<GoldenOrderExpectation>? Orders { get; set; }

    public bool? IsAggregate { get; set; }

    public bool? Distinct { get; set; }

    public int? Limit { get; set; }

    public bool? IsRanking { get; set; }

    public bool? IsDetailRanking { get; set; }

    public bool? IsAggregateRanking { get; set; }
}
