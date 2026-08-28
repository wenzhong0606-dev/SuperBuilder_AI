namespace SuperBuilder_AI.Models.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.8 Golden Dataset Quality Gate。
/// </summary>
public sealed class GoldenDatasetQualityScorecard
{
    public string Dataset { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public bool Passed { get; init; }
    public int Score { get; init; }
    public int TotalCases { get; init; }
    public int EnabledCases { get; init; }
    public List<GoldenDatasetQualityIssue> Issues { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}

public sealed class GoldenDatasetQualityIssue
{
    public string Code { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string CaseId { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}
