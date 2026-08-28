using SuperBuilder_AI.Interfaces.BI.Entity;
using SuperBuilder_AI.Models.BI.Entity;

namespace SuperBuilder_AI.Services.BI.Entity;

/// <summary>
/// 业务实体注册表服务实现。
///
/// 复用 IBusinessEntityService 读取业务实体，复用仓储端口读取业务域。
/// 只访问 SuperBuilder Metadata DB，不连接动态业务数据库。
/// </summary>
public sealed class BusinessEntityRegistryService(
    IBusinessEntityService entities,
    IBusinessEntityRepository repository) : IBusinessEntityRegistryService
{
    public async Task<IReadOnlyList<BusinessEntity>> ListEntitiesAsync(
        long tenantId,
        CancellationToken cancellationToken = default) =>
        await entities.ListAsync(tenantId, cancellationToken);

    public async Task<IReadOnlyList<BusinessDomain>> ListDomainsAsync(
        long tenantId,
        CancellationToken cancellationToken = default) =>
        await repository.ListDomainsAsync(tenantId, cancellationToken);

    public async Task<BusinessEntity?> GetAsync(
        long tenantId,
        long id,
        CancellationToken cancellationToken = default) =>
        await entities.GetAsync(tenantId, id, cancellationToken);
}
