namespace SuperBuilder_AI.Models.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.5.3 Golden Dataset Regression Policy。
/// 用于定义 QueryPlan 回归是否达到发布门槛。
/// </summary>
public sealed class GoldenDatasetRegressionPolicy
{
    public double MinimumOverallPassRate { get; init; } = 0.90;
    public double MinimumPositivePassRate { get; init; } = 0.95;
    public double MinimumNegativeDetectionRate { get; init; } = 0.90;
    public double MinimumAmbiguousDetectionRate { get; init; } = 0.90;
    public double MinimumUnresolvedDetectionRate { get; init; } = 0.90;
    public bool BlockOnUnexpectedApplicabilityState { get; init; } = true;
}

public sealed class GoldenDatasetRegressionScorecard
{
    public bool Passed { get; init; }
    public int Total { get; init; }
    public int Executed { get; init; }
    public int PassedCases { get; init; }
    public int FailedCases { get; init; }
    public double OverallPassRate { get; init; }
    public double PositivePassRate { get; init; }
    public double NegativeDetectionRate { get; init; }
    public double AmbiguousDetectionRate { get; init; }
    public double UnresolvedDetectionRate { get; init; }
    public bool HasUnexpectedApplicabilityState { get; init; }
    public string Decision { get; init; } = string.Empty;
    public List<string> FailedGates { get; init; } = new();
}
