namespace SuperBulider_AI.Models.BI.Evaluation;

/// <summary>
/// QueryPlan Golden Case。
///
/// 描述 Query Evaluation Framework 的 Ground Truth，
/// 不代表运行时 QueryPlan，也不直接引用 QueryPlan。
/// </summary>
public class GoldenQueryCase
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Question { get; set; } = string.Empty;

    public string? ExpectedIntentType { get; set; }

    public long? ExpectedDataSourceId { get; set; }

    public List<GoldenMetricExpectation> ExpectedMetrics { get; set; } = new();

    public List<GoldenDimensionExpectation> ExpectedDimensions { get; set; } = new();

    public List<GoldenFilterExpectation> ExpectedFilters { get; set; } = new();

    public List<GoldenTableExpectation> ExpectedTables { get; set; } = new();

    public List<GoldenJoinExpectation> ExpectedJoins { get; set; } = new();

    public List<GoldenOrderExpectation> ExpectedOrders { get; set; } = new();

    public bool? ExpectedIsAggregate { get; set; }

    public bool? ExpectedDistinct { get; set; }

    public int? ExpectedLimit { get; set; }

    public bool? ExpectedIsRanking { get; set; }

    public bool? ExpectedIsDetailRanking { get; set; }

    public bool? ExpectedIsAggregateRanking { get; set; }

    public string? Difficulty { get; set; }

    public List<string> Tags { get; set; } = new();

    public string Version { get; set; } = "1.0";

    public bool Enabled { get; set; } = true;
}
