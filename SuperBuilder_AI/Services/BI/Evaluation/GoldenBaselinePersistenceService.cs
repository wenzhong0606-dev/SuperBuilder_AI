using SuperBuilder_AI.Interfaces.BI.Evaluation;
using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Coordinates Registry and Persistence without coupling either implementation to EF Core.
/// </summary>
public sealed class GoldenBaselinePersistenceService
{
    private readonly IGoldenBaselineRegistry _registry;
    private readonly IGoldenBaselinePersistence _persistence;

    public GoldenBaselinePersistenceService(
        IGoldenBaselineRegistry registry,
        IGoldenBaselinePersistence persistence)
    {
        _registry = registry;
        _persistence = persistence;
    }

    public async Task<GoldenBaseline> RegisterAndPersistAsync(
        GoldenBaseline baseline,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        var registered = _registry.Register(baseline);
        await _persistence.SaveAsync(
            GoldenBaselinePersistenceMapper.ToRecord(registered),
            cancellationToken);
        return registered;
    }

    public async Task<GoldenBaseline?> GetAsync(
        string version,
        CancellationToken cancellationToken = default)
    {
        var persisted = await _persistence.GetAsync(version, cancellationToken);
        if (persisted is null)
            return _registry.Get(version);

        var baseline = GoldenBaselinePersistenceMapper.ToBaseline(persisted);
        _registry.Register(baseline);
        return baseline;
    }

    public async Task<IReadOnlyList<GoldenBaseline>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var persisted = await _persistence.ListAsync(cancellationToken);
        if (persisted.Count == 0)
            return _registry.List();

        var baselines = persisted
            .Select(GoldenBaselinePersistenceMapper.ToBaseline)
            .ToList();

        foreach (var baseline in baselines)
            _registry.Register(baseline);

        return baselines;
    }
}
