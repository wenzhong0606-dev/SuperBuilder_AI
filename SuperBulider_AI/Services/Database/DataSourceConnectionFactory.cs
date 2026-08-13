using System.Data.Common;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using SuperBulider_AI.Data;
using SuperBulider_AI.Interfaces.Database;


namespace SuperBulider_AI.Services.Database;

/// <summary>
/// 数据源连接工厂。
///
/// 负责:
///
/// DataSource
///
/// 转换为:
///
/// SqlConnection
/// MySqlConnection
/// NpgsqlConnection
///
/// </summary>
public class DataSourceConnectionFactory
	:
	IDataSourceConnectionFactory
{


	private readonly SuperBIContext _context;



	/// <summary>
	/// 构造函数。
	/// </summary>
	public DataSourceConnectionFactory(
		SuperBIContext context)
	{

		_context = context;

	}





	/// <summary>
	/// 创建数据库连接。
	/// </summary>
	public async Task<DbConnection>
		CreateAsync(
			long dataSourceId)
	{


		var dataSource =
			await _context.DataSources
			.FirstOrDefaultAsync(x =>
				x.Id == dataSourceId);





		if (dataSource == null)
		{
			throw new InvalidOperationException(
				$"不存在数据源:{dataSourceId}");
		}





		if (string.IsNullOrWhiteSpace(
			dataSource.ConnectionString))
		{
			throw new InvalidOperationException(
				$"数据源连接字符串为空:{dataSourceId}");
		}





		var dbType =
			dataSource.DbType?
			.ToUpper();





		return dbType switch
		{


			"SQLSERVER" =>
				new SqlConnection(
					dataSource.ConnectionString),



			"MYSQL" =>
				new MySqlConnection(
					dataSource.ConnectionString),



			"POSTGRESQL" =>
				new NpgsqlConnection(
					dataSource.ConnectionString),



			_ =>
				throw new NotSupportedException(
					$"不支持数据库类型:{dataSource.DbType}")

		};

	}



}