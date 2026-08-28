namespace SuperBuilder_AI.Models.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.7 Golden Dataset Coverage。
/// 描述 Dataset 对 QueryPlan 能力维度的测试覆盖，而不是简单统计 Case 数量。
/// </summary>
public sealed class GoldenDatasetCoverageScorecard
{
    public string Dataset { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public int TotalCases { get; init; }
    public int EnabledCases { get; init; }
    public Dictionary<string, int> DimensionCounts { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, double> DimensionCoverage { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> MissingDimensions { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}

public sealed class GoldenDatasetCoverageCase
{
    public string CaseId { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public List<string> Dimensions { get; init; } = new();
}
