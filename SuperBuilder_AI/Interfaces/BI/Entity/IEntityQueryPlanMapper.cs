using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Entity;

namespace SuperBuilder_AI.Interfaces.BI.Entity;

/// <summary>
/// Business Entity Semantic Model 到既有 QueryPlanSemanticResolution 的映射契约。
/// Mapping 必须显式限定 DataSource，不在多数据源场景下隐式选择物理绑定。
/// 不生成 SQL，不改变 Phase 2.7 QueryPlan Contract。
/// </summary>
public interface IEntityQueryPlanMapper
{
    Task<QueryPlanSemanticResolution> MapAsync(
        BusinessEntity entity,
        QueryIntent intent,
        long dataSourceId,
        CancellationToken cancellationToken = default);
}
