namespace SuperBuilder_AI.Models.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.9.3 comparison between an existing baseline and a candidate dataset.
/// </summary>
public sealed class GoldenBaselineComparisonScorecard
{
    public bool Passed { get; init; }
    public string Decision { get; init; } = string.Empty;
    public string BaselineVersion { get; init; } = string.Empty;
    public string CandidateVersion { get; init; } = string.Empty;
    public int CaseCountDelta { get; init; }
    public int EnabledCaseCountDelta { get; init; }
    public int QualityScoreDelta { get; init; }
    public int MissingDimensionDelta { get; init; }
    public List<string> Improvements { get; init; } = new();
    public List<string> Regressions { get; init; } = new();
}
