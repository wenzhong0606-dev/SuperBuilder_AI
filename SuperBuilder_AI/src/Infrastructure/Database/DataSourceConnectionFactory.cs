using System.Data.Common;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Database;
using SuperBuilder_AI.Interfaces.Identity;


namespace SuperBuilder_AI.Services.Database;

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
	private readonly IDataSourceAuthorizationService? _authorization;
	private readonly IDataSourceExecutionIdentityAccessor? _executionIdentity;



	/// <summary>
	/// 构造函数。
	/// </summary>
	public DataSourceConnectionFactory(
		SuperBIContext context,
		IDataSourceAuthorizationService? authorization = null,
		IDataSourceExecutionIdentityAccessor? executionIdentity = null)
	{

		_context = context;
		_authorization = authorization;
		_executionIdentity = executionIdentity;

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

		var caller = _executionIdentity?.Current;
		if (caller is not null &&
			(dataSource.TenantId != caller.TenantId || dataSource.Enabled != true ||
			 _authorization is null ||
			 !await _authorization.IsAuthorizedAsync(caller.TenantId, caller.UserId, dataSourceId)))
		{
			throw new UnauthorizedAccessException("当前账号无权执行所选数据源。");
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
