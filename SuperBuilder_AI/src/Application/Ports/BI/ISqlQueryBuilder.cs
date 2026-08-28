using SuperBuilder_AI.Infrastructure.Database;
using SuperBuilder_AI.Models.BI;


namespace SuperBuilder_AI.Interfaces.BI;

/// <summary>
/// SQL查询构建器。
///
/// 负责:
///
/// QueryPlan
///
/// 转换为:
///
/// SqlQuery
///
/// 不负责:
///
/// 1. 数据库连接
/// 2. SQL执行
///
/// </summary>
public interface ISqlQueryBuilder
{


	/// <summary>
	/// 根据查询计划生成SQL。
	/// </summary>
	/// <param name="plan">
	/// 查询执行计划。
	/// </param>
	/// <param name="dialect">
	/// 数据库SQL方言。
	/// </param>
	/// <returns>
	/// 可执行SQL及参数。
	/// </returns>
	Task<SqlQuery>
		BuildAsync(
			QueryPlan plan,
			ISqlDialect dialect);


}