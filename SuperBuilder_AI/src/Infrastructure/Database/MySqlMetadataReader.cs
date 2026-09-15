using Dapper;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Models.DTO;

namespace SuperBuilder_AI.Services;

/// <summary>
/// 多数据库元数据读取器。
/// 保留历史类名以避免大范围 DI/测试改动；实际按 DbType 支持 MySQL / SQL Server / PostgreSQL。
/// </summary>
public class MySqlMetadataReader : IDataSourceMetadataReader
{
    public Task<List<TableMetadataDto>> GetTablesAsync(string connectionString)
        => GetTablesAsync(connectionString, "MYSQL");

    public Task<List<ColumnMetadataDto>> GetColumnsAsync(string connectionString)
        => GetColumnsAsync(connectionString, "MYSQL");

    public async Task<List<TableMetadataDto>> GetTablesAsync(
        string connectionString,
        string? dbType)
    {
        return NormalizeDbType(dbType) switch
        {
            "SQLSERVER" => await GetSqlServerTablesAsync(connectionString),
            "POSTGRESQL" => await GetPostgreSqlTablesAsync(connectionString),
            _ => await GetMySqlTablesAsync(connectionString)
        };
    }

    public async Task<List<ColumnMetadataDto>> GetColumnsAsync(
        string connectionString,
        string? dbType)
    {
        return NormalizeDbType(dbType) switch
        {
            "SQLSERVER" => await GetSqlServerColumnsAsync(connectionString),
            "POSTGRESQL" => await GetPostgreSqlColumnsAsync(connectionString),
            _ => await GetMySqlColumnsAsync(connectionString)
        };
    }

    private static string NormalizeDbType(string? dbType)
        => (dbType ?? "MYSQL").Trim().ToUpperInvariant();

    private static async Task<List<TableMetadataDto>> GetMySqlTablesAsync(string connectionString)
    {
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync();

        const string sql = """
            SELECT
                TABLE_NAME TableName,
                TABLE_COMMENT TableComment
            FROM information_schema.tables
            WHERE table_schema = DATABASE()
              AND TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_NAME
            """;

        return (await conn.QueryAsync<TableMetadataDto>(sql)).AsList();
    }

    private static async Task<List<ColumnMetadataDto>> GetMySqlColumnsAsync(string connectionString)
    {
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync();

        const string sql = """
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

        return (await conn.QueryAsync<ColumnMetadataDto>(sql)).AsList();
    }

    private static async Task<List<TableMetadataDto>> GetSqlServerTablesAsync(string connectionString)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        const string sql = """
            SELECT
                t.name AS TableName,
                CAST(ep.value AS nvarchar(4000)) AS TableComment
            FROM sys.tables t
            LEFT JOIN sys.extended_properties ep
              ON ep.major_id = t.object_id
             AND ep.minor_id = 0
             AND ep.name = 'MS_Description'
            WHERE t.is_ms_shipped = 0
            ORDER BY t.name
            """;

        return (await conn.QueryAsync<TableMetadataDto>(sql)).AsList();
    }

    private static async Task<List<ColumnMetadataDto>> GetSqlServerColumnsAsync(string connectionString)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        const string sql = """
            SELECT
                t.name AS TableName,
                c.name AS ColumnName,
                CAST(ep.value AS nvarchar(4000)) AS ColumnComment,
                ty.name AS DataType,
                CASE
                    WHEN ty.name IN ('nvarchar','nchar') AND c.max_length > 0 THEN c.max_length / 2
                    WHEN c.max_length < 0 THEN NULL
                    ELSE c.max_length
                END AS Length,
                c.is_nullable AS IsNullable,
                CASE WHEN pk.column_id IS NULL THEN 0 ELSE 1 END AS IsPrimaryKey
            FROM sys.tables t
            INNER JOIN sys.columns c ON c.object_id = t.object_id
            INNER JOIN sys.types ty ON ty.user_type_id = c.user_type_id
            LEFT JOIN sys.extended_properties ep
              ON ep.major_id = c.object_id
             AND ep.minor_id = c.column_id
             AND ep.name = 'MS_Description'
            LEFT JOIN (
                SELECT ic.object_id, ic.column_id
                FROM sys.indexes i
                INNER JOIN sys.index_columns ic
                  ON ic.object_id = i.object_id
                 AND ic.index_id = i.index_id
                WHERE i.is_primary_key = 1
            ) pk
              ON pk.object_id = c.object_id
             AND pk.column_id = c.column_id
            WHERE t.is_ms_shipped = 0
            ORDER BY t.name, c.column_id
            """;

        return (await conn.QueryAsync<ColumnMetadataDto>(sql)).AsList();
    }

    private static async Task<List<TableMetadataDto>> GetPostgreSqlTablesAsync(string connectionString)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        const string sql = """
            SELECT
                c.relname AS "TableName",
                obj_description(c.oid, 'pg_class') AS "TableComment"
            FROM pg_class c
            INNER JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relkind = 'r'
              AND n.nspname NOT IN ('pg_catalog', 'information_schema')
            ORDER BY c.relname
            """;

        return (await conn.QueryAsync<TableMetadataDto>(sql)).AsList();
    }

    private static async Task<List<ColumnMetadataDto>> GetPostgreSqlColumnsAsync(string connectionString)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        const string sql = """
            SELECT
                cls.relname AS "TableName",
                a.attname AS "ColumnName",
                col_description(a.attrelid, a.attnum) AS "ColumnComment",
                format_type(a.atttypid, a.atttypmod) AS "DataType",
                CASE WHEN a.atttypmod > 0 THEN a.atttypmod - 4 ELSE NULL END AS "Length",
                NOT a.attnotnull AS "IsNullable",
                EXISTS (
                    SELECT 1
                    FROM pg_index i
                    WHERE i.indrelid = a.attrelid
                      AND i.indisprimary
                      AND a.attnum = ANY(i.indkey)
                ) AS "IsPrimaryKey"
            FROM pg_attribute a
            INNER JOIN pg_class cls ON cls.oid = a.attrelid
            INNER JOIN pg_namespace n ON n.oid = cls.relnamespace
            WHERE a.attnum > 0
              AND NOT a.attisdropped
              AND cls.relkind = 'r'
              AND n.nspname NOT IN ('pg_catalog', 'information_schema')
            ORDER BY cls.relname, a.attnum
            """;

        return (await conn.QueryAsync<ColumnMetadataDto>(sql)).AsList();
    }
}
