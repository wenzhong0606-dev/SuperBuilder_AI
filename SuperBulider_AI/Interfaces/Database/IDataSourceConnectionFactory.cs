using System.Data.Common;


namespace SuperBuilder_AI.Interfaces.Database;

/// <summary>
/// 数据源连接工厂。
///
/// 根据平台Metadata中的DataSource配置，
/// 动态创建业务数据库连接。
///
/// 支持:
///
/// SQLSERVER
/// MYSQL
/// POSTGRESQL
///
/// </summary>
public interface IDataSourceConnectionFactory
{


	/// <summary>
	/// 根据数据源Id创建数据库连接。
	///
	/// 数据来源:
	///
	/// SuperBIContext.DataSources
	///
	/// </summary>
	/// <param name="dataSourceId">
	/// 数据源Id。
	/// </param>
	/// <returns>
	/// 未打开的数据库连接。
	/// </returns>
	Task<DbConnection>
		CreateAsync(
			long dataSourceId);



}