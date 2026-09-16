using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.BI.Entity;
using SuperBuilder_AI.Models.BI.Entity;

namespace SuperBuilder_AI.Services.BI.Entity;

/// <summary>解析 Business Entity 对 SuperBuilder Physical Metadata 的绑定。</summary>
public sealed class PhysicalBindingResolver(SuperBIContext db) : IPhysicalBindingResolver
{
    public async Task<IReadOnlyList<PhysicalBinding>> ResolveAsync(
        long tenantId,
        long dataSourceId,
        long businessEntityId,
        CancellationToken cancellationToken = default)
    {
        // Tenant isolation is enforced through both the Entity and DataSource ownership.
        return await db.PhysicalBindings
            .AsNoTracking()
            .Where(x => x.DataSourceId == dataSourceId && x.IsActive)
            .Where(x => x.DataSource != null && x.DataSource.TenantId == tenantId)
            .Where(x => x.BusinessEntityKey != null && x.BusinessEntityKey.BusinessEntity != null && x.BusinessEntityKey.BusinessEntityId == businessEntityId && x.BusinessEntityKey.BusinessEntity.TenantId == tenantId
                     || x.BusinessEntityAttribute != null && x.BusinessEntityAttribute.BusinessEntity != null && x.BusinessEntityAttribute.BusinessEntityId == businessEntityId && x.BusinessEntityAttribute.BusinessEntity.TenantId == tenantId
                     || x.BusinessEntityMetric != null && x.BusinessEntityMetric.BusinessEntity != null && x.BusinessEntityMetric.BusinessEntityId == businessEntityId && x.BusinessEntityMetric.BusinessEntity.TenantId == tenantId
                     || x.BusinessEntityRelationship != null && (x.BusinessEntityRelationship.SourceEntityId == businessEntityId || x.BusinessEntityRelationship.TargetEntityId == businessEntityId)
                        && x.BusinessEntityRelationship.SourceEntity != null
                        && x.BusinessEntityRelationship.TargetEntity != null
                        && x.BusinessEntityRelationship.SourceEntity.TenantId == tenantId
                        && x.BusinessEntityRelationship.TargetEntity.TenantId == tenantId)
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }
}
