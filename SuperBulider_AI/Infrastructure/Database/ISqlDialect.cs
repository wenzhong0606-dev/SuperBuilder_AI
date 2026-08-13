namespace SuperBulider_AI.Infrastructure.Database;

/// <summary>
/// SQL数据库方言接口。
///
/// 用于处理不同数据库之间的SQL语法差异。
///
/// 支持:
///
/// SQL Server
/// MySQL
/// PostgreSQL
///
/// </summary>
public interface ISqlDialect
{


	/// <summary>
	/// 数据库类型名称。
	///
	/// SQLSERVER
	/// MYSQL
	/// POSTGRESQL
	///
	/// </summary>
	string Name
	{
		get;
	}





	/// <summary>
	/// 获取字段名称转义。
	///
	/// 例如:
	///
	/// SQL Server:
	/// [Amount]
	///
	/// MySQL:
	/// `Amount`
	///
	/// PostgreSQL:
	/// "Amount"
	///
	/// </summary>
	string EscapeIdentifier(
		string identifier);





	/// <summary>
	/// 获取分页SQL。
	///
	/// </summary>
	/// <param name="sql">
	/// 原始SQL
	/// </param>
	/// <param name="limit">
	/// 返回数量
	/// </param>
	string ApplyLimit(
		string sql,
		int limit);





	/// <summary>
	/// 参数名称。
	///
	/// 例如:
	///
	/// @p0
	///
	/// </summary>
	string GetParameterName(
		int index);



}