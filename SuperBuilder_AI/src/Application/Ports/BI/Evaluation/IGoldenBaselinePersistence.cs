using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Interfaces.BI.Evaluation;

/// <summary>
/// Persistence abstraction for Golden Baselines.
/// The initial implementation can be in-memory; EF Core is deliberately deferred.
/// </summary>
public interface IGoldenBaselinePersistence
{
    Task SaveAsync(GoldenBaselinePersistenceRecord record, CancellationToken cancellationToken = default);
    Task<GoldenBaselinePersistenceRecord?> GetAsync(string version, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GoldenBaselinePersistenceRecord>> ListAsync(CancellationToken cancellationToken = default);
}
