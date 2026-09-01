using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Interfaces.BI;

/// <summary>
/// AI BI会话编排服务。
///
/// 负责将各个AI BI服务串联起来。
///
/// 完整流程:
///
/// 用户问题
///     ↓
/// QueryUnderstandingService
///     ↓
/// QueryIntent
///     ↓
/// QueryPlanBuilder
///     ↓
/// QueryPlan
///     ↓
/// SqlDialectResolver
///     ↓
/// SqlQueryBuilder
///     ↓
/// SqlQuery
///     ↓
/// QueryExecutionService
///     ↓
/// QueryResult
///     ↓
/// ResultUnderstandingService
///     ↓
/// QueryAnswer
///
/// 该服务只负责流程编排，
/// 不负责具体的AI理解、SQL生成或数据库执行逻辑。
/// </summary>
public interface IBIConversationService
{
	/// <summary>
	/// 执行一次完整的AI BI查询。
	/// </summary>
	/// <param name="question">
	/// 用户自然语言问题。
	/// </param>
	/// <param name="tenantId">
	/// 执行该查询的租户Id（由鉴权令牌的 <c>tid</c> 声明驱动，不是数据源Id）。
	/// </param>
	/// <param name="requestedDataSourceId">
	/// 可选数据源约束。为 null 或 &lt;=0 时表示由系统根据问题推断数据源（默认行为）；
	/// 显式指定时查询结果必须限定在该数据源内，且缓存键、执行连接与计划
	/// <see cref="QueryPlan.DataSourceId"/> 必须同源。P0-01：修复缓存键与执行连接不同源缺陷。
	/// </param>
	/// <returns>
	/// 完整BI查询响应。
	/// </returns>
	Task<BIResponse> AskAsync(
		string question,
		long tenantId,
		long? requestedDataSourceId = null,
		IReadOnlyCollection<long>? authorizedDataSourceIds = null);
}
