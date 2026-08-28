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
    /// <summary>
    /// GQ-002：由 Semantic Applicability 判定的 Metric 类型（EntityCount / ColumnMetric）。
    /// QueryPlanBuilder.ApplyMetricResolutions 依据该类型强制运行时聚合（EntityCount → COUNT），
    /// 防止 LLM Intent 将"…数量"误生成为 SUM。
    /// </summary>
    public string MetricType { get; init; } = "ColumnMetric";
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
    public string ResolutionType { get; init; } = "NotResolved";
    public string ExecutionCapability { get; init; } = "NotExecutable";
    public long? DimensionKeyColumnId { get; init; }
    public string? DimensionKeyColumn { get; init; }
    public long? DimensionLabelColumnId { get; init; }
    public string? DimensionLabelColumn { get; init; }

    // Master-side physical binding supplied by DimensionResolutionEvidence.
    public long? MasterTableId { get; init; }
    public long? MasterDataSourceId { get; init; }
    public string? MasterTable { get; init; }
    public long? MasterKeyColumnId { get; init; }
    public string? MasterKeyColumn { get; init; }
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
