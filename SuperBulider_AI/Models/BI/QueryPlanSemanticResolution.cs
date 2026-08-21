namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// Phase 2.6 C.13：Semantic Applicability 到 QueryPlan 的稳定绑定契约。
/// 每一个 Metric 都必须拥有独立的物理绑定，避免多指标场景再次发生二次语义猜测。
/// </summary>
public sealed class QueryPlanSemanticResolution
{
    /// <summary>
    /// 所有已解析 Metric 的稳定物理绑定，顺序与 QueryIntent.Metrics 保持一致。
    /// </summary>
    public IReadOnlyList<QueryPlanMetricResolution> Metrics { get; init; }
        = Array.Empty<QueryPlanMetricResolution>();

    /// <summary>
    /// 兼容旧调用方：第一个 Metric 的主绑定。
    /// 新代码应优先使用 Metrics。
    /// </summary>
    public QueryPlanMetricResolution? Metric { get; init; }

    public IReadOnlyList<QueryPlanFilterResolution> Filters { get; init; }
        = Array.Empty<QueryPlanFilterResolution>();

    public IReadOnlyList<QueryPlanDimensionResolution> Dimensions { get; init; }
        = Array.Empty<QueryPlanDimensionResolution>();

    public IReadOnlyList<QueryPlanOrderResolution> Orders { get; init; }
        = Array.Empty<QueryPlanOrderResolution>();
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
