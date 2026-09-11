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
    /// M12 增量：按业务实体全量合并其指标集合。
    /// 按指标 Name 匹配：命中则仅更新标量字段（保留 Id 与 PhysicalBindings），未命中则新增，传入列表外的存量项删除（cascade 清其物理绑定）。
    /// 租户隔离：先校验实体存在且属该租户，杜绝跨租户写入。
    /// </summary>
    public async Task UpsertMetricsAsync(long tenantId, long entityId, IReadOnlyList<BusinessEntityMetric> metrics, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        await EnsureTenantAsync(tenantId, cancellationToken);
        var entity = await db.BusinessEntities.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == entityId && x.TenantId == tenantId, cancellationToken);
        if (entity is null) throw new KeyNotFoundException($"BusinessEntity {entityId} was not found for tenant {tenantId}.");

        var names = metrics.Select(m => m.Name).ToList();
        if (names.Count != names.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            throw new InvalidOperationException("指标 Name 在集合中必须唯一（不区分大小写）。");

        var existing = await db.BusinessEntityMetrics.Where(m => m.BusinessEntityId == entityId).ToListAsync(cancellationToken);
        var byName = existing.ToDictionary(m => m.Name, m => m, StringComparer.OrdinalIgnoreCase);
        var incoming = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);

        foreach (var e in existing.Where(e => !incoming.Contains(e.Name)))
            db.BusinessEntityMetrics.Remove(e);

        foreach (var src in metrics)
        {
            if (byName.TryGetValue(src.Name, out var e))
            {
                e.DisplayName = src.DisplayName;
                e.Description = src.Description;
                e.SemanticType = src.SemanticType;
                e.Aggregation = src.Aggregation;
                e.IsCalculated = src.IsCalculated;
                e.Expression = src.Expression;
                e.DataType = src.DataType;
                db.BusinessEntityMetrics.Update(e);
            }
            else
            {
                db.BusinessEntityMetrics.Add(new BusinessEntityMetric
                {
                    BusinessEntityId = entityId,
                    Name = src.Name,
                    DisplayName = src.DisplayName,
                    Description = src.Description,
                    SemanticType = src.SemanticType,
                    Aggregation = src.Aggregation,
                    IsCalculated = src.IsCalculated,
                    Expression = src.Expression,
                    DataType = src.DataType,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// M12 增量：按业务域全量合并其维度集合。
    /// 按维度 Name 匹配：命中则更新标量字段，未命中则新增，传入列表外的存量项删除。
    /// 租户隔离：先校验业务域存在且属该租户。
    /// </summary>
    public async Task UpsertDimensionsAsync(long tenantId, long domainId, IReadOnlyList<BusinessEntityDimension> dimensions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dimensions);
        await EnsureTenantAsync(tenantId, cancellationToken);
        var domain = await db.BusinessDomains.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == domainId && x.TenantId == tenantId, cancellationToken);
        if (domain is null) throw new KeyNotFoundException($"BusinessDomain {domainId} was not found for tenant {tenantId}.");

        var names = dimensions.Select(d => d.Name).ToList();
        if (names.Count != names.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            throw new InvalidOperationException("维度 Name 在集合中必须唯一（不区分大小写）。");

        var existing = await db.BusinessEntityDimensions
            .Where(d => d.TenantId == tenantId && d.BusinessDomainId == domainId)
            .ToListAsync(cancellationToken);
        var byName = existing.ToDictionary(d => d.Name, d => d, StringComparer.OrdinalIgnoreCase);
        var incoming = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);

        foreach (var e in existing.Where(e => !incoming.Contains(e.Name)))
            db.BusinessEntityDimensions.Remove(e);

        foreach (var src in dimensions)
        {
            if (byName.TryGetValue(src.Name, out var e))
            {
                e.Description = src.Description;
                e.Expression = src.Expression;
                e.DataType = src.DataType;
                db.BusinessEntityDimensions.Update(e);
            }
            else
            {
                db.BusinessEntityDimensions.Add(new BusinessEntityDimension
                {
                    TenantId = tenantId,
                    BusinessDomainId = domainId,
                    Name = src.Name,
                    Description = src.Description,
                    Expression = src.Expression,
                    DataType = src.DataType,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// M0-06：校验 BusinessEntity 聚合内所有 PhysicalBinding 的 Tenant → DataSource → Table → Column 完整链，
    /// 确保每条物理绑定都归属于该实体的租户，杜绝跨租户字段越权。
    /// </summary>
    private async Task ValidateBindingsAsync(long tenantId, BusinessEntity entity, CancellationToken cancellationToken)
    {
        // 收集每条 PhysicalBinding 及其所属的业务实体成员类型（Key=0 / Attribute=1 / Metric=2 / SourceRel=3 / TargetRel=4）。
        // 注意：CreateAsync 在 db.BusinessEntities.Add 之前调用本方法，FK 列（BusinessEntityKeyId 等）尚未被 EF fixup 填充，
        // 因此不能依赖 FK 列判断归属；必须以"父集合"为准——对象模型天然保证一条绑定只属于一个父集合。
        var bindingsWithOwner = new List<(PhysicalBinding B, int Owner)>();
        bindingsWithOwner.AddRange(entity.Keys.SelectMany(k => k.PhysicalBindings).Select(b => (b, 0)));
        bindingsWithOwner.AddRange(entity.Attributes.SelectMany(a => a.PhysicalBindings).Select(b => (b, 1)));
        bindingsWithOwner.AddRange(entity.Metrics.SelectMany(m => m.PhysicalBindings).Select(b => (b, 2)));
        bindingsWithOwner.AddRange(entity.SourceRelationships.SelectMany(r => r.PhysicalBindings).Select(b => (b, 3)));
        bindingsWithOwner.AddRange(entity.TargetRelationships.SelectMany(r => r.PhysicalBindings).Select(b => (b, 4)));

        // M1-06：恰好一个 Owner 约束（写入路径）。一条 PhysicalBinding 不得同时挂到多个业务实体成员。
        // 采用"父集合归属"判定，兼容既有无 Owner 的存量绑定（按 M1-02/03/04 约定不在 DB 层加硬 CHECK）。
        var multiOwned = bindingsWithOwner
            .GroupBy(x => x.B)
            .Where(g => g.Select(x => x.Owner).Distinct().Count() != 1)
            .ToList();
        if (multiOwned.Count != 0)
            throw new InvalidOperationException(
                "PhysicalBinding 必须且只能绑定一个业务实体成员（Key/Attribute/Metric/Relationship），发现同一条绑定挂到多个成员。");

        foreach (var (binding, _) in bindingsWithOwner)
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

            // M1-06：Priority 非负约束（写入路径 + 数据库 CK_PhysicalBindings_PriorityNonNeg 双重保证）。
            if (binding.Priority < 0)
                throw new InvalidOperationException(
                    $"PhysicalBinding.Priority 不能为负，当前值 {binding.Priority}。");
        }
    }

    private async Task EnsureTenantAsync(long tenantId, CancellationToken cancellationToken)
    {
        var exists = await db.Tenants.AnyAsync(x => x.Id == tenantId, cancellationToken);
        if (!exists) throw new KeyNotFoundException($"Tenant {tenantId} was not found.");
    }
}
