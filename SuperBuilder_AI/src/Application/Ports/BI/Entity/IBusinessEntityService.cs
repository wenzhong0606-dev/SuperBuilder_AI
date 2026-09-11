using SuperBuilder_AI.Models.BI.Entity;

namespace SuperBuilder_AI.Interfaces.BI.Entity;

/// <summary>
/// Business Entity 持久化服务契约。
/// 只管理 SuperBuilder Metadata DB 中的业务语义模型，不访问动态业务数据库。
/// </summary>
public interface IBusinessEntityService
{
    Task<BusinessEntity?> GetAsync(long tenantId, long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BusinessEntity>> ListAsync(long tenantId, CancellationToken cancellationToken = default);
    Task<BusinessEntity> CreateAsync(BusinessEntity entity, CancellationToken cancellationToken = default);
    Task<BusinessEntity> UpdateAsync(BusinessEntity entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(long tenantId, long id, CancellationToken cancellationToken = default);

    /// <summary>M12 增量：按业务实体全量合并其指标集合（按指标 Name 匹配更新/新增/删除，保留匹配项的物理绑定）。租户隔离经实体 TenantId 保证。</summary>
    Task UpsertMetricsAsync(long tenantId, long entityId, IReadOnlyList<BusinessEntityMetric> metrics, CancellationToken cancellationToken = default);

    /// <summary>M12 增量：按业务域全量合并其维度集合（按维度 Name 匹配更新/新增/删除）。租户隔离经维度 TenantId 保证。</summary>
    Task UpsertDimensionsAsync(long tenantId, long domainId, IReadOnlyList<BusinessEntityDimension> dimensions, CancellationToken cancellationToken = default);
}
