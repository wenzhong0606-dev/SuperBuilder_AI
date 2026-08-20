namespace SuperBuilder_AI.Models.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.5.2 / C.6.3 Golden Dataset 批量回归执行结果。
/// </summary>
public sealed class GoldenDatasetRunResult
{
    public string Dataset { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public int Total { get; init; }
    public int Executed { get; init; }
    public int Passed { get; init; }
    public int Failed { get; init; }
    public int Blocked { get; init; }
    public int Review { get; init; }
    public int Unresolved { get; init; }
    public int Ambiguous { get; init; }
    public List<GoldenCaseRunResult> Cases { get; init; } = new();
    public List<QueryPlanEvaluationConfidenceResult> ConfidenceResults { get; init; } = new();
    public GoldenConfidenceCalibrationScorecard? ConfidenceCalibration { get; init; }
}

public sealed class GoldenCaseRunResult
{
    public string CaseId { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public string Category { get; init; } = "positive";
    public bool Enabled { get; init; }
    public string Stage { get; init; } = string.Empty;
    public string Decision { get; init; } = string.Empty;
    public bool Passed { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string? ApplicabilityState { get; init; }
    public bool? QueryPlanEvaluationPassed { get; init; }
}
