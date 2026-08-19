namespace SuperBulider_AI.Models.BI.Evaluation;

/// <summary>
/// Golden Query Case 的期望查询语义。
/// 未提供的集合/属性表示该维度不参与本 Case 的评价；
/// 显式提供空集合则表示该维度必须为空。
/// </summary>
public class GoldenQueryExpectation
{
    public string? IntentType { get; set; }

    public long? DataSourceId { get; set; }

    public List<GoldenMetricExpectation> Metrics { get; set; } = new();

    public List<GoldenDimensionExpectation> Dimensions { get; set; } = new();

    public List<GoldenFilterExpectation> Filters { get; set; } = new();

    public List<GoldenTableExpectation> Tables { get; set; } = new();

    public List<GoldenJoinExpectation> Joins { get; set; } = new();

    public List<GoldenOrderExpectation> Orders { get; set; } = new();

    public bool? IsAggregate { get; set; }

    public bool? Distinct { get; set; }

    public int? Limit { get; set; }

    public bool? IsRanking { get; set; }

    public bool? IsDetailRanking { get; set; }

    public bool? IsAggregateRanking { get; set; }
}
