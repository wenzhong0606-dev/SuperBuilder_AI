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
	/// <param name="dataSourceId">
	/// 查询使用的数据源Id。
	/// </param>
	/// <returns>
	/// 完整BI查询响应。
	/// </returns>
	Task<BIResponse> AskAsync(
		string question,
		long dataSourceId);
}