namespace SuperBuilder_AI.Models.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.9 released Golden Dataset baseline metadata.
/// </summary>
public sealed class GoldenBaseline
{
    public string BaselineId { get; init; } = string.Empty;
    public string Dataset { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public DateTime ReleasedAtUtc { get; init; }
    public string Status { get; init; } = string.Empty;
    public int QualityScore { get; init; }
    public int TotalCases { get; init; }
    public int EnabledCases { get; init; }
    public int MissingDimensionCount { get; init; }
}

public sealed class GoldenBaselineReleaseScorecard
{
    public bool Passed { get; init; }
    public string Decision { get; init; } = string.Empty;
    public GoldenBaseline? Baseline { get; init; }
    public GoldenDatasetQualityScorecard? Quality { get; init; }
    public GoldenDatasetCoverageScorecard? Coverage { get; init; }
    public List<string> BlockingReasons { get; init; } = new();
}
