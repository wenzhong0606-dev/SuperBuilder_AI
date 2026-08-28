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
}
