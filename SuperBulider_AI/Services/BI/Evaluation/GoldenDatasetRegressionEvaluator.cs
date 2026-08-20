using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.5.3 Golden Dataset Regression Scorecard。
/// 不执行 QueryPlan，只评价 GoldenDatasetRunner 的批量结果是否达到回归发布门槛。
/// </summary>
public sealed class GoldenDatasetRegressionEvaluator
{
    public GoldenDatasetRegressionScorecard Evaluate(
        GoldenDatasetRunResult result,
        GoldenDatasetRegressionPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        policy ??= new GoldenDatasetRegressionPolicy();

        var executed = result.Cases.Where(x => x.Enabled).ToList();
        var positive = executed.Where(x => x.Category == "positive").ToList();
        var negative = executed.Where(x => x.Category == "negative").ToList();
        var ambiguous = executed.Where(x => x.Category == "ambiguous").ToList();
        var unresolved = executed.Where(x => x.Category == "unresolved").ToList();

        var overallPassRate = Rate(executed.Count(x => x.Passed), executed.Count);
        var positivePassRate = Rate(positive.Count(x => x.Passed), positive.Count);
        var negativeDetectionRate = Rate(negative.Count(IsNegativeDetected), negative.Count);
        var ambiguousDetectionRate = Rate(ambiguous.Count(x => x.Passed && x.ApplicabilityState == "Ambiguous"), ambiguous.Count);
        var unresolvedDetectionRate = Rate(unresolved.Count(x => x.Passed && x.ApplicabilityState == "NotResolved"), unresolved.Count);
        var unexpectedState = executed.Any(x =>
            x.Category is "ambiguous" or "unresolved"
                ? x.ApplicabilityState is not null &&
                  ((x.Category == "ambiguous" && x.ApplicabilityState != "Ambiguous") ||
                   (x.Category == "unresolved" && x.ApplicabilityState != "NotResolved"))
                : false);

        var failedGates = new List<string>();
        AddGateFailure(failedGates, "OverallPassRate", overallPassRate, policy.MinimumOverallPassRate);
        AddGateFailure(failedGates, "PositivePassRate", positivePassRate, policy.MinimumPositivePassRate);
        AddGateFailure(failedGates, "NegativeDetectionRate", negativeDetectionRate, policy.MinimumNegativeDetectionRate);
        AddGateFailure(failedGates, "AmbiguousDetectionRate", ambiguousDetectionRate, policy.MinimumAmbiguousDetectionRate);
        AddGateFailure(failedGates, "UnresolvedDetectionRate", unresolvedDetectionRate, policy.MinimumUnresolvedDetectionRate);
        if (policy.BlockOnUnexpectedApplicabilityState && unexpectedState)
            failedGates.Add("UnexpectedApplicabilityState");

        return new GoldenDatasetRegressionScorecard
        {
            Passed = failedGates.Count == 0 && executed.Count > 0,
            Total = result.Total,
            Executed = executed.Count,
            PassedCases = executed.Count(x => x.Passed),
            FailedCases = executed.Count(x => !x.Passed),
            OverallPassRate = overallPassRate,
            PositivePassRate = positivePassRate,
            NegativeDetectionRate = negativeDetectionRate,
            AmbiguousDetectionRate = ambiguousDetectionRate,
            UnresolvedDetectionRate = unresolvedDetectionRate,
            HasUnexpectedApplicabilityState = unexpectedState,
            Decision = failedGates.Count == 0 && executed.Count > 0 ? "PASS" : "BLOCK",
            FailedGates = failedGates
        };
    }

    private static bool IsNegativeDetected(GoldenCaseRunResult item) =>
        !item.Passed && item.Decision is "FAIL" or "ERROR" or "BLOCK" or "REVIEW";

    private static double Rate(int numerator, int denominator) => denominator == 0 ? 1.0 : (double)numerator / denominator;

    private static void AddGateFailure(List<string> failures, string name, double actual, double minimum)
    {
        if (actual < minimum)
            failures.Add($"{name}={actual:P2} < minimum={minimum:P2}");
    }
}
