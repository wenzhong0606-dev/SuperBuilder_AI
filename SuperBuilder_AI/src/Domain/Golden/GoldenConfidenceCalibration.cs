namespace SuperBuilder_AI.Models.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.6.3 Confidence × Golden Regression Calibration。
/// 统计 Confidence Level 与 QueryPlan Evaluation / Decision 的一致性。
/// </summary>
public sealed class GoldenConfidenceCalibrationScorecard
{
    public int Total { get; init; }
    public int Evaluated { get; init; }
    public int EvaluationPassed { get; init; }
    public int EvaluationFailed { get; init; }
    public int DecisionProceed { get; init; }
    public int DecisionConfirm { get; init; }
    public int DecisionReject { get; init; }
    public double EvaluationDecisionAgreementRate { get; init; }
    public double HighConfidencePassRate { get; init; }
    public double MediumConfidencePassRate { get; init; }
    public double LowConfidencePassRate { get; init; }
    public double HighConfidenceProceedRate { get; init; }
    public double MediumConfidenceConfirmRate { get; init; }
    public double LowConfidenceRejectRate { get; init; }
    public List<string> CalibrationWarnings { get; init; } = new();
}

public sealed class GoldenConfidenceCalibrationCase
{
    public string CaseId { get; init; } = string.Empty;
    public string ConfidenceLevel { get; init; } = string.Empty;
    public double ConfidenceScore { get; init; }
    public bool EvaluationPassed { get; init; }
    public string Decision { get; init; } = string.Empty;
    public bool Agreement { get; init; }
}
