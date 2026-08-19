namespace SuperBulider_AI.Models.BI.Evaluation;

/// <summary>
/// Phase 2.6.3.5-C.2 Semantic Applicability Evaluation Result。
///
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

    /// <summary>
    /// Semantic Search 返回的最高相关候选，仅作为检索证据。
    /// </summary>
    public SemanticApplicabilityCandidate? SearchCandidate { get; init; }

    /// <summary>
    /// 已通过 Applicability 判断、允许进入 QueryPlan 构造的物理绑定。
    /// 只有 State=Resolved 时才应非空。
    /// </summary>
    public SemanticApplicabilityResolution? Resolution { get; init; }

    public SemanticApplicabilityEvidence Evidence { get; init; } = new();
}

/// <summary>
/// 已解析的语义到物理 Metadata 字段绑定。
/// </summary>
public sealed class SemanticApplicabilityResolution
{
    public string? Table { get; init; }

    public string? Column { get; init; }

    public string? BusinessMeaning { get; init; }

    public double? Score { get; init; }
}

/// <summary>
/// Semantic Search 当前最高相关候选。
/// </summary>
public sealed class SemanticApplicabilityCandidate
{
    public string? VectorType { get; init; }

    public string? VectorId { get; init; }

    public double Score { get; init; }

    public string? Table { get; init; }

    public string? Column { get; init; }

    public string? BusinessMeaning { get; init; }
}

/// <summary>
/// Semantic Applicability 的结构化证据。
/// 第一版不把 Evidence 压缩成单一 Confidence 分数。
/// </summary>
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
