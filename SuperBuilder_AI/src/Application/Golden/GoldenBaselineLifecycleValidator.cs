using SuperBuilder_AI.Interfaces.BI.Evaluation;
using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.9.9 validates Registry/Persistence lifecycle consistency.
/// It is diagnostic-only and never mutates or creates a baseline.
/// </summary>
public sealed class GoldenBaselineLifecycleValidator
{
    private readonly IGoldenBaselineRegistry _registry;
    private readonly IGoldenBaselinePersistence _persistence;

    public GoldenBaselineLifecycleValidator(
        IGoldenBaselineRegistry registry,
        IGoldenBaselinePersistence persistence)
    {
        _registry = registry;
        _persistence = persistence;
    }

    public async Task<GoldenBaselineLifecycleScorecard> ValidateAsync(
        GoldenBaseline baseline,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);

        var registered = _registry.Get(baseline.Version);
        var persisted = await _persistence.GetAsync(baseline.Version, cancellationToken);
        var current = _registry.GetCurrent();
        var persistedList = await _persistence.ListAsync(cancellationToken);

        var checks = new List<GoldenBaselineLifecycleCheck>
        {
            Check("ReleasedStatus", string.Equals(baseline.Status, "Released", StringComparison.OrdinalIgnoreCase), "Baseline status must be Released."),
            Check("RegistryContainsVersion", registered is not null, "Registry does not contain the baseline version."),
            Check("PersistenceContainsVersion", persisted is not null, "Persistence does not contain the baseline version."),
            Check("RegistryPersistenceConsistent", Same(registered, persisted), "Registry and persistence records differ."),
            Check("CurrentVersion", current is not null && string.Equals(current.Version, baseline.Version, StringComparison.OrdinalIgnoreCase), "Baseline is not the current registry baseline."),
            Check("PersistenceListContainsVersion", persistedList.Any(x => string.Equals(x.Version, baseline.Version, StringComparison.OrdinalIgnoreCase)), "Persistence list does not contain the baseline version.")
        };

        return new GoldenBaselineLifecycleScorecard
        {
            Passed = checks.All(x => x.Passed),
            Version = baseline.Version,
            CurrentVersion = current?.Version ?? string.Empty,
            RegistryCount = _registry.List().Count,
            PersistenceCount = persistedList.Count,
            Checks = checks
        };
    }

    private static GoldenBaselineLifecycleCheck Check(string name, bool passed, string message)
        => new() { Name = name, Passed = passed, Message = passed ? "OK" : message };

    private static bool Same(GoldenBaseline? baseline, GoldenBaselinePersistenceRecord? record)
        => baseline is not null && record is not null
            && baseline.BaselineId == record.BaselineId
            && baseline.Dataset == record.Dataset
            && baseline.Version == record.Version
            && baseline.ReleasedAtUtc == record.ReleasedAtUtc
            && baseline.Status == record.Status
            && baseline.QualityScore == record.QualityScore
            && baseline.TotalCases == record.TotalCases
            && baseline.EnabledCases == record.EnabledCases
            && baseline.MissingDimensionCount == record.MissingDimensionCount;
}

public sealed class GoldenBaselineLifecycleScorecard
{
    public bool Passed { get; init; }
    public string Version { get; init; } = string.Empty;
    public string CurrentVersion { get; init; } = string.Empty;
    public int RegistryCount { get; init; }
    public int PersistenceCount { get; init; }
    public IReadOnlyList<GoldenBaselineLifecycleCheck> Checks { get; init; } = Array.Empty<GoldenBaselineLifecycleCheck>();
}

public sealed class GoldenBaselineLifecycleCheck
{
    public string Name { get; init; } = string.Empty;
    public bool Passed { get; init; }
    public string Message { get; init; } = string.Empty;
}
