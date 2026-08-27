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
    /// <summary>
    /// GQ-002：Applicability 已判定的 Metric 类型（EntityCount / ColumnMetric）。
    /// EntityCount 表示 Golden 契约期望 COUNT(PK)，Runtime 必须消费该类型，
    /// 不得沿用 LLM Intent 的 SUM 等错误聚合。
    /// </summary>
    public string MetricType { get; init; } = "ColumnMetric";
}

public sealed class SemanticApplicabilityFilterResolution : SemanticApplicabilityMetricResolution { }

public sealed class SemanticApplicabilityDimensionResolution : SemanticApplicabilityMetricResolution
{
    public string ResolutionType { get; init; } = "NotResolved";
    public string ExecutionCapability { get; init; } = "NotExecutable";
    public long? DimensionKeyColumnId { get; init; }
    public string? DimensionKeyColumn { get; init; }
    public long? DimensionLabelColumnId { get; init; }
    public string? DimensionLabelColumn { get; init; }

    // Master-side physical binding returned by DimensionResolutionEvidenceService.
    public long? MasterTableId { get; init; }
    public long? MasterDataSourceId { get; init; }
    public string? MasterTable { get; init; }
    public long? MasterKeyColumnId { get; init; }
    public string? MasterKeyColumn { get; init; }
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
