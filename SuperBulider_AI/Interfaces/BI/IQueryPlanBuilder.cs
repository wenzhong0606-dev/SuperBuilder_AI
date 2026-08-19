using SuperBulider_AI.Models.BI;

namespace SuperBulider_AI.Interfaces.BI;

/// <summary>
/// Query执行计划构建服务。
/// </summary>
public interface IQueryPlanBuilder
{
    /// <summary>
    /// 创建查询计划。
    /// </summary>
    Task<QueryPlan> BuildAsync(QueryIntent intent);

    /// <summary>
    /// 使用已经完成 Semantic Applicability Resolution 的绑定构建查询计划。
    /// Resolution 为 null 时保持原有行为。
    /// </summary>
    Task<QueryPlan> BuildAsync(
        QueryIntent intent,
        QueryPlanSemanticResolution? resolution);
}
