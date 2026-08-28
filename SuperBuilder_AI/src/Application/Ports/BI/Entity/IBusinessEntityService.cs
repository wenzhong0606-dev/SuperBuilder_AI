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
}
