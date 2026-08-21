namespace SuperBuilder_AI.Models.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.4.8 / C.13.2 QueryPlan Semantic Resolution Evidence。
/// 将 Semantic Applicability 的解析结果与 Runtime QueryPlan 的物理绑定进行独立评价。
/// Structural Evaluation 与 Semantic Evidence 保持职责分离。
/// </summary>
public sealed class QueryPlanSemanticEvidenceResult
{
    public string CaseId { get; init; } = string.Empty;
    public bool Passed { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string GoldenSemanticText { get; init; } = string.Empty;
    public string ApplicabilityState { get; init; } = string.Empty;

    // 兼容既有 C.4.8 单 Metric Evidence 字段；新实现同时提供集合式 Evidence。
    public bool ResolutionExists { get; init; }
    public bool RuntimeMetricExists { get; init; }
    public bool MetricFieldMatchesResolution { get; init; }
    public bool TableBindingMatchesResolution { get; init; }
    public bool DataSourceBindingMatchesResolution { get; init; }
    public double? ResolutionScore { get; init; }

    /// <summary>
    /// Metric Semantic Resolution → Runtime Metric Binding Evidence。
    /// </summary>
    public IReadOnlyList<QueryPlanSemanticBindingEvidence> Metrics { get; init; }
        = Array.Empty<QueryPlanSemanticBindingEvidence>();

    /// <summary>
    /// Dimension Semantic Resolution → Runtime Dimension Binding Evidence。
    /// </summary>
    public IReadOnlyList<QueryPlanSemanticBindingEvidence> Dimensions { get; init; }
        = Array.Empty<QueryPlanSemanticBindingEvidence>();

    /// <summary>
    /// Filter Semantic Resolution → Runtime Filter Binding Evidence。
    /// </summary>
    public IReadOnlyList<QueryPlanSemanticBindingEvidence> Filters { get; init; }
        = Array.Empty<QueryPlanSemanticBindingEvidence>();

    /// <summary>
    /// Resolution → Runtime Table / DataSource Binding Evidence。
    /// Table Evidence 来源于当前 Applicability 中可用的物理 Resolution。
    /// </summary>
    public IReadOnlyList<QueryPlanSemanticBindingEvidence> Tables { get; init; }
        = Array.Empty<QueryPlanSemanticBindingEvidence>();
}

/// <summary>
/// 统一 Semantic Binding Evidence Snapshot。
/// 不持有 Planning Model / Resolution 对象本身，避免 Evaluation Model 反向耦合规划层。
/// </summary>
public sealed class QueryPlanSemanticBindingEvidence
{
    public string SemanticText { get; init; } = string.Empty;

    public bool ResolutionExists { get; init; }
    public long ResolvedColumnId { get; init; }
    public string? ResolvedColumn { get; init; }
    public long ResolvedTableId { get; init; }
    public string? ResolvedTable { get; init; }
    public long ResolvedDataSourceId { get; init; }

    public long? RuntimeColumnId { get; init; }
    public string? RuntimeColumn { get; init; }
    public long? RuntimeTableId { get; init; }
    public long? RuntimeDataSourceId { get; init; }

    public bool BindingMatched { get; init; }
    public double? ResolutionScore { get; init; }
    public string Reason { get; init; } = string.Empty;
    public bool Passed { get; init; }
}
