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
        await ValidateBindingsAsync(entity.TenantId, entity, cancellationToken);
        db.BusinessEntities.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<BusinessEntity> UpdateAsync(BusinessEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await EnsureTenantAsync(entity.TenantId, cancellationToken);
        await ValidateBindingsAsync(entity.TenantId, entity, cancellationToken);
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

    /// <summary>
    /// M0-06：校验 BusinessEntity 聚合内所有 PhysicalBinding 的 Tenant → DataSource → Table → Column 完整链，
    /// 确保每条物理绑定都归属于该实体的租户，杜绝跨租户字段越权。
    /// </summary>
    private async Task ValidateBindingsAsync(long tenantId, BusinessEntity entity, CancellationToken cancellationToken)
    {
        var bindings = entity.Keys.SelectMany(k => k.PhysicalBindings)
            .Concat(entity.Attributes.SelectMany(a => a.PhysicalBindings))
            .Concat(entity.Metrics.SelectMany(m => m.PhysicalBindings))
            .Concat(entity.SourceRelationships.SelectMany(r => r.PhysicalBindings))
            .Concat(entity.TargetRelationships.SelectMany(r => r.PhysicalBindings))
            .ToList();
        foreach (var binding in bindings)
        {
            var column = await db.MetadataColumns.AsNoTracking()
                .Include(c => c.MetadataTable)
                .FirstOrDefaultAsync(c => c.Id == binding.MetadataColumnId, cancellationToken);
            if (column is null || column.MetadataTableId != binding.MetadataTableId)
                throw new InvalidOperationException(
                    $"PhysicalBinding 引用的列 {binding.MetadataColumnId} 不存在或不属于声明的表 {binding.MetadataTableId}。");

            var table = column.MetadataTable;
            if (table is null || table.DataSourceId != binding.DataSourceId)
                throw new InvalidOperationException(
                    $"PhysicalBinding 声明的表 {binding.MetadataTableId} 不属于声明的 DataSource {binding.DataSourceId}。");

            var dataSource = await db.DataSources.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == binding.DataSourceId, cancellationToken);
            if (dataSource is null || dataSource.TenantId != tenantId)
                throw new InvalidOperationException(
                    $"PhysicalBinding 声明的 DataSource {binding.DataSourceId} 不存在或不属于租户 {tenantId}。");
        }
    }

    private async Task EnsureTenantAsync(long tenantId, CancellationToken cancellationToken)
    {
        var exists = await db.Tenants.AnyAsync(x => x.Id == tenantId, cancellationToken);
        if (!exists) throw new KeyNotFoundException($"Tenant {tenantId} was not found.");
    }
}
