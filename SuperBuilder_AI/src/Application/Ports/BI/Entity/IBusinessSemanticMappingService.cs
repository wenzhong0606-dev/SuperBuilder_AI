using SuperBuilder_AI.Models.BI.Entity;

namespace SuperBuilder_AI.Interfaces.BI.Entity;

/// <summary>
/// 业务语义映射服务：自然语言 → 业务实体。
///
/// 采用确定性词元重叠打分，不调用 LLM，保证 Golden 回归稳定可复现。
/// </summary>
public interface IBusinessSemanticMappingService
{
    Task<BusinessSemanticResolutionResult> ResolveAsync(
        long tenantId,
        long dataSourceId,
        string query,
        int topPerDomain = 3,
        CancellationToken cancellationToken = default);
}
