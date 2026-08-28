namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// BI 查询指标。
/// SemanticText 保存已经解析确认的 Golden 业务语义；Field 保存物理字段。
/// 二者必须保持职责分离。
/// </summary>
public class QueryMetric
{
    public string Name { get; set; } = string.Empty;
    public string SemanticText { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string Aggregation { get; set; } = "NONE";
    public string? Alias { get; set; }
    public string? SemanticType { get; set; }
    public bool IsOrderingMetric { get; set; }

    public QueryAggregation GetAggregation()
    {
        return Aggregation?.Trim().ToUpperInvariant() switch
        {
            "SUM" => QueryAggregation.Sum,
            "COUNT" => QueryAggregation.Count,
            "AVG" => QueryAggregation.Average,
            "AVERAGE" => QueryAggregation.Average,
            "MAX" => QueryAggregation.Max,
            "MIN" => QueryAggregation.Min,
            "DISTINCTCOUNT" => QueryAggregation.DistinctCount,
            "DISTINCT_COUNT" => QueryAggregation.DistinctCount,
            _ => QueryAggregation.None
        };
    }
}
