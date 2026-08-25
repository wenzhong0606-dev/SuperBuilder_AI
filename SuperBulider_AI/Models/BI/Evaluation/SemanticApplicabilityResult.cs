namespace SuperBuilder_AI.Models.BI.Evaluation;

public sealed record SemanticApplicabilityResult
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

public class SemanticApplicabilityResolution
{
    public long TableId { get; init; }
    public long DataSourceId { get; init; }
    public long ColumnId { get; init; }
    public string? Table { get; init; }
    public string? Column { get; init; }
    public string? BusinessMeaning { get; init; }
    public double? Score { get; init; }
}

public class SemanticApplicabilityMetricResolution : SemanticApplicabilityResolution
{
    public string SemanticText { get; init; } = string.Empty;
}

public sealed class SemanticApplicabilityFilterResolution : SemanticApplicabilityMetricResolution { }

public sealed class SemanticApplicabilityDimensionResolution : SemanticApplicabilityMetricResolution
{
    /// <summary>Resolution strategy: MasterJoin / DirectKey / Ambiguous / NotResolved.</summary>
    public string ResolutionType { get; init; } = "NotResolved";

    /// <summary>Whether the evidence is executable by the QueryPlan layer.</summary>
    public string ExecutionCapability { get; init; } = "NotExecutable";

    /// <summary>Fact-side dimension key selected by the evidence resolver.</summary>
    public long? DimensionKeyColumnId { get; init; }
    public string? DimensionKeyColumn { get; init; }

    /// <summary>Optional master-side display/label column for MasterJoin.</summary>
    public long? DimensionLabelColumnId { get; init; }
    public string? DimensionLabelColumn { get; init; }
}

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
