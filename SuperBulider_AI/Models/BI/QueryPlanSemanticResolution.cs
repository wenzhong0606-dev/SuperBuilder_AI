namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// Phase 2.6 C.13.2：Semantic Applicability 到 QueryPlan 的统一绑定契约。
/// 每一个 Golden Metric 都必须对应一个稳定的 Semantic Resolution；
/// 下游不得重新进行语义猜测。
/// </summary>
public sealed class QueryPlanSemanticResolution
{
    public IReadOnlyList<QueryPlanMetricResolution> Metrics { get; init; } = Array.Empty<QueryPlanMetricResolution>();
    public QueryPlanMetricResolution? Metric => Metrics.FirstOrDefault();
    public IReadOnlyList<QueryPlanFilterResolution> Filters { get; init; } = Array.Empty<QueryPlanFilterResolution>();
    public IReadOnlyList<QueryPlanDimensionResolution> Dimensions { get; init; } = Array.Empty<QueryPlanDimensionResolution>();
    public IReadOnlyList<QueryPlanTableResolution> Tables { get; init; } = Array.Empty<QueryPlanTableResolution>();
    public IReadOnlyList<QueryPlanOrderResolution> Orders { get; init; } = Array.Empty<QueryPlanOrderResolution>();
}

public sealed class QueryPlanMetricResolution
{
    public long TableId { get; init; }
    public long DataSourceId { get; init; }
    public long ColumnId { get; init; }
    public string SemanticText { get; init; } = string.Empty;
    public string Table { get; init; } = string.Empty;
    public string Column { get; init; } = string.Empty;
    public string? BusinessMeaning { get; init; }
    public double? Score { get; init; }
}

public sealed class QueryPlanFilterResolution
{
    public long TableId { get; init; }
    public long DataSourceId { get; init; }
    public long ColumnId { get; init; }
    public string SemanticText { get; init; } = string.Empty;
    public string Table { get; init; } = string.Empty;
    public string Column { get; init; } = string.Empty;
    public string? BusinessMeaning { get; init; }
    public double? Score { get; init; }
}

public sealed class QueryPlanDimensionResolution
{
    public long TableId { get; init; }
    public long DataSourceId { get; init; }
    public long ColumnId { get; init; }
    public string SemanticText { get; init; } = string.Empty;
    public string Table { get; init; } = string.Empty;
    public string Column { get; init; } = string.Empty;
    public string? BusinessMeaning { get; init; }
    public double? Score { get; init; }
}

public sealed class QueryPlanTableResolution
{
    public long TableId { get; init; }
    public long DataSourceId { get; init; }
    public string SemanticText { get; init; } = string.Empty;
    public string Table { get; init; } = string.Empty;
    public string? BusinessMeaning { get; init; }
    public double? Score { get; init; }
}

public sealed class QueryPlanOrderResolution
{
    public long TableId { get; init; }
    public long DataSourceId { get; init; }
    public long ColumnId { get; init; }
    public string SemanticText { get; init; } = string.Empty;
    public string Table { get; init; } = string.Empty;
    public string Column { get; init; } = string.Empty;
    public string? BusinessMeaning { get; init; }
    public double? Score { get; init; }
}