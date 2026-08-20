using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.9
/// 将通过 Quality + Coverage Gate 的 Golden Dataset 标记为可发布 Baseline。
/// 当前阶段只产生 Release Scorecard，不持久化或覆盖正式 Golden Dataset。
/// </summary>
public sealed class GoldenBaselineReleaseService
{
    private readonly GoldenDatasetQualityGate _qualityGate;
    private readonly GoldenDatasetCoverageAnalyzer _coverageAnalyzer;

    public GoldenBaselineReleaseService(
        GoldenDatasetQualityGate qualityGate,
        GoldenDatasetCoverageAnalyzer coverageAnalyzer)
    {
        _qualityGate = qualityGate;
        _coverageAnalyzer = coverageAnalyzer;
    }

    public GoldenBaselineReleaseScorecard Evaluate(GoldenQueryDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        var quality = _qualityGate.Evaluate(dataset);
        var coverage = _coverageAnalyzer.Analyze(dataset);
        var reasons = new List<string>();

        if (!quality.Passed)
            reasons.Add("Golden Dataset Quality Gate failed.");
        if (coverage.EnabledCases <= 0)
            reasons.Add("No enabled Golden Cases are available.");
        if (coverage.MissingDimensions.Count > 0)
            reasons.Add($"Coverage has {coverage.MissingDimensions.Count} missing dimensions.");

        var passed = reasons.Count == 0;
        GoldenBaseline? baseline = null;

        if (passed)
        {
            baseline = new GoldenBaseline
            {
                BaselineId = $"{dataset.Dataset}:{dataset.Version}",
                Dataset = dataset.Dataset,
                Version = dataset.Version,
                ReleasedAtUtc = DateTime.UtcNow,
                Status = "Released",
                QualityScore = quality.Score,
                TotalCases = quality.TotalCases,
                EnabledCases = quality.EnabledCases,
                MissingDimensionCount = coverage.MissingDimensions.Count
            };
        }

        return new GoldenBaselineReleaseScorecard
        {
            Passed = passed,
            Decision = passed ? "RELEASE" : "BLOCK",
            Baseline = baseline,
            Quality = quality,
            Coverage = coverage,
            BlockingReasons = reasons
        };
    }
}
