using SuperBuilder_AI.Models.BI.Entity;

namespace SuperBuilder_AI.Interfaces.BI.Entity;

/// <summary>
/// 业务实体注册表服务：按租户列举业务实体与业务域。
///
/// 只读取 SuperBuilder Metadata DB，不连接动态业务数据库。
/// </summary>
public interface IBusinessEntityRegistryService
{
    Task<IReadOnlyList<BusinessEntity>> ListEntitiesAsync(long tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BusinessDomain>> ListDomainsAsync(long tenantId, CancellationToken cancellationToken = default);
    Task<BusinessEntity?> GetAsync(long tenantId, long id, CancellationToken cancellationToken = default);
}
