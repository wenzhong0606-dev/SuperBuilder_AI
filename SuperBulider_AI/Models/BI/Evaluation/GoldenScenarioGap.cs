namespace SuperBuilder_AI.Models.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.7.4 Golden Dataset Scenario Gap。
/// 表示 Coverage 缺口对应的待补测试场景，不生成虚假的 Golden Ground Truth。
/// </summary>
public sealed class GoldenScenarioGap
{
    public string Dimension { get; init; } = string.Empty;
    public string ScenarioType { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public List<string> SuggestedTags { get; init; } = new();
}

public sealed class GoldenScenarioGapScorecard
{
    public string Dataset { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public int GapCount { get; init; }
    public List<GoldenScenarioGap> Gaps { get; init; } = new();
}
