using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
///
/// §10.1（数据源扫描优化 v10）：所有读取返回 CatalogName/SchemaName/ObjectKind 三键，
/// 供同名表跨 schema/跨库并存与 Ask 限定名查询；SQL Server 用 sys.tables UNION sys.views 覆盖视图；
/// 跨库扫描由 GetDatabasesAsync 枚举后逐库连接汇总。
/// </summary>
public class MySqlMetadataReader : IDataSourceMetadataReader
{
    public Task<List<TableMetadataDto>> GetTablesAsync(string connectionString, CancellationToken ct = default)
        => GetTablesAsync(connectionString, "MYSQL", ct);

    public Task<List<ColumnMetadataDto>> GetColumnsAsync(string connectionString, CancellationToken ct = default)
        => GetColumnsAsync(connectionString, "MYSQL", ct);

    public Task<List<TableMetadataDto>> GetTablesAsync(
        string connectionString,
        string? dbType,
        CancellationToken ct = default)
    {
        return NormalizeDbType(dbType) switch
        {
            "SQLSERVER" => GetSqlServerTablesAsync(connectionString, ct),
            "POSTGRESQL" => GetPostgreSqlTablesAsync(connectionString, ct),
            _ => GetMySqlTablesAsync(connectionString, ct)
        };
    }

    public Task<List<ColumnMetadataDto>> GetColumnsAsync(
        string connectionString,
        string? dbType,
        CancellationToken ct = default)
    {
        return NormalizeDbType(dbType) switch
        {
            "SQLSERVER" => GetSqlServerColumnsAsync(connectionString, null, ct),
            "POSTGRESQL" => GetPostgreSqlColumnsAsync(connectionString, null, ct),
            _ => GetMySqlColumnsAsync(connectionString, null, ct)
        };
    }

    /// <summary>
    /// 逐表列读取（§5 + §10.1）。给定表名集合时按表名过滤，单表失败不影响其他表；
    /// 空集合回退为全表读取。
    /// </summary>
    public Task<List<ColumnMetadataDto>> GetColumnsAsync(
        string connectionString,
        string? dbType,
        IEnumerable<string> tableNames,
        CancellationToken ct = default)
    {
        var list = tableNames as List<string> ?? new List<string>(tableNames);
        if (list.Count == 0)
            return GetColumnsAsync(connectionString, dbType, ct);

        return NormalizeDbType(dbType) switch
        {
            "SQLSERVER" => GetSqlServerColumnsAsync(connectionString, list, ct),
            "POSTGRESQL" => GetPostgreSqlColumnsAsync(connectionString, list, ct),
            _ => GetMySqlColumnsAsync(connectionString, list, ct)
        };
    }

    public Task<List<ForeignKeyMetadataDto>> GetForeignKeysAsync(string connectionString, CancellationToken ct = default)
        => GetForeignKeysAsync(connectionString, "MYSQL", ct);

    public Task<List<ForeignKeyMetadataDto>> GetForeignKeysAsync(
        string connectionString,
        string? dbType,
        CancellationToken ct = default)
    {
        return NormalizeDbType(dbType) switch
        {
            "SQLSERVER" => GetSqlServerForeignKeysAsync(connectionString, ct),
            "POSTGRESQL" => GetPostgreSqlForeignKeysAsync(connectionString, ct),
            _ => GetMySqlForeignKeysAsync(connectionString, ct)
        };
    }

    /// <summary>
    /// 枚举目标实例下可扫描的数据库（§10.1 跨库扫描）。排除系统库；
    /// 单连接默认仅扫连接串指定数据库（调用方用于逐库替换连接串汇总）。
    /// </summary>
    public Task<List<string>> GetDatabasesAsync(
        string connectionString,
        string? dbType,
        CancellationToken ct = default)
    {
        return NormalizeDbType(dbType) switch
        {
            "SQLSERVER" => GetSqlServerDatabasesAsync(connectionString, ct),
            "POSTGRESQL" => GetPostgreSqlDatabasesAsync(connectionString, ct),
            _ => GetMySqlDatabasesAsync(connectionString, ct)
        };
    }

    private static string NormalizeDbType(string? dbType)
        => (dbType ?? "MYSQL").Trim().ToUpperInvariant();

    // ---- MySQL ----

    private static async Task<List<TableMetadataDto>> GetMySqlTablesAsync(string connectionString, CancellationToken ct)
    {
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT
                TABLE_SCHEMA CatalogName,
                TABLE_SCHEMA SchemaName,
                TABLE_NAME TableName,
                CASE TABLE_TYPE WHEN 'VIEW' THEN 1 ELSE 0 END ObjectKind,
                TABLE_COMMENT TableComment
            FROM information_schema.tables
            WHERE table_schema = DATABASE()
            ORDER BY TABLE_NAME
            """;

        return (await conn.QueryAsync<TableMetadataDto>(new CommandDefinition(sql, cancellationToken: ct))).AsList();
    }

    private static async Task<List<ColumnMetadataDto>> GetMySqlColumnsAsync(
        string connectionString, IReadOnlyCollection<string>? tableNames, CancellationToken ct)
    {
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync(ct);

        var sql = """
            SELECT
                TABLE_SCHEMA CatalogName,
                TABLE_SCHEMA SchemaName,
                TABLE_NAME TableName,
                COLUMN_NAME ColumnName,
                COLUMN_COMMENT ColumnComment,
                DATA_TYPE DataType,
                CHARACTER_MAXIMUM_LENGTH Length,
                CASE WHEN IS_NULLABLE='YES' THEN 1 ELSE 0 END IsNullable,
                CASE WHEN COLUMN_KEY='PRI' THEN 1 ELSE 0 END IsPrimaryKey
            FROM information_schema.columns
            WHERE table_schema = DATABASE()
            """;
        object? param = null;
        if (tableNames is { Count: > 0 })
        {
            sql += "\n  AND TABLE_NAME IN @tableNames";
            param = new { tableNames };
        }
        sql += "\nORDER BY TABLE_NAME, ORDINAL_POSITION";

        return (await conn.QueryAsync<ColumnMetadataDto>(new CommandDefinition(sql, param, cancellationToken: ct))).AsList();
    }

    private static async Task<List<ForeignKeyMetadataDto>> GetMySqlForeignKeysAsync(string connectionString, CancellationToken ct)
    {
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT
                kcu.TABLE_SCHEMA CatalogName,
                kcu.TABLE_SCHEMA SchemaName,
                kcu.TABLE_NAME TableName,
                kcu.COLUMN_NAME ColumnName,
                kcu.REFERENCED_TABLE_SCHEMA ReferencedCatalogName,
                kcu.REFERENCED_TABLE_SCHEMA ReferencedSchemaName,
                kcu.REFERENCED_TABLE_NAME ReferencedTableName,
                kcu.REFERENCED_COLUMN_NAME ReferencedColumnName
            FROM information_schema.KEY_COLUMN_USAGE kcu
            INNER JOIN information_schema.REFERENTIAL_CONSTRAINTS rc
                ON rc.CONSTRAINT_SCHEMA = kcu.TABLE_SCHEMA
               AND rc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
            WHERE kcu.TABLE_SCHEMA = DATABASE()
              AND kcu.REFERENCED_TABLE_NAME IS NOT NULL
            """;
        return (await conn.QueryAsync<ForeignKeyMetadataDto>(new CommandDefinition(sql, cancellationToken: ct))).AsList();
    }

    private static async Task<List<string>> GetMySqlDatabasesAsync(string connectionString, CancellationToken ct)
    {
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync(ct);
        const string sql = """
            SELECT SCHEMA_NAME FROM information_schema.schemata
            WHERE SCHEMA_NAME NOT IN ('information_schema','mysql','performance_schema','sys')
            ORDER BY SCHEMA_NAME
            """;
        return (await conn.QueryAsync<string>(new CommandDefinition(sql, cancellationToken: ct))).AsList();
    }

    // ---- SQL Server ----

    private static async Task<List<TableMetadataDto>> GetSqlServerTablesAsync(string connectionString, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT
                DB_NAME() CatalogName,
                SCHEMA_NAME(o.schema_id) SchemaName,
                o.name AS TableName,
                CASE WHEN o.type = 'V' THEN 1 ELSE 0 END ObjectKind,
                CAST(ep.value AS nvarchar(4000)) AS TableComment
            FROM (SELECT object_id, schema_id, name, type, is_ms_shipped FROM sys.tables
                  UNION ALL
                  SELECT object_id, schema_id, name, type, is_ms_shipped FROM sys.views) o
            LEFT JOIN sys.extended_properties ep
              ON ep.major_id = o.object_id
             AND ep.minor_id = 0
             AND ep.name = 'MS_Description'
            WHERE o.is_ms_shipped = 0
            ORDER BY o.name
            """;

        return (await conn.QueryAsync<TableMetadataDto>(new CommandDefinition(sql, cancellationToken: ct))).AsList();
    }

    private static async Task<List<ColumnMetadataDto>> GetSqlServerColumnsAsync(
        string connectionString, IReadOnlyCollection<string>? tableNames, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);

        var sql = """
            SELECT
                DB_NAME() CatalogName,
                SCHEMA_NAME(o.schema_id) SchemaName,
                o.name AS TableName,
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
            FROM (SELECT object_id, schema_id, name, type FROM sys.tables
                  UNION ALL
                  SELECT object_id, schema_id, name, type FROM sys.views) o
            INNER JOIN sys.columns c ON c.object_id = o.object_id
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
            WHERE o.is_ms_shipped = 0
            """;
        object? param = null;
        if (tableNames is { Count: > 0 })
        {
            sql += "\n  AND o.name IN @tableNames";
            param = new { tableNames };
        }
        sql += "\nORDER BY o.name, c.column_id";

        return (await conn.QueryAsync<ColumnMetadataDto>(new CommandDefinition(sql, param, cancellationToken: ct))).AsList();
    }

    private static async Task<List<ForeignKeyMetadataDto>> GetSqlServerForeignKeysAsync(string connectionString, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT
                DB_NAME() CatalogName,
                SCHEMA_NAME(tp.schema_id) SchemaName,
                tp.name AS TableName,
                cc.name AS ColumnName,
                DB_NAME() ReferencedCatalogName,
                SCHEMA_NAME(tr.schema_id) ReferencedSchemaName,
                tr.name AS ReferencedTableName,
                rc.name AS ReferencedColumnName
            FROM sys.foreign_key_columns fkc
            INNER JOIN sys.foreign_keys fk ON fk.object_id = fkc.constraint_object_id
            INNER JOIN sys.tables tp ON tp.object_id = fkc.parent_object_id
            INNER JOIN sys.columns cc ON cc.object_id = tp.object_id AND cc.column_id = fkc.parent_column_id
            INNER JOIN sys.tables tr ON tr.object_id = fkc.referenced_object_id
            INNER JOIN sys.columns rc ON rc.object_id = tr.object_id AND rc.column_id = fkc.referenced_column_id
            """;
        return (await conn.QueryAsync<ForeignKeyMetadataDto>(new CommandDefinition(sql, cancellationToken: ct))).AsList();
    }

    private static async Task<List<string>> GetSqlServerDatabasesAsync(string connectionString, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        const string sql = """
            SELECT name FROM sys.databases
            WHERE name NOT IN ('master','tempdb','model','msdb')
              AND state_desc = 'ONLINE'
            ORDER BY name
            """;
        return (await conn.QueryAsync<string>(new CommandDefinition(sql, cancellationToken: ct))).AsList();
    }

    // ---- PostgreSQL ----

    private static async Task<List<TableMetadataDto>> GetPostgreSqlTablesAsync(string connectionString, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

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

        return (await conn.QueryAsync<TableMetadataDto>(new CommandDefinition(sql, cancellationToken: ct))).AsList();
    }

    private static async Task<List<ColumnMetadataDto>> GetPostgreSqlColumnsAsync(
        string connectionString, IReadOnlyCollection<string>? tableNames, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        var sql = """
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
            """;
        object? param = null;
        if (tableNames is { Count: > 0 })
        {
            sql += "\n  AND cls.relname IN @tableNames";
            param = new { tableNames };
        }
        sql += "\nORDER BY cls.relname, a.attnum";

        return (await conn.QueryAsync<ColumnMetadataDto>(new CommandDefinition(sql, param, cancellationToken: ct))).AsList();
    }

    private static async Task<List<ForeignKeyMetadataDto>> GetPostgreSqlForeignKeysAsync(string connectionString, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT
                c.relname AS "TableName",
                a.attname AS "ColumnName",
                c_r.relname AS "ReferencedTableName",
                a_r.attname AS "ReferencedColumnName"
            FROM pg_constraint con
            JOIN pg_class c ON c.oid = con.conrelid
            JOIN pg_class c_r ON c_r.oid = con.confrelid
            JOIN pg_attribute a ON a.attnum = ANY(con.conkey) AND a.attrelid = c.oid
            JOIN pg_attribute a_r ON a_r.attnum = ANY(con.confkey) AND a_r.attrelid = c_r.oid
            WHERE con.contype = 'f'
            """;
        return (await conn.QueryAsync<ForeignKeyMetadataDto>(new CommandDefinition(sql, cancellationToken: ct))).AsList();
    }

    private static async Task<List<string>> GetPostgreSqlDatabasesAsync(string connectionString, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        const string sql = """
            SELECT datname FROM pg_database
            WHERE datistemplate = false
              AND datallowconn = true
              AND datname NOT IN ('postgres', 'template0', 'template1')
            ORDER BY datname
            """;
        return (await conn.QueryAsync<string>(new CommandDefinition(sql, cancellationToken: ct))).AsList();
    }
}
