using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.9.3
/// 比较已发布 Golden Baseline 与候选 Dataset。
/// 不修改 Ground Truth，仅产生 Release Candidate Comparison。
/// </summary>
public sealed class GoldenBaselineComparisonService
{
    private readonly GoldenDatasetQualityGate _qualityGate;
    private readonly GoldenDatasetCoverageAnalyzer _coverageAnalyzer;

    public GoldenBaselineComparisonService(
        GoldenDatasetQualityGate qualityGate,
        GoldenDatasetCoverageAnalyzer coverageAnalyzer)
    {
        _qualityGate = qualityGate;
        _coverageAnalyzer = coverageAnalyzer;
    }

    public GoldenBaselineComparisonScorecard Compare(GoldenBaseline baseline, GoldenQueryDataset candidate)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(candidate);

        var quality = _qualityGate.Evaluate(candidate);
        var coverage = _coverageAnalyzer.Analyze(candidate);
        var caseCount = candidate.Cases?.Count ?? 0;
        var enabledCount = candidate.Cases?.Count(x => x.Enabled) ?? 0;
        var caseDelta = caseCount - baseline.TotalCases;
        var enabledDelta = enabledCount - baseline.EnabledCases;
        var qualityDelta = quality.Score - baseline.QualityScore;
        var missingDelta = coverage.MissingDimensions.Count - baseline.MissingDimensionCount;

        var improvements = new List<string>();
        var regressions = new List<string>();

        if (caseDelta > 0) improvements.Add($"Case count increased by {caseDelta}.");
        if (enabledDelta > 0) improvements.Add($"Enabled case count increased by {enabledDelta}.");
        if (qualityDelta > 0) improvements.Add($"Quality score improved by {qualityDelta}.");
        if (missingDelta < 0) improvements.Add($"Missing dimension count decreased by {-missingDelta}.");

        if (caseDelta < 0) regressions.Add($"Case count decreased by {-caseDelta}.");
        if (enabledDelta < 0) regressions.Add($"Enabled case count decreased by {-enabledDelta}.");
        if (qualityDelta < 0) regressions.Add($"Quality score decreased by {-qualityDelta}.");
        if (missingDelta > 0) regressions.Add($"Missing dimension count increased by {missingDelta}.");
        if (!quality.Passed) regressions.Add("Candidate fails the Golden Dataset Quality Gate.");
        if (coverage.EnabledCases == 0) regressions.Add("Candidate has no enabled Golden Cases.");
        if (coverage.MissingDimensions.Count > 0) regressions.Add($"Candidate still has {coverage.MissingDimensions.Count} missing coverage dimensions.");

        var passed = quality.Passed && coverage.EnabledCases > 0 && coverage.MissingDimensions.Count == 0 && regressions.Count == 0;

        return new GoldenBaselineComparisonScorecard
        {
            Passed = passed,
            Decision = passed ? "RELEASE" : "REJECT",
            BaselineVersion = baseline.Version,
            CandidateVersion = candidate.Version,
            CaseCountDelta = caseDelta,
            EnabledCaseCountDelta = enabledDelta,
            QualityScoreDelta = qualityDelta,
            MissingDimensionDelta = missingDelta,
            Improvements = improvements,
            Regressions = regressions
        };
    }
}
