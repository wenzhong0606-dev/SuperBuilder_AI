using SuperBuilder_AI.Models.BI.Evaluation;
using SuperBuilder_AI.Services.BI.Evaluation;

namespace SuperBulider_AI.Tests;

public sealed class GoldenDatasetRegressionEvaluatorTests
{
    [Fact]
    public void PassingScorecard_ReturnsPass()
    {
        var result = new GoldenDatasetRunResult
        {
            Total = 4,
            Cases =
            [
                Case("G1", "positive", true, "PASS"),
                Case("G2", "positive", true, "PASS"),
                Case("G3", "negative", false, "BLOCK"),
                Case("G4", "unresolved", true, "PASS", "NotResolved")
            ]
        };

        var policy = new GoldenDatasetRegressionPolicy
        {
            MinimumOverallPassRate = 0.75,
            MinimumPositivePassRate = 1.0,
            MinimumNegativeDetectionRate = 1.0,
            MinimumUnresolvedDetectionRate = 1.0,
            MinimumAmbiguousDetectionRate = 1.0
        };

        var scorecard = new GoldenDatasetRegressionEvaluator().Evaluate(result, policy);

        Assert.True(scorecard.Passed);
        Assert.Equal("PASS", scorecard.Decision);
        Assert.Empty(scorecard.FailedGates);
    }

    [Fact]
    public void PositiveRegression_Blocks()
    {
        var result = new GoldenDatasetRunResult
        {
            Total = 2,
            Cases =
            [
                Case("G1", "positive", true, "PASS"),
                Case("G2", "positive", false, "FAIL")
            ]
        };

        var scorecard = new GoldenDatasetRegressionEvaluator().Evaluate(result,
            new GoldenDatasetRegressionPolicy { MinimumOverallPassRate = 1.0, MinimumPositivePassRate = 1.0 });

        Assert.False(scorecard.Passed);
        Assert.Equal("BLOCK", scorecard.Decision);
        Assert.Contains(scorecard.FailedGates, x => x.StartsWith("OverallPassRate", StringComparison.Ordinal));
        Assert.Contains(scorecard.FailedGates, x => x.StartsWith("PositivePassRate", StringComparison.Ordinal));
    }

    [Fact]
    public void UnexpectedApplicabilityState_Blocks()
    {
        var result = new GoldenDatasetRunResult
        {
            Total = 1,
            Cases = [Case("G1", "unresolved", true, "PASS", "Ambiguous")]
        };

        var scorecard = new GoldenDatasetRegressionEvaluator().Evaluate(result,
            new GoldenDatasetRegressionPolicy { MinimumOverallPassRate = 1.0 });

        Assert.False(scorecard.Passed);
        Assert.Contains("UnexpectedApplicabilityState", scorecard.FailedGates);
    }

    private static GoldenCaseRunResult Case(string id, string category, bool passed, string decision, string? applicability = null) => new()
    {
        CaseId = id,
        Category = category,
        Enabled = true,
        Passed = passed,
        Decision = decision,
        ApplicabilityState = applicability
    };
}
