using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.10.9 evaluates whether a candidate Golden Dataset can safely replace the released baseline.
/// It deliberately reuses the existing baseline comparison and does not mutate the baseline.
/// </summary>
public sealed class GoldenBaselineRegressionService
{
    private readonly GoldenBaselineComparisonService _comparisonService;

    public GoldenBaselineRegressionService(GoldenBaselineComparisonService comparisonService)
    {
        _comparisonService = comparisonService;
    }

    public GoldenBaselineRegressionResult Evaluate(GoldenBaseline baseline, GoldenQueryDataset candidate)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(candidate);

        var comparison = _comparisonService.Compare(baseline, candidate);
        var regressions = comparison.Regressions.ToList();
        var warnings = new List<string>();

        if (comparison.QualityScoreDelta < 0)
            regressions.Add("Candidate quality score regressed from the released baseline.");

        if (comparison.MissingDimensionDelta > 0)
            regressions.Add("Candidate coverage regressed by introducing additional missing dimensions.");

        if (comparison.EnabledCaseCountDelta < 0)
            regressions.Add("Candidate removes enabled Golden Cases from the baseline.");

        if (comparison.CaseCountDelta > 0 && comparison.EnabledCaseCountDelta == 0)
            warnings.Add("Candidate adds cases, but none are enabled; added cases cannot improve active regression coverage.");

        var blocking = regressions
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new GoldenBaselineRegressionResult
        {
            Passed = blocking.Count == 0 && comparison.Passed,
            Decision = blocking.Count == 0 && comparison.Passed ? "PASS" : "BLOCK",
            BaselineVersion = baseline.Version,
            CandidateVersion = candidate.Version,
            Comparison = comparison,
            BlockingRegressions = blocking,
            Warnings = warnings
        };
    }
}

public sealed class GoldenBaselineRegressionResult
{
    public bool Passed { get; init; }
    public string Decision { get; init; } = string.Empty;
    public string BaselineVersion { get; init; } = string.Empty;
    public string CandidateVersion { get; init; } = string.Empty;
    public GoldenBaselineComparisonScorecard Comparison { get; init; } = new();
    public IReadOnlyList<string> BlockingRegressions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
