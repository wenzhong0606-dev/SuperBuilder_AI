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
        return await db.PhysicalBindings
            .AsNoTracking()
            .Where(x => x.DataSourceId == dataSourceId && x.IsActive)
            .Where(x => x.BusinessEntityKey != null && x.BusinessEntityKey.BusinessEntityId == businessEntityId
                     || x.BusinessEntityAttribute != null && x.BusinessEntityAttribute.BusinessEntityId == businessEntityId
                     || x.BusinessEntityMetric != null && x.BusinessEntityMetric.BusinessEntityId == businessEntityId
                     || x.BusinessEntityRelationship != null && x.BusinessEntityRelationship.SourceEntityId == businessEntityId
                     || x.BusinessEntityRelationship != null && x.BusinessEntityRelationship.TargetEntityId == businessEntityId)
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }
}
