using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Entity;

namespace SuperBuilder_AI.Interfaces.BI.Entity;

/// <summary>
/// Business Entity Semantic Model 到既有 QueryPlanSemanticResolution 的映射契约。
/// 不生成 SQL，不改变 Phase 2.7 QueryPlan Contract。
/// </summary>
public interface IEntityQueryPlanMapper
{
    Task<QueryPlanSemanticResolution> MapAsync(
        BusinessEntity entity,
        QueryIntent intent,
        CancellationToken cancellationToken = default);
}
