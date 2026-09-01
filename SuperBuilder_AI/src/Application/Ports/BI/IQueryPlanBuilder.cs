using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Interfaces.BI;

/// <summary>
/// Query执行计划构建服务。
/// </summary>
public interface IQueryPlanBuilder
{
    /// <summary>
    /// 创建查询计划。
    /// </summary>
    /// <param name="requestedDataSourceId">
    /// 可选数据源约束。为 null 或 &lt;=0 时保持原有行为（数据源由元数据搜索推断）；
    /// 显式指定时约束元数据搜索与选表仅在该数据源范围内（必须在选表前生效），
    /// 从而保证 <see cref="QueryPlan.DataSourceId"/> 与请求的数据源一致。
    /// </param>
    Task<QueryPlan> BuildAsync(
        QueryIntent intent,
        long? requestedDataSourceId = null,
        IReadOnlyCollection<long>? authorizedDataSourceIds = null);

    /// <summary>
    /// 使用已经完成 Semantic Applicability Resolution 的绑定构建查询计划。
    /// Resolution 为 null 时保持原有行为。
    /// </summary>
    Task<QueryPlan> BuildAsync(
        QueryIntent intent,
        QueryPlanSemanticResolution? resolution);
}
