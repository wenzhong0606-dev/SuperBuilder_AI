using SuperBuilder_AI.Models.BI.Entity;

namespace SuperBuilder_AI.Interfaces.BI.Entity;

/// <summary>
/// 业务域 / 业务维度仓储端口（P3 批次2）。
///
/// 只操作 SuperBuilder Metadata DB，不连接动态业务数据库。
/// 该端口为后续 A4 依赖倒置做准备：让 Application 层不直接依赖 DbContext。
/// </summary>
public interface IBusinessEntityRepository
{
    Task<BusinessDomain?> GetDomainByCodeAsync(long tenantId, string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BusinessDomain>> ListDomainsAsync(long tenantId, CancellationToken cancellationToken = default);
    Task UpsertDomainAsync(BusinessDomain domain, CancellationToken cancellationToken = default);

    Task<BusinessEntityDimension?> GetDimensionAsync(long tenantId, long domainId, string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BusinessEntityDimension>> ListDimensionsAsync(long tenantId, long domainId, CancellationToken cancellationToken = default);
    Task UpsertDimensionAsync(BusinessEntityDimension dimension, CancellationToken cancellationToken = default);

    /// <summary>M12-15：按租户列举业务实体之间的语义关系（Source/Target 实体均须属于该租户），用于关系图。</summary>
    Task<IReadOnlyList<BusinessEntityRelationship>> ListRelationshipsAsync(long tenantId, CancellationToken cancellationToken = default);

    /// <summary>M12-16：按租户列举全部业务实体指标（经 BusinessEntity.TenantId 过滤，附实体名/业务域/物理绑定计数），用于指标中心。</summary>
    Task<IReadOnlyList<BusinessMetricView>> ListMetricsAsync(long tenantId, CancellationToken cancellationToken = default);

    /// <summary>M12-16：按租户列举全部业务维度（附所属业务域名），用于指标中心。</summary>
    Task<IReadOnlyList<BusinessDimensionView>> ListDimensionsByTenantAsync(long tenantId, CancellationToken cancellationToken = default);
}
