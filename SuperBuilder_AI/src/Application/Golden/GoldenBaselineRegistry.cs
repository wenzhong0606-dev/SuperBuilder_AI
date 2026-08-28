using SuperBuilder_AI.Interfaces.BI.Evaluation;
using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.9.5
/// In-memory registry for released Golden Baselines.
/// Persistence is intentionally deferred until the baseline lifecycle is stable.
/// </summary>
public sealed class GoldenBaselineRegistry : IGoldenBaselineRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<string, GoldenBaseline> _baselines = new(StringComparer.OrdinalIgnoreCase);
    private string? _currentVersion;

    public GoldenBaseline Register(GoldenBaseline baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        if (string.IsNullOrWhiteSpace(baseline.Version))
            throw new ArgumentException("Baseline version is required.", nameof(baseline));
        if (!string.Equals(baseline.Status, "Released", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only Released baselines can be registered.", nameof(baseline));

        lock (_sync)
        {
            _baselines[baseline.Version] = baseline;
            _currentVersion = baseline.Version;
            return baseline;
        }
    }

    public GoldenBaseline? Get(string version)
    {
        if (string.IsNullOrWhiteSpace(version)) return null;
        lock (_sync)
            return _baselines.TryGetValue(version, out var baseline) ? baseline : null;
    }

    public GoldenBaseline? GetCurrent()
    {
        lock (_sync)
            return _currentVersion is not null && _baselines.TryGetValue(_currentVersion, out var baseline)
                ? baseline
                : null;
    }

    public IReadOnlyList<GoldenBaseline> List()
    {
        lock (_sync)
            return _baselines.Values
                .OrderByDescending(x => x.ReleasedAtUtc)
                .ToList();
    }
}
