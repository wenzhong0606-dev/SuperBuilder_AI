using SuperBulider_AI.Models.BI;


namespace SuperBulider_AI.Interfaces.BI;

/// <summary>
/// Query执行计划构建服务。
///
/// 负责:
///
/// QueryIntent
///
/// 转换为:
///
/// QueryPlan
///
/// </summary>
public interface IQueryPlanBuilder
{


	/// <summary>
	/// 创建查询计划。
	/// </summary>
	Task<QueryPlan> BuildAsync(
		QueryIntent intent);


}
