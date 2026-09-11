using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.BI.Entity;
using SuperBuilder_AI.Models.BI.Entity;

namespace SuperBuilder_AI.Infrastructure.Persistence;

/// <summary>
/// 业务域 / 业务维度仓储实现（P3 批次2）。
///
/// 只操作 SuperBuilder Metadata DB，不连接动态业务数据库。
/// </summary>
public sealed class BusinessEntityRepository(SuperBIContext db) : IBusinessEntityRepository
{
    public async Task<BusinessDomain?> GetDomainByCodeAsync(
        long tenantId,
        string name,
        CancellationToken cancellationToken = default) =>
        await db.BusinessDomains
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Name == name, cancellationToken);

    public async Task<IReadOnlyList<BusinessDomain>> ListDomainsAsync(
        long tenantId,
        CancellationToken cancellationToken = default) =>
        await db.BusinessDomains
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task UpsertDomainAsync(BusinessDomain domain, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domain);

        var existing = await db.BusinessDomains
            .SingleOrDefaultAsync(x => x.TenantId == domain.TenantId && x.Name == domain.Name, cancellationToken);

        if (existing is null)
        {
            db.BusinessDomains.Add(domain);
        }
        else
        {
            existing.Description = domain.Description;
            db.BusinessDomains.Update(existing);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<BusinessEntityDimension?> GetDimensionAsync(
        long tenantId,
        long domainId,
        string name,
        CancellationToken cancellationToken = default) =>
        await db.BusinessEntityDimensions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.BusinessDomainId == domainId && x.Name == name, cancellationToken);

    public async Task<IReadOnlyList<BusinessEntityDimension>> ListDimensionsAsync(
        long tenantId,
        long domainId,
        CancellationToken cancellationToken = default) =>
        await db.BusinessEntityDimensions
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.BusinessDomainId == domainId)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task UpsertDimensionAsync(BusinessEntityDimension dimension, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dimension);

        var existing = await db.BusinessEntityDimensions
            .SingleOrDefaultAsync(
                x => x.TenantId == dimension.TenantId
                     && x.BusinessDomainId == dimension.BusinessDomainId
                     && x.Name == dimension.Name,
                cancellationToken);

        if (existing is null)
        {
            db.BusinessEntityDimensions.Add(dimension);
        }
        else
        {
            existing.Description = dimension.Description;
            db.BusinessEntityDimensions.Update(existing);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>M12-15：列举租户内业务实体关系。两端实体必须均属该租户，租户隔离由数据面保证。</summary>
    public async Task<IReadOnlyList<BusinessEntityRelationship>> ListRelationshipsAsync(
        long tenantId,
        CancellationToken cancellationToken = default) =>
        await db.BusinessEntityRelationships
            .AsNoTracking()
            .Include(x => x.SourceEntity)
            .Include(x => x.TargetEntity)
            .Where(x => x.SourceEntity != null && x.SourceEntity.TenantId == tenantId
                        && x.TargetEntity != null && x.TargetEntity.TenantId == tenantId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// M12-16：列举租户内全部指标。BusinessEntityMetric 本身无 TenantId，
    /// 租户隔离经所属 BusinessEntity 过滤（EF 翻译为 INNER JOIN）；投影同时带出实体名与物理绑定计数。
    /// M12 增量：投影补充 Expression / DataType 供前端编辑预填。
    /// </summary>
    public async Task<IReadOnlyList<BusinessMetricView>> ListMetricsAsync(
        long tenantId,
        CancellationToken cancellationToken = default) =>
        await db.BusinessEntityMetrics
            .AsNoTracking()
            .Where(m => m.BusinessEntity!.TenantId == tenantId)
            .OrderBy(m => m.BusinessEntityId)
            .ThenBy(m => m.Name)
            .ThenBy(m => m.Id)
            .Select(m => new BusinessMetricView(
                m.Id,
                m.BusinessEntityId,
                m.BusinessEntity!.Name,
                m.BusinessEntity.DisplayName,
                m.BusinessEntity.BusinessDomain,
                m.Name,
                m.DisplayName,
                m.Description,
                m.SemanticType,
                m.Aggregation,
                m.IsCalculated,
                m.PhysicalBindings.Count,
                m.Expression,
                m.DataType))
            .ToListAsync(cancellationToken);

    /// <summary>M12-16：列举租户内全部维度（附所属业务域名）。维度自带 TenantId，直接过滤。M12 增量：投影补充 Expression / DataType。</summary>
    public async Task<IReadOnlyList<BusinessDimensionView>> ListDimensionsByTenantAsync(
        long tenantId,
        CancellationToken cancellationToken = default) =>
        await db.BusinessEntityDimensions
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .Select(x => new BusinessDimensionView(
                x.Id,
                x.BusinessDomainId,
                x.Domain != null ? x.Domain.Name : null,
                x.Name,
                x.Description,
                x.Expression,
                x.DataType))
            .ToListAsync(cancellationToken);
}
