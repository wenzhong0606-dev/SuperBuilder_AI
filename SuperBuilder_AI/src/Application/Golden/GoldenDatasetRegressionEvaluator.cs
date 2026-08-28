using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.5.3 Golden Dataset Regression Scorecard。
/// 不执行 QueryPlan，只评价 GoldenDatasetRunner 的批量结果是否达到回归发布门槛。
///
/// 注意：Golden Case 的 Passed 是 Runtime 原始执行结果，不等价于 Regression 成功。
/// Positive 需要原始执行通过；Negative 需要被正确拦截/拒绝；Ambiguous 与 Unresolved
/// 需要得到预期的 Applicability 状态。因此 OverallPassRate 必须基于
/// ExpectedOutcomeSatisfied，而不能简单统计 GoldenCaseRunResult.Passed。
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

        var positivePassed = positive.Count(x => x.Passed);
        var negativeDetected = negative.Count(IsNegativeDetected);
        var ambiguousDetected = ambiguous.Count(IsAmbiguousDetected);
        var unresolvedDetected = unresolved.Count(IsUnresolvedDetected);

        // Regression 的 OverallPassRate 表示“是否满足 Golden Expected Outcome”，
        // 而不是“Runtime 是否原始返回 PASS”。这避免 Negative Case 被正确拒绝后
        // 反而降低 OverallPassRate 的逻辑错误。
        var expectedOutcomeSatisfiedCount =
            positivePassed + negativeDetected + ambiguousDetected + unresolvedDetected;
        var overallPassRate = Rate(expectedOutcomeSatisfiedCount, executed.Count);

        var positivePassRate = Rate(positivePassed, positive.Count);
        var negativeDetectionRate = Rate(negativeDetected, negative.Count);
        var ambiguousDetectionRate = Rate(ambiguousDetected, ambiguous.Count);
        var unresolvedDetectionRate = Rate(unresolvedDetected, unresolved.Count);

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

        var passed = failedGates.Count == 0 && executed.Count > 0;

        return new GoldenDatasetRegressionScorecard
        {
            Passed = passed,
            Total = result.Total,
            Executed = executed.Count,
            // PassedCases 表示满足 Golden Expected Outcome 的 Case 数量。
            // 因此 Negative / Ambiguous / Unresolved 正确检测也计入 PassedCases。
            PassedCases = expectedOutcomeSatisfiedCount,
            FailedCases = executed.Count - expectedOutcomeSatisfiedCount,
            OverallPassRate = overallPassRate,
            PositivePassRate = positivePassRate,
            NegativeDetectionRate = negativeDetectionRate,
            AmbiguousDetectionRate = ambiguousDetectionRate,
            UnresolvedDetectionRate = unresolvedDetectionRate,
            HasUnexpectedApplicabilityState = unexpectedState,
            Decision = passed ? "PASS" : "BLOCK",
            FailedGates = failedGates
        };
    }

    private static bool IsNegativeDetected(GoldenCaseRunResult item) =>
        !item.Passed && item.Decision is "FAIL" or "ERROR" or "BLOCK" or "REVIEW";

    private static bool IsAmbiguousDetected(GoldenCaseRunResult item) =>
        item.Passed && string.Equals(item.ApplicabilityState, "Ambiguous", StringComparison.OrdinalIgnoreCase);

    private static bool IsUnresolvedDetected(GoldenCaseRunResult item) =>
        item.Passed && string.Equals(item.ApplicabilityState, "NotResolved", StringComparison.OrdinalIgnoreCase);

    private static double Rate(int numerator, int denominator) => denominator == 0 ? 1.0 : (double)numerator / denominator;

    private static void AddGateFailure(List<string> failures, string name, double actual, double minimum)
    {
        if (actual < minimum)
            failures.Add($"{name}={actual:P2} < minimum={minimum:P2}");
    }
}
