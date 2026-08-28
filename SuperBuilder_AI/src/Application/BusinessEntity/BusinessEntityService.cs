using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.BI.Entity;
using SuperBuilder_AI.Models.BI.Entity;

namespace SuperBuilder_AI.Services.BI.Entity;

/// <summary>Business Entity 持久化服务，仅操作 SuperBuilder Metadata DB。</summary>
public sealed class BusinessEntityService(SuperBIContext db) : IBusinessEntityService
{
    public Task<BusinessEntity?> GetAsync(long tenantId, long id, CancellationToken cancellationToken = default) =>
        db.BusinessEntities
            .AsNoTracking()
            .Include(x => x.Keys).ThenInclude(x => x.PhysicalBindings).ThenInclude(x => x.MetadataTable)
            .Include(x => x.Keys).ThenInclude(x => x.PhysicalBindings).ThenInclude(x => x.MetadataColumn)
            .Include(x => x.Attributes).ThenInclude(x => x.PhysicalBindings).ThenInclude(x => x.MetadataTable)
            .Include(x => x.Attributes).ThenInclude(x => x.PhysicalBindings).ThenInclude(x => x.MetadataColumn)
            .Include(x => x.Metrics).ThenInclude(x => x.PhysicalBindings).ThenInclude(x => x.MetadataTable)
            .Include(x => x.Metrics).ThenInclude(x => x.PhysicalBindings).ThenInclude(x => x.MetadataColumn)
            .Include(x => x.SourceRelationships).ThenInclude(x => x.PhysicalBindings).ThenInclude(x => x.MetadataTable)
            .Include(x => x.SourceRelationships).ThenInclude(x => x.PhysicalBindings).ThenInclude(x => x.MetadataColumn)
            .Include(x => x.TargetRelationships).ThenInclude(x => x.PhysicalBindings).ThenInclude(x => x.MetadataTable)
            .Include(x => x.TargetRelationships).ThenInclude(x => x.PhysicalBindings).ThenInclude(x => x.MetadataColumn)
            .SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken);

    public async Task<IReadOnlyList<BusinessEntity>> ListAsync(long tenantId, CancellationToken cancellationToken = default) =>
        await db.BusinessEntities
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task<BusinessEntity> CreateAsync(BusinessEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await EnsureTenantAsync(entity.TenantId, cancellationToken);
        db.BusinessEntities.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<BusinessEntity> UpdateAsync(BusinessEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await EnsureTenantAsync(entity.TenantId, cancellationToken);
        var exists = await db.BusinessEntities.AnyAsync(x => x.Id == entity.Id && x.TenantId == entity.TenantId, cancellationToken);
        if (!exists) throw new KeyNotFoundException($"BusinessEntity {entity.Id} was not found for tenant {entity.TenantId}.");
        db.BusinessEntities.Update(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task DeleteAsync(long tenantId, long id, CancellationToken cancellationToken = default)
    {
        var entity = await db.BusinessEntities.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken);
        if (entity is null) return;
        db.BusinessEntities.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureTenantAsync(long tenantId, CancellationToken cancellationToken)
    {
        var exists = await db.Tenants.AnyAsync(x => x.Id == tenantId, cancellationToken);
        if (!exists) throw new KeyNotFoundException($"Tenant {tenantId} was not found.");
    }
}
