using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;

namespace SuperBuilder_AI.Application.Metadata;

/// <summary>Reclaims inactive relational metadata only after all external references have moved.</summary>
public sealed class MetadataVersionGcJob
{
    private readonly SuperBIContext _context;

    public MetadataVersionGcJob(SuperBIContext context) => _context = context;

    public async Task RunAsync(CancellationToken ct = default)
    {
        var sources = await _context.DataSources.AsNoTracking()
            .Where(d => d.ActiveMetadataVersion > 0)
            .Select(d => new { d.Id, d.ActiveMetadataVersion })
            .ToListAsync(ct);

        foreach (var source in sources)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            var oldTables = _context.MetadataTables.Where(t => t.DataSourceId == source.Id
                && t.MetadataVersion < source.ActiveMetadataVersion);
            var oldTableIds = oldTables.Select(t => t.Id);
            var oldColumns = _context.MetadataColumns.Where(c => oldTableIds.Contains(c.MetadataTableId));
            var oldColumnIds = oldColumns.Select(c => c.Id);

            if (await _context.RowLevelSecurityPolicies.AnyAsync(p => p.DataSourceId == source.Id
                    && (oldTableIds.Contains(p.MetadataTableId) || oldColumnIds.Contains(p.MetadataColumnId)), ct)
                || await _context.PhysicalBindings.AnyAsync(b => b.DataSourceId == source.Id
                    && (oldTableIds.Contains(b.MetadataTableId) || oldColumnIds.Contains(b.MetadataColumnId)), ct)
                || await _context.LearningRecords.AnyAsync(r => r.MetadataColumnId != null
                    && oldColumnIds.Contains(r.MetadataColumnId.Value), ct))
            {
                continue;
            }

            await _context.MetadataSemantics.Where(s => s.MetadataColumnId != null
                    && oldColumnIds.Contains(s.MetadataColumnId.Value))
                .ExecuteDeleteAsync(ct);
            await oldColumns.ExecuteDeleteAsync(ct);
            await oldTables.ExecuteDeleteAsync(ct);
            await transaction.CommitAsync(ct);
        }
    }
}
