using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Npgsql;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Database;
using System.Data.Common;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// Source DB relationship diagnostics.
/// 与 LocalRuntimeDiagnosticsController 中的 SuperBIContext SQL Server FK 诊断不同，
/// 本 Controller 会根据 MetadataColumn.DataSourceId 通过 IDataSourceConnectionFactory
/// 连接真实业务数据库，读取 source DB 的物理 FK。
/// </summary>
[ApiController]
[Route("evaluation/local-runtime")]
public sealed class SourceDatabaseRelationDiagnosticsController : ControllerBase
{
    private readonly SuperBIContext _context;
    private readonly IDataSourceConnectionFactory _connectionFactory;

    public SourceDatabaseRelationDiagnosticsController(
        SuperBIContext context,
        IDataSourceConnectionFactory connectionFactory)
    {
        _context = context;
        _connectionFactory = connectionFactory;
    }

    [HttpGet("source-database-relations")]
    public async Task<ActionResult<object>> SourceDatabaseRelations(
        [FromQuery] string table = "wms_storage_receipt_info",
        [FromQuery] string column = "material_id",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(table) || string.IsNullOrWhiteSpace(column))
            return BadRequest(new { passed = false, reason = "table/column 不能为空。" });

        var metadata = await _context.MetadataColumns
            .AsNoTracking()
            .Include(x => x.MetadataTable)
            .Where(x => x.MetadataTable != null &&
                        x.MetadataTable.TableName == table &&
                        x.ColumnName == column)
            .Select(x => new
            {
                columnId = x.Id,
                metadataTableId = x.MetadataTableId,
                dataSourceId = x.MetadataTable!.DataSourceId,
                tenantId = x.MetadataTable.TenantId,
                tableName = x.MetadataTable.TableName,
                columnName = x.ColumnName
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (metadata == null)
        {
            return Ok(new
            {
                passed = false,
                input = new { table, column },
                metadata = new { found = false },
                sourceDatabase = new { error = "MetadataColumn 不存在，无法解析 DataSourceId。" }
            });
        }

        try
        {
            await using var connection = await _connectionFactory.CreateAsync(metadata.dataSourceId);
            await connection.OpenAsync(cancellationToken);

            var provider = GetProviderName(connection);
            var relations = await QueryRelationsAsync(
                connection,
                provider,
                table,
                column,
                cancellationToken);

            return Ok(new
            {
                passed = relations.Error == null,
                input = new { table, column },
                metadata = new
                {
                    found = true,
                    metadata.columnId,
                    metadata.metadataTableId,
                    metadata.dataSourceId,
                    metadata.tenantId
                },
                sourceDatabase = new
                {
                    provider,
                    database = connection.Database,
                    server = connection.DataSource,
                    foreignKeys = relations.ForeignKeys,
                    referencedColumns = relations.ReferencedColumns,
                    error = relations.Error,
                    note = "这是通过 MetadataColumn.DataSourceId 连接真实业务数据库得到的物理 FK，不是 SuperBIContext 的 Metadata 数据库关系。"
                }
            });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                passed = false,
                input = new { table, column },
                metadata = new
                {
                    found = true,
                    metadata.columnId,
                    metadata.metadataTableId,
                    metadata.dataSourceId,
                    metadata.tenantId
                },
                sourceDatabase = new
                {
                    provider = "unknown",
                    foreignKeys = Array.Empty<object>(),
                    referencedColumns = Array.Empty<object>(),
                    error = ex.Message,
                    exceptionType = ex.GetType().FullName
                }
            });
        }
    }

    private static string GetProviderName(DbConnection connection)
        => connection switch
        {
            MySqlConnection => "MYSQL",
            Microsoft.Data.SqlClient.SqlConnection => "SQLSERVER",
            NpgsqlConnection => "POSTGRESQL",
            _ => connection.GetType().FullName ?? connection.GetType().Name
        };

    private static async Task<RelationQueryResult> QueryRelationsAsync(
        DbConnection connection,
        string provider,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        var foreignKeys = new List<object>();
        var referencedColumns = new List<object>();

        try
        {
            var queries = provider switch
            {
                "MYSQL" => new[]
                {
                    (Direction: "outbound", Sql: @"
SELECT
    kcu.CONSTRAINT_NAME,
    kcu.TABLE_SCHEMA,
    kcu.TABLE_NAME,
    kcu.COLUMN_NAME,
    kcu.REFERENCED_TABLE_SCHEMA,
    kcu.REFERENCED_TABLE_NAME,
    kcu.REFERENCED_COLUMN_NAME
FROM information_schema.KEY_COLUMN_USAGE kcu
WHERE kcu.TABLE_SCHEMA = DATABASE()
  AND kcu.TABLE_NAME = @table
  AND kcu.COLUMN_NAME = @column
  AND kcu.REFERENCED_TABLE_NAME IS NOT NULL
ORDER BY kcu.CONSTRAINT_NAME, kcu.ORDINAL_POSITION;"),
                    (Direction: "inbound", Sql: @"
SELECT
    kcu.CONSTRAINT_NAME,
    kcu.TABLE_SCHEMA,
    kcu.TABLE_NAME,
    kcu.COLUMN_NAME,
    kcu.REFERENCED_TABLE_SCHEMA,
    kcu.REFERENCED_TABLE_NAME,
    kcu.REFERENCED_COLUMN_NAME
FROM information_schema.KEY_COLUMN_USAGE kcu
WHERE kcu.TABLE_SCHEMA = DATABASE()
  AND kcu.REFERENCED_TABLE_NAME = @table
  AND kcu.REFERENCED_COLUMN_NAME = @column
ORDER BY kcu.CONSTRAINT_NAME, kcu.ORDINAL_POSITION;")
                },
                "SQLSERVER" => new[]
                {
                    (Direction: "outbound", Sql: @"
SELECT
    fk.name AS CONSTRAINT_NAME,
    SCHEMA_NAME(tp.schema_id) AS TABLE_SCHEMA,
    tp.name AS TABLE_NAME,
    cp.name AS COLUMN_NAME,
    SCHEMA_NAME(tr.schema_id) AS REFERENCED_TABLE_SCHEMA,
    tr.name AS REFERENCED_TABLE_NAME,
    cr.name AS REFERENCED_COLUMN_NAME
FROM sys.foreign_key_columns fkc
JOIN sys.foreign_keys fk ON fk.object_id = fkc.constraint_object_id
JOIN sys.tables tp ON tp.object_id = fkc.parent_object_id
JOIN sys.columns cp ON cp.object_id = fkc.parent_object_id AND cp.column_id = fkc.parent_column_id
JOIN sys.tables tr ON tr.object_id = fkc.referenced_object_id
JOIN sys.columns cr ON cr.object_id = fkc.referenced_object_id AND cr.column_id = fkc.referenced_column_id
WHERE tp.name = @table AND cp.name = @column
ORDER BY fk.name, fkc.constraint_column_id;"),
                    (Direction: "inbound", Sql: @"
SELECT
    fk.name AS CONSTRAINT_NAME,
    SCHEMA_NAME(tp.schema_id) AS TABLE_SCHEMA,
    tp.name AS TABLE_NAME,
    cp.name AS COLUMN_NAME,
    SCHEMA_NAME(tr.schema_id) AS REFERENCED_TABLE_SCHEMA,
    tr.name AS REFERENCED_TABLE_NAME,
    cr.name AS REFERENCED_COLUMN_NAME
FROM sys.foreign_key_columns fkc
JOIN sys.foreign_keys fk ON fk.object_id = fkc.constraint_object_id
JOIN sys.tables tp ON tp.object_id = fkc.parent_object_id
JOIN sys.columns cp ON cp.object_id = fkc.parent_object_id AND cp.column_id = fkc.parent_column_id
JOIN sys.tables tr ON tr.object_id = fkc.referenced_object_id
JOIN sys.columns cr ON cr.object_id = fkc.referenced_object_id AND cr.column_id = fkc.referenced_column_id
WHERE tr.name = @table AND cr.name = @column
ORDER BY fk.name, fkc.constraint_column_id;")
                },
                "POSTGRESQL" => new[]
                {
                    (Direction: "outbound", Sql: @"
SELECT
    con.conname AS constraint_name,
    nsp.nspname AS table_schema,
    cls.relname AS table_name,
    att.attname AS column_name,
    rnsp.nspname AS referenced_table_schema,
    rcls.relname AS referenced_table_name,
    ratt.attname AS referenced_column_name
FROM pg_constraint con
JOIN pg_class cls ON cls.oid = con.conrelid
JOIN pg_namespace nsp ON nsp.oid = cls.relnamespace
JOIN pg_class rcls ON rcls.oid = con.confrelid
JOIN pg_namespace rnsp ON rnsp.oid = rcls.relnamespace
JOIN LATERAL unnest(con.conkey) WITH ORDINALITY AS cols(attnum, ord) ON true
JOIN LATERAL unnest(con.confkey) WITH ORDINALITY AS rcols(attnum, ord) ON rcols.ord = cols.ord
JOIN pg_attribute att ON att.attrelid = cls.oid AND att.attnum = cols.attnum
JOIN pg_attribute ratt ON ratt.attrelid = rcls.oid AND ratt.attnum = rcols.attnum
WHERE con.contype = 'f' AND cls.relname = @table AND att.attname = @column
ORDER BY con.conname, cols.ord;"),
                    (Direction: "inbound", Sql: @"
SELECT
    con.conname AS constraint_name,
    nsp.nspname AS table_schema,
    cls.relname AS table_name,
    att.attname AS column_name,
    rnsp.nspname AS referenced_table_schema,
    rcls.relname AS referenced_table_name,
    ratt.attname AS referenced_column_name
FROM pg_constraint con
JOIN pg_class cls ON cls.oid = con.conrelid
JOIN pg_namespace nsp ON nsp.oid = cls.relnamespace
JOIN pg_class rcls ON rcls.oid = con.confrelid
JOIN pg_namespace rnsp ON rnsp.oid = rcls.relnamespace
JOIN LATERAL unnest(con.conkey) WITH ORDINALITY AS cols(attnum, ord) ON true
JOIN LATERAL unnest(con.confkey) WITH ORDINALITY AS rcols(attnum, ord) ON rcols.ord = cols.ord
JOIN pg_attribute att ON att.attrelid = cls.oid AND att.attnum = cols.attnum
JOIN pg_attribute ratt ON ratt.attrelid = rcls.oid AND ratt.attnum = rcols.attnum
WHERE con.contype = 'f' AND rcls.relname = @table AND ratt.attname = @column
ORDER BY con.conname, cols.ord;")
                },
                _ => Array.Empty<(string Direction, string Sql)>()
            };

            if (queries.Length == 0)
                return new RelationQueryResult(foreignKeys, referencedColumns, $"不支持的数据源类型: {provider}");

            foreach (var query in queries)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = query.Sql;
                AddParameter(command, "@table", table);
                AddParameter(command, "@column", column);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var relation = ReadRelation(reader, query.Direction);
                    if (query.Direction == "outbound")
                        foreignKeys.Add(relation);
                    else
                        referencedColumns.Add(relation);
                }
            }

            return new RelationQueryResult(foreignKeys, referencedColumns, null);
        }
        catch (Exception ex)
        {
            return new RelationQueryResult(foreignKeys, referencedColumns, ex.Message);
        }
    }

    private static object ReadRelation(DbDataReader reader, string direction) => new
    {
        direction,
        constraintName = reader["CONSTRAINT_NAME"]?.ToString(),
        tableSchema = reader["TABLE_SCHEMA"]?.ToString(),
        tableName = reader["TABLE_NAME"]?.ToString(),
        columnName = reader["COLUMN_NAME"]?.ToString(),
        referencedTableSchema = reader["REFERENCED_TABLE_SCHEMA"]?.ToString(),
        referencedTableName = reader["REFERENCED_TABLE_NAME"]?.ToString(),
        referencedColumnName = reader["REFERENCED_COLUMN_NAME"]?.ToString()
    };

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record RelationQueryResult(
        List<object> ForeignKeys,
        List<object> ReferencedColumns,
        string? Error);
}
