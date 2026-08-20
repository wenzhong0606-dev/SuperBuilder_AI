namespace SuperBuilder_AI.Models.BI.Evaluation;

/// <summary>
/// Phase 2.6.3.5-C.2 Semantic Applicability Evaluation Result。
/// 描述当前 Golden Case 在运行时 Metadata + Semantic Search 环境中的语义适用性。
/// 当 State=Resolved 时，Resolution 是允许 QueryPlanBuilder 消费的稳定物理绑定。
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
    /// 已解析的 Filter 物理绑定。操作符和值仍由 QueryIntent 保留，
    /// 这里只负责把业务语义稳定绑定到 MetadataColumn。
    /// </summary>
    public IReadOnlyList<SemanticApplicabilityFilterResolution> FilterResolutions { get; init; }
        = Array.Empty<SemanticApplicabilityFilterResolution>();

    /// <summary>
    /// Golden 明确要求多表 Join 时，Dimension 必须有稳定的物理语义绑定。
    /// </summary>
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
