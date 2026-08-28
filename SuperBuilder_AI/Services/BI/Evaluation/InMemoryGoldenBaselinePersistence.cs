using SuperBuilder_AI.Interfaces.BI.Evaluation;
using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Persistence adapter used to validate the contract before introducing EF Core storage.
/// </summary>
public sealed class InMemoryGoldenBaselinePersistence : IGoldenBaselinePersistence
{
    private readonly Dictionary<string, GoldenBaselinePersistenceRecord> _records = new(StringComparer.OrdinalIgnoreCase);

    public Task SaveAsync(GoldenBaselinePersistenceRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (string.IsNullOrWhiteSpace(record.Version))
            throw new ArgumentException("Baseline version is required.", nameof(record));
        _records[record.Version] = record;
        return Task.CompletedTask;
    }

    public Task<GoldenBaselinePersistenceRecord?> GetAsync(string version, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(version)) return Task.FromResult<GoldenBaselinePersistenceRecord?>(null);
        return Task.FromResult(_records.GetValueOrDefault(version));
    }

    public Task<IReadOnlyList<GoldenBaselinePersistenceRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<GoldenBaselinePersistenceRecord> result = _records.Values.OrderByDescending(x => x.ReleasedAtUtc).ToList();
        return Task.FromResult(result);
    }
}
