using SuperBuilder_AI.Models.BI.Entity;

namespace SuperBuilder_AI.Interfaces.BI.Entity;

/// <summary>
/// Business Entity 到 Physical Metadata 的绑定解析契约。
/// 只解析 SuperBuilder Metadata DB 中的 Binding，不直接连接动态业务数据库。
/// </summary>
public interface IPhysicalBindingResolver
{
    Task<IReadOnlyList<PhysicalBinding>> ResolveAsync(
        long tenantId,
        long dataSourceId,
        long businessEntityId,
        CancellationToken cancellationToken = default);
}
