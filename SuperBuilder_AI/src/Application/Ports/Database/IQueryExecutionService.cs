using SuperBuilder_AI.Models.BI;


namespace SuperBuilder_AI.Interfaces.Database;

/// <summary>
/// 查询执行服务。
///
/// 负责:
///
/// SqlQuery
///
/// ↓
///
/// 数据库执行
///
/// ↓
///
/// QueryResult
///
/// </summary>
public interface IQueryExecutionService
{


	/// <summary>
	/// 执行动态SQL查询。
	/// </summary>
	/// <param name="query">
	/// SQL查询对象。
	/// </param>
	/// <param name="dataSourceId">
	/// 数据源Id。
	/// </param>
	Task<QueryResult>
		ExecuteAsync(
			SqlQuery query,
			long dataSourceId);


}