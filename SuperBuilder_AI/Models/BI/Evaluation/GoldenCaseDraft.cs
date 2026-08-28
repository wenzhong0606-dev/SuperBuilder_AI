namespace SuperBuilder_AI.Models.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.7.5 Golden Case Draft。
/// 由 Coverage Gap 生成候选草稿，但不等同于已确认的 Ground Truth。
/// </summary>
public sealed class GoldenCaseDraft
{
    public string DraftId { get; init; } = string.Empty;
    public string Dimension { get; init; } = string.Empty;
    public string ScenarioType { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string QuestionTemplate { get; init; } = string.Empty;
    public List<string> SuggestedTags { get; init; } = new();
    public bool RequiresHumanConfirmation { get; init; } = true;
}

public sealed class GoldenCaseDraftScorecard
{
    public string Dataset { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public int DraftCount { get; init; }
    public List<GoldenCaseDraft> Drafts { get; init; } = new();
}
