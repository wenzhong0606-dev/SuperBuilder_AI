namespace SuperBuilder_AI.Models.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.13.2 Semantic Applicability：Resolved 表示所有 Golden 语义断言均已获得稳定物理绑定。
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
    public IReadOnlyList<SemanticApplicabilityMetricResolution> MetricResolutions { get; init; } = Array.Empty<SemanticApplicabilityMetricResolution>();
    public IReadOnlyList<SemanticApplicabilityFilterResolution> FilterResolutions { get; init; } = Array.Empty<SemanticApplicabilityFilterResolution>();
    public IReadOnlyList<SemanticApplicabilityDimensionResolution> DimensionResolutions { get; init; } = Array.Empty<SemanticApplicabilityDimensionResolution>();
    public IReadOnlyList<SemanticApplicabilityTableResolution> TableResolutions { get; init; } = Array.Empty<SemanticApplicabilityTableResolution>();
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

public sealed class SemanticApplicabilityMetricResolution : SemanticApplicabilityResolution { public string SemanticText { get; init; } = string.Empty; }
public sealed class SemanticApplicabilityFilterResolution : SemanticApplicabilityMetricResolution { }
public sealed class SemanticApplicabilityDimensionResolution : SemanticApplicabilityMetricResolution { }
public sealed class SemanticApplicabilityTableResolution
{
    public long TableId { get; init; }
    public long DataSourceId { get; init; }
    public string SemanticText { get; init; } = string.Empty;
    public string? Table { get; init; }
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
