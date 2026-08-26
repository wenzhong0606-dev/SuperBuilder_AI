using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Database;
using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace SuperBuilder_AI.Controllers;

[ApiController]
[Route("evaluation/local-runtime")]
public sealed class LocalRuntimeDiagnosticsController : ControllerBase
{
    private readonly SuperBIContext _context;
    private readonly IDataSourceConnectionFactory _dataSourceConnectionFactory;

    public LocalRuntimeDiagnosticsController(
        SuperBIContext context,
        IDataSourceConnectionFactory dataSourceConnectionFactory)
    {
        _context = context;
        _dataSourceConnectionFactory = dataSourceConnectionFactory;
    }

    /// <summary>
    /// 本次根因诊断唯一测试入口：
    /// MetadataColumn -> DataSource #2 -> 安全连接信息 -> DNS/TCP -> DB Open/SELECT 1 -> FK。
    /// 不返回连接字符串、密码或完整认证信息。
    /// </summary>
    [HttpGet("source-database-diagnostic")]
    public async Task<ActionResult<object>> SourceDatabaseDiagnostic(
        [FromQuery] string table = "wms_storage_receipt_info",
        [FromQuery] string column = "material_id",
        [FromQuery] long dataSourceId = 2,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(table) || string.IsNullOrWhiteSpace(column))
            return BadRequest(new { passed = false, reason = "table/column 不能为空。" });

        var metadata = await _context.MetadataColumns
            .AsNoTracking()
            .Include(x => x.MetadataTable)
            .Include(x => x.Semantic)
            .Where(x => x.MetadataTable != null &&
                        x.MetadataTable.TableName == table &&
                        x.ColumnName == column)
            .Select(x => new
            {
                columnId = x.Id,
                metadataTableId = x.MetadataTableId,
                tableName = x.MetadataTable!.TableName,
                tableComment = x.MetadataTable.TableComment,
                metadataDataSourceId = x.MetadataTable.DataSourceId,
                tenantId = x.MetadataTable.TenantId,
                columnName = x.ColumnName,
                columnComment = x.ColumnComment,
                dataType = x.DataType,
                isPrimaryKey = x.IsPrimaryKey,
                businessKey = x.BusinessKey,
                semantic = x.Semantic == null ? null : new
                {
                    id = x.Semantic.Id,
                    metadataColumnId = x.Semantic.MetadataColumnId,
                    businessMeaning = x.Semantic.BusinessMeaning,
                    keywords = x.Semantic.Keywords,
                    synonyms = x.Semantic.Synonyms,
                    exampleQuestions = x.Semantic.ExampleQuestions,
                    businessDomain = x.Semantic.BusinessDomain,
                    confidence = x.Semantic.Confidence,
                    source = x.Semantic.Source
                }
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (metadata == null)
            return NotFound(new { passed = false, table, column, reason = "MetadataColumn 未找到。" });

        var source = await _context.DataSources
            .AsNoTracking()
            .Where(x => x.Id == dataSourceId)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.DbType,
                x.ConnectionString,
                x.Enabled,
                x.TenantId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (source == null)
            return NotFound(new { passed = false, table, column, dataSourceId, reason = "DataSource 不存在。" });

        var safe = BuildSafeConnectionInfo(source.ConnectionString, source.DbType);
        var tcp = await CheckTcpAsync(safe.Host, safe.Port, cancellationToken);
        var db = await CheckDbConnectionAsync(dataSourceId, cancellationToken);

        var relations = db.Passed
            ? await QueryRelationsAsync(dataSourceId, table, column, cancellationToken)
            : new RelationResult(new List<object>(), new List<object>(), "DB connection 未成功，跳过 FK 查询。");

        return Ok(new
        {
            passed = metadata.metadataDataSourceId == dataSourceId && db.Passed && relations.Error == null,
            input = new { table, column, dataSourceId },
            metadata = new
            {
                metadataColumnFound = true,
                columnId = metadata.columnId,
                metadataTableId = metadata.metadataTableId,
                dataSourceId = metadata.metadataDataSourceId,
                tenantId = metadata.tenantId,
                columnName = metadata.columnName,
                columnComment = metadata.columnComment,
                dataType = metadata.dataType,
                isPrimaryKey = metadata.isPrimaryKey,
                businessKey = metadata.businessKey,
                semantic = metadata.semantic
            },
            dataSource = new
            {
                id = source.Id,
                name = source.Name,
                dbType = source.DbType,
                enabled = source.Enabled,
                tenantId = source.TenantId,
                connection = safe
            },
            tcp,
            dbConnection = db,
            relationship = new
            {
                foreignKeys = relations.ForeignKeys,
                referencedColumns = relations.ReferencedColumns,
                error = relations.Error,
                note = "foreignKeys 表示 material_id 引用的主表；referencedColumns 表示其他表引用 material_id。"
            }
        });
    }

    private async Task<DataSourceConnectionCheck> CheckDbConnectionAsync(long dataSourceId, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var connection = await _dataSourceConnectionFactory.CreateAsync(dataSourceId);
            var before = connection.State.ToString();
            await connection.OpenAsync(cancellationToken);
            var after = connection.State.ToString();

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            var value = await command.ExecuteScalarAsync(cancellationToken);

            return new DataSourceConnectionCheck(
                true,
                sw.ElapsedMilliseconds,
                before,
                after,
                connection.Database,
                connection.DataSource,
                value?.ToString(),
                null);
        }
        catch (Exception ex)
        {
            return new DataSourceConnectionCheck(
                false,
                sw.ElapsedMilliseconds,
                null,
                null,
                null,
                null,
                null,
                FlattenException(ex));
        }
    }

    private static SafeConnectionInfo BuildSafeConnectionInfo(string? connectionString, string? dbType)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return new SafeConnectionInfo(dbType, null, null, null, false, "连接字符串为空。", null);

        try
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            var server = GetValue(builder, "Server")
                ?? GetValue(builder, "Data Source")
                ?? GetValue(builder, "Host")
                ?? GetValue(builder, "Address");
            var database = GetValue(builder, "Database") ?? GetValue(builder, "Initial Catalog");
            var parsed = ParseHostPort(server, dbType);
            return new SafeConnectionInfo(dbType, parsed.Host, parsed.Port, database, true, null, parsed.OriginalServer);
        }
        catch (Exception ex)
        {
            return new SafeConnectionInfo(dbType, null, null, null, false, $"连接字符串解析失败: {ex.Message}", null);
        }
    }

    private static string? GetValue(DbConnectionStringBuilder builder, string key)
    {
        foreach (var item in builder)
            if (string.Equals(item.Key?.ToString(), key, StringComparison.OrdinalIgnoreCase))
                return item.Value?.ToString();
        return null;
    }

    private static (string? Host, int? Port, string? OriginalServer) ParseHostPort(string? server, string? dbType)
    {
        if (string.IsNullOrWhiteSpace(server)) return (null, null, null);
        var original = server;
        var value = server.Trim();
        var defaultPort = string.Equals(dbType, "MYSQL", StringComparison.OrdinalIgnoreCase) ? 3306
            : string.Equals(dbType, "POSTGRESQL", StringComparison.OrdinalIgnoreCase) ? 5432 : 1433;

        if (value.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase)) value = value[4..];
        var slash = value.IndexOf('\\');
        if (slash >= 0) value = value[..slash];

        if (value.StartsWith("[") && value.Contains(']'))
        {
            var end = value.IndexOf(']');
            var host = value[1..end];
            var port = defaultPort;
            if (end + 1 < value.Length && value[end + 1] == ':' && int.TryParse(value[(end + 2)..], out var p)) port = p;
            return (host, port, original);
        }

        var colon = value.LastIndexOf(':');
        if (colon > 0 && int.TryParse(value[(colon + 1)..], out var explicitPort))
            return (value[..colon], explicitPort, original);

        return (value, defaultPort, original);
    }

    private static async Task<TcpDiagnostic> CheckTcpAsync(string? host, int? port, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(host) || !port.HasValue)
            return new TcpDiagnostic(false, host, port, null, null, "无法从连接字符串解析 TCP host/port。", null);

        var sw = Stopwatch.StartNew();
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
            var results = new List<object>();
            foreach (var address in addresses.Take(8))
            {
                using var client = new TcpClient(address.AddressFamily);
                try
                {
                    await client.ConnectAsync(address, port.Value, cancellationToken);
                    results.Add(new { address = address.ToString(), reachable = true });
                    return new TcpDiagnostic(true, host, port, sw.ElapsedMilliseconds, address.ToString(), null, results);
                }
                catch (Exception ex)
                {
                    results.Add(new { address = address.ToString(), reachable = false, errorType = ex.GetType().Name, error = ex.Message });
                }
            }
            return new TcpDiagnostic(false, host, port, sw.ElapsedMilliseconds, null, "DNS 解析成功，但 TCP 连接失败。", results);
        }
        catch (Exception ex)
        {
            return new TcpDiagnostic(false, host, port, sw.ElapsedMilliseconds, null, FlattenException(ex), null);
        }
    }

    private async Task<RelationResult> QueryRelationsAsync(long dataSourceId, string table, string column, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _dataSourceConnectionFactory.CreateAsync(dataSourceId);
            await connection.OpenAsync(cancellationToken);
            var type = DetectDatabaseType(connection);
            var foreignKeys = new List<object>();
            var referenced = new List<object>();

            switch (type)
            {
                case "SQLSERVER":
                    await ReadRelationsAsync(connection, @"
SELECT fk.name constraint_name, sch_from.name from_schema, t_from.name from_table, c_from.name from_column,
       sch_to.name referenced_schema, t_to.name referenced_table, c_to.name referenced_column
FROM sys.foreign_key_columns fkc
JOIN sys.foreign_keys fk ON fk.object_id=fkc.constraint_object_id
JOIN sys.tables t_from ON t_from.object_id=fkc.parent_object_id
JOIN sys.schemas sch_from ON sch_from.schema_id=t_from.schema_id
JOIN sys.columns c_from ON c_from.object_id=fkc.parent_object_id AND c_from.column_id=fkc.parent_column_id
JOIN sys.tables t_to ON t_to.object_id=fkc.referenced_object_id
JOIN sys.schemas sch_to ON sch_to.schema_id=t_to.schema_id
JOIN sys.columns c_to ON c_to.object_id=fkc.referenced_object_id AND c_to.column_id=fkc.referenced_column_id
WHERE (t_from.name=@table AND c_from.name=@column) OR (t_to.name=@table AND c_to.name=@column)", table, column, foreignKeys, referenced, cancellationToken);
                    break;
                case "MYSQL":
                    await ReadRelationsAsync(connection, @"
SELECT CONSTRAINT_NAME constraint_name, TABLE_SCHEMA from_schema, TABLE_NAME from_table, COLUMN_NAME from_column,
       REFERENCED_TABLE_SCHEMA referenced_schema, REFERENCED_TABLE_NAME referenced_table, REFERENCED_COLUMN_NAME referenced_column
FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
WHERE ((TABLE_NAME=@table AND COLUMN_NAME=@column) OR (REFERENCED_TABLE_NAME=@table AND REFERENCED_COLUMN_NAME=@column))
  AND REFERENCED_TABLE_NAME IS NOT NULL", table, column, foreignKeys, referenced, cancellationToken);
                    break;
                case "POSTGRESQL":
                    await ReadRelationsAsync(connection, @"
SELECT tc.constraint_name, kcu.table_schema from_schema, kcu.table_name from_table, kcu.column_name from_column,
       ccu.table_schema referenced_schema, ccu.table_name referenced_table, ccu.column_name referenced_column
FROM information_schema.table_constraints tc
JOIN information_schema.key_column_usage kcu ON tc.constraint_name=kcu.constraint_name AND tc.table_schema=kcu.table_schema
JOIN information_schema.constraint_column_usage ccu ON ccu.constraint_name=tc.constraint_name AND ccu.table_schema=tc.table_schema
WHERE tc.constraint_type='FOREIGN KEY'
  AND ((kcu.table_name=@table AND kcu.column_name=@column) OR (ccu.table_name=@table AND ccu.column_name=@column))", table, column, foreignKeys, referenced, cancellationToken);
                    break;
                default:
                    return new RelationResult(foreignKeys, referenced, $"不支持的 source DB 类型: {type}");
            }

            return new RelationResult(foreignKeys, referenced, null);
        }
        catch (Exception ex)
        {
            return new RelationResult(new List<object>(), new List<object>(), $"FK 查询失败: {FlattenException(ex)}");
        }
    }

    private static async Task ReadRelationsAsync(DbConnection connection, string sql, string table, string column,
        List<object> foreignKeys, List<object> referencedColumns, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameter(command, "@table", table);
        AddParameter(command, "@column", column);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var fromTable = GetString(reader, "from_table");
            var fromColumn = GetString(reader, "from_column");
            var referencedTable = GetString(reader, "referenced_table");
            var referencedColumn = GetString(reader, "referenced_column");
            var relation = new
            {
                constraintName = GetString(reader, "constraint_name"),
                fromSchema = GetString(reader, "from_schema"),
                fromTable,
                fromColumn,
                referencedSchema = GetString(reader, "referenced_schema"),
                referencedTable,
                referencedColumn
            };
            if (string.Equals(fromTable, table, StringComparison.OrdinalIgnoreCase) && string.Equals(fromColumn, column, StringComparison.OrdinalIgnoreCase)) foreignKeys.Add(relation);
            if (string.Equals(referencedTable, table, StringComparison.OrdinalIgnoreCase) && string.Equals(referencedColumn, column, StringComparison.OrdinalIgnoreCase)) referencedColumns.Add(relation);
        }
    }

    private static string DetectDatabaseType(DbConnection connection)
    {
        var name = connection.GetType().FullName ?? connection.GetType().Name;
        if (name.Contains("SqlClient", StringComparison.OrdinalIgnoreCase)) return "SQLSERVER";
        if (name.Contains("MySql", StringComparison.OrdinalIgnoreCase)) return "MYSQL";
        if (name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)) return "POSTGRESQL";
        return connection.GetType().Name.ToUpperInvariant();
    }

    private static string? GetString(DbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal)?.ToString();
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string FlattenException(Exception ex)
    {
        var parts = new List<string>();
        for (var current = ex; current != null; current = current.InnerException)
            parts.Add($"{current.GetType().Name}: {current.Message}");
        return string.Join(" | ", parts);
    }

    private sealed record SafeConnectionInfo(string? DbType, string? Host, int? Port, string? Database,
        bool Parsed, string? ParseError, string? OriginalServer);
    private sealed record TcpDiagnostic(bool Passed, string? Host, int? Port, long? ElapsedMs,
        string? ConnectedAddress, string? Error, object? Addresses);
    private sealed record DataSourceConnectionCheck(bool Passed, long ElapsedMs, string? StateBeforeOpen,
        string? StateAfterOpen, string? Database, string? DataSource, string? Select1, string? Error);
    private sealed record RelationResult(List<object> ForeignKeys, List<object> ReferencedColumns, string? Error);
}
