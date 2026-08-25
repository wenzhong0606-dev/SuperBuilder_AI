namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// Phase 2.6 C.13.2 / Phase 2.7：Semantic Applicability 到 QueryPlan 的统一绑定契约。
/// 每一个 Golden Metric / Dimension 都必须对应稳定 Resolution；下游不得重新进行语义猜测。
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

    /// <summary>
    /// 当前 Metadata Snapshot 下的 Dimension 执行路径。
    /// </summary>
    public DimensionResolutionType ResolutionType { get; init; } = DimensionResolutionType.NotResolved;

    /// <summary>
    /// Resolution 是否已经形成可执行绑定。
    /// </summary>
    public DimensionExecutionCapability ExecutionCapability { get; init; } = DimensionExecutionCapability.NotExecutable;

    /// <summary>
    /// DirectKey 模式下事实表中的稳定 Key。
    /// </summary>
    public long? DimensionKeyColumnId { get; init; }
    public string? DimensionKeyColumn { get; init; }

    /// <summary>
    /// DirectKey 模式下可直接展示 / GROUP BY 的 Label，可为空。
    /// </summary>
    public long? DimensionLabelColumnId { get; init; }
    public string? DimensionLabelColumn { get; init; }
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