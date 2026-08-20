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
    public string? ConfidenceDecision { get; init; }
    public string? ConfidenceLevel { get; init; }
    public double? ConfidenceScore { get; init; }

    /// <summary>
    /// Golden Runtime 的 Validation 诊断。
    /// 直接暴露 Pipeline 最终 ValidationResult，避免只返回“存在 N 个 Validation Error”而丢失根因。
    /// </summary>
    public GoldenValidationDiagnostics? ValidationDiagnostics { get; init; }
}

/// <summary>
/// Golden Runtime Validation / Repair 诊断信息。
/// 仅用于可观测性，不参与 Golden Case 判定。
/// </summary>
public sealed class GoldenValidationDiagnostics
{
    public bool ValidationPassed { get; init; }
    public int ErrorCount { get; init; }
    public int WarningCount { get; init; }
    public List<SemanticValidationError> Errors { get; init; } = new();
    public List<SemanticValidationError> Warnings { get; init; } = new();
    public QueryPlanRepairTrace? RepairTrace { get; init; }
    public string? RepairStatus { get; init; }
    public int RepairAttempts { get; init; }
    public int ChangedPlanCount { get; init; }
    public string? RepairStopReason { get; init; }
}