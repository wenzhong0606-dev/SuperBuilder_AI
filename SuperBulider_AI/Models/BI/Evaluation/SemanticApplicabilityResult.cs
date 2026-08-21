namespace SuperBuilder_AI.Models.BI.Evaluation;

/// <summary>
/// Phase 2.6.3.5-C.2 Semantic Applicability Evaluation Result。
/// 描述当前 Golden Case 在运行时 Metadata + Semantic Search 环境中的语义适用性。
/// State=Resolved 时，MetricResolutions 是允许 QueryPlanBuilder 消费的稳定物理绑定。
/// </summary>
public sealed class SemanticApplicabilityResult
{
    public string CaseId { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public string MetricSemanticText { get; init; } = string.Empty;
    public string MetricType { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public SemanticApplicabilityCandidate? SearchCandidate { get; init; }
    public SemanticApplicabilityResolution? Resolution { get; init; }

    /// <summary>
    /// 所有 Golden Metrics 的稳定物理绑定，顺序与 Golden Expected.Metrics 保持一致。
    /// </summary>
    public IReadOnlyList<SemanticApplicabilityMetricResolution> MetricResolutions { get; init; }
        = Array.Empty<SemanticApplicabilityMetricResolution>();

    public IReadOnlyList<SemanticApplicabilityFilterResolution> FilterResolutions { get; init; }
        = Array.Empty<SemanticApplicabilityFilterResolution>();

    public IReadOnlyList<SemanticApplicabilityDimensionResolution> DimensionResolutions { get; init; }
        = Array.Empty<SemanticApplicabilityDimensionResolution>();

    public SemanticApplicabilityEvidence Evidence { get; init; } = new();
}

public sealed class SemanticApplicabilityResolution
{
    public long TableId { get; init; }
    public long DataSourceId { get; init; }
    public long ColumnId { get; init; }
    public string? Table { get; init; }
    public string? Column { get; init; }
    public string? BusinessMeaning { get; init; }
    public double? Score { get; init; }
}

public sealed class SemanticApplicabilityMetricResolution
{
    public long TableId { get; init; }
    public long DataSourceId { get; init; }
    public long ColumnId { get; init; }
    public string SemanticText { get; init; } = string.Empty;
    public string? Table { get; init; }
    public string? Column { get; init; }
    public string? BusinessMeaning { get; init; }
    public double? Score { get; init; }
}

public sealed class SemanticApplicabilityFilterResolution
{
    public long TableId { get; init; }
    public long DataSourceId { get; init; }
    public long ColumnId { get; init; }
    public string SemanticText { get; init; } = string.Empty;
    public string? Table { get; init; }
    public string? Column { get; init; }
    public string? BusinessMeaning { get; init; }
    public double? Score { get; init; }
}

public sealed class SemanticApplicabilityDimensionResolution
{
    public long TableId { get; init; }
    public long DataSourceId { get; init; }
    public long ColumnId { get; init; }
    public string SemanticText { get; init; } = string.Empty;
    public string? Table { get; init; }
    public string? Column { get; init; }
    public string? BusinessMeaning { get; init; }
    public double? Score { get; init; }
}

public sealed class SemanticApplicabilityCandidate
{
    public string? VectorType { get; init; }
    public string? VectorId { get; init; }
    public double Score { get; init; }
    public string? Table { get; init; }
    public string? Column { get; init; }
    public string? BusinessMeaning { get; init; }
}

public sealed class SemanticApplicabilityEvidence
{
    public bool SemanticCandidateExists { get; init; }
    public bool EntityCandidateExists { get; init; }
    public bool DirectEntityCountEvidence { get; init; }
    public bool LexicalMatch { get; init; }
    public bool CompetingCandidates { get; init; }
    public double? TopScore { get; init; }
    public double? SecondScore { get; init; }
    public double? ScoreGap { get; init; }
}
