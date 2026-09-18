namespace SuperBuilder_AI.Infrastructure.Database;

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


	/// <summary>
	/// 构造三键物理限定名（catalog.schema.table）。
	///
	/// 各方言规则（§10.5 #2）：
	/// - SQL Server：catalog 非空 → [catalog].[schema].[table]；否则 [schema].[table]。
	/// - MySQL：catalog(=数据库) 非空 → `catalog`.`table`；DTO 的 schema 必须为空或等于 catalog，
	///   不一致视为无效物理键并拒绝。
	/// - PostgreSQL：仅 "schema"."table"（catalog 不进入限定名，跨 catalog 由
	///   <see cref="AssertCatalogResolvable"/> 在执行层拒绝）。
	/// </summary>
	string QualifyTable(
		string? catalog,
		string? schema,
		string table);


	/// <summary>
	/// PostgreSQL 跨 catalog 执行层校验（§10.5 #5）。
	///
	/// <paramref name="catalog"/> 非空且不等于当前执行连接库 <paramref name="currentCatalog"/>
	/// 时拒绝（PostgreSQL 无法在同一连接内跨数据库查询）。SQL Server / MySQL 支持跨 catalog
	/// 限定名，本方法为空操作。
	/// </summary>
	void AssertCatalogResolvable(
		string? catalog,
		string? currentCatalog);



}