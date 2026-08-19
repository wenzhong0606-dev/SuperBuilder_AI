namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// Phase 2.4：Semantic Applicability 到 QueryPlan 的绑定契约。
/// 这里保存已经完成语义解析、允许进入 QueryPlan 的物理字段绑定。
/// QueryPlanBuilder 不应通过二次语义猜测覆盖已经确认的绑定。
/// </summary>
public sealed class QueryPlanSemanticResolution
{
    public QueryPlanMetricResolution? Metric { get; init; }
    public IReadOnlyList<QueryPlanFilterResolution> Filters { get; init; } = Array.Empty<QueryPlanFilterResolution>();
    public IReadOnlyList<QueryPlanDimensionResolution> Dimensions { get; init; } = Array.Empty<QueryPlanDimensionResolution>();
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
