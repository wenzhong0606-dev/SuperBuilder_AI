namespace SuperBuilder_AI.Models.BI.Evaluation;

/// <summary>
/// Persistence-neutral record for a released Golden Baseline.
/// No EF Core dependency is introduced at this stage.
/// </summary>
public sealed class GoldenBaselinePersistenceRecord
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
