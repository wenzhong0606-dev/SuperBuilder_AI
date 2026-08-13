using Dapper;
using MySqlConnector;
using SuperBulider_AI.Interfaces;
using SuperBulider_AI.Models.DTO;

namespace SuperBulider_AI.Services;

/// <summary>
/// MySQL 元数据读取器。
/// 实现 IDataSourceMetadataReader，从 MySQL 数据库的 information_schema 读取表和列的元数据信息。
/// </summary>
public class MySqlMetadataReader : IDataSourceMetadataReader
{
	/// <summary>
	/// 异步获取指定连接字符串下的表元数据列表。
	/// </summary>
	/// <param name="connectionString">目标数据库的连接字符串。</param>
	/// <returns>表元数据 DTO 列表。</returns>
	public async Task<List<TableMetadataDto>> GetTablesAsync(string connectionString)
	{
		await using var conn = new MySqlConnection(connectionString);
		await conn.OpenAsync();

		var sql =
			"""
			SELECT
				TABLE_NAME TableName,
				TABLE_COMMENT TableComment
			FROM information_schema.tables
			WHERE table_schema = DATABASE()
			ORDER BY TABLE_NAME
			""";

		var result = await conn.QueryAsync<TableMetadataDto>(sql);
		return result.AsList();
	}

	/// <summary>
	/// 异步获取指定连接字符串下的列元数据列表。
	/// </summary>
	/// <param name="connectionString">目标数据库的连接字符串。</param>
	/// <returns>列元数据 DTO 列表。</returns>
	public async Task<List<ColumnMetadataDto>> GetColumnsAsync(string connectionString)
	{
		await using var conn = new MySqlConnection(connectionString);
		await conn.OpenAsync();

		var sql =
			"""
			SELECT
				TABLE_NAME TableName,
				COLUMN_NAME ColumnName,
				COLUMN_COMMENT ColumnComment,
				DATA_TYPE DataType,
				CHARACTER_MAXIMUM_LENGTH Length,
				CASE WHEN IS_NULLABLE='YES' THEN 1 ELSE 0 END IsNullable,
				CASE WHEN COLUMN_KEY='PRI' THEN 1 ELSE 0 END IsPrimaryKey
			FROM information_schema.columns
			WHERE table_schema = DATABASE()
			ORDER BY TABLE_NAME, ORDINAL_POSITION
			""";

		var result = await conn.QueryAsync<ColumnMetadataDto>(sql);
		return result.AsList();
	}
}
