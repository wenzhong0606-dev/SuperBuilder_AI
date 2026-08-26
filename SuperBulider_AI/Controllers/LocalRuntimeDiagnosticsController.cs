using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Database;
using System.Data;
using System.Data.Common;

namespace SuperBuilder_AI.Controllers;

[ApiController]
[Route("evaluation/local-runtime")]
public sealed class LocalRuntimeDiagnosticsController : ControllerBase
{
    private readonly SuperBIContext _context;
    private readonly IConfiguration _configuration;
    private readonly IDataSourceConnectionFactory _dataSourceConnectionFactory;

    public LocalRuntimeDiagnosticsController(
        SuperBIContext context,
        IConfiguration configuration,
        IDataSourceConnectionFactory dataSourceConnectionFactory)
    {
        _context = context;
        _configuration = configuration;
        _dataSourceConnectionFactory = dataSourceConnectionFactory;
    }

    [HttpGet("sqlserver")]
    public async Task<ActionResult<object>> SqlServer(CancellationToken cancellationToken)
        => Ok(await CheckSqlServerAsync(cancellationToken));

    [HttpGet("qdrant")]
    public async Task<ActionResult<object>> Qdrant(CancellationToken cancellationToken)
        => Ok(await CheckQdrantAsync(cancellationToken));

    [HttpGet("infrastructure")]
    public async Task<ActionResult<object>> Infrastructure(CancellationToken cancellationToken)
    {
        var sql = await CheckSqlServerAsync(cancellationToken);
        var qdrant = await CheckQdrantAsync(cancellationToken);
        return Ok(new { passed = sql.Passed && qdrant.Passed, sqlServer = sql, qdrant });
    }

    /// <summary>
    /// 查询 Metadata 中的字段事实、语义证据，以及真正业务数据源中的 FK 关系。
    /// 注意：SuperBIContext 只是 Metadata DB，不是业务 source DB。
    /// </summary>
    [HttpGet("dimension-forensic")]
    public async Task<ActionResult<object>> DimensionForensic(
        [FromQuery] string table = "wms_storage_receipt_info",
        [FromQuery] string column = "material_id",
        [FromQuery] string dimension = "物料",
        [FromQuery] long? preferredColumnId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(table) || string.IsNullOrWhiteSpace(column))
            return BadRequest(new { passed = false, reason = "table/column 不能为空。" });

        var metadataColumns = await _context.MetadataColumns
            .AsNoTracking()
            .Include(x => x.MetadataTable)
            .Include(x => x.Semantic)
            .Where(x => x.MetadataTable != null && x.MetadataTable.TableName == table && x.ColumnName == column)
            .Select(x => new
            {
                columnId = x.Id,
                metadataTableId = x.MetadataTableId,
                tableName = x.MetadataTable!.TableName,
                tableComment = x.MetadataTable.TableComment,
                dataSourceId = x.MetadataTable.DataSourceId,
                tenantId = x.MetadataTable.TenantId,
                columnName = x.ColumnName,
                columnComment = x.ColumnComment,
                dataType = x.DataType,
                isPrimaryKey = x.IsPrimaryKey,
                businessKey = x.BusinessKey,
                searchText = x.SearchText,
                vectorId = x.VectorId,
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
                    source = x.Semantic.Source,
                    searchText = x.Semantic.SearchText,
                    vectorId = x.Semantic.VectorId
                }
            })
            .ToListAsync(cancellationToken);

        var relatedMetadataColumns = await _context.MetadataColumns
            .AsNoTracking()
            .Include(x => x.MetadataTable)
            .Include(x => x.Semantic)
            .Where(x => x.MetadataTable != null && x.ColumnName != null &&
                        (x.ColumnName == column ||
                         x.ColumnName == "material_code" ||
                         x.ColumnName == "material_name"))
            .Select(x => new
            {
                columnId = x.Id,
                metadataTableId = x.MetadataTableId,
                tableName = x.MetadataTable!.TableName,
                tableComment = x.MetadataTable.TableComment,
                dataSourceId = x.MetadataTable.DataSourceId,
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
                    source = x.Semantic.Source,
                    searchText = x.Semantic.SearchText,
                    vectorId = x.Semantic.VectorId
                }
            })
            .OrderByDescending(x => x.tableName == table)
            .ThenByDescending(x => x.columnName == column)
            .Take(200)
            .ToListAsync(cancellationToken);

        var dataSourceId = metadataColumns.FirstOrDefault()?.dataSourceId;
        var sourceDatabaseRelations = dataSourceId.HasValue
            ? await QuerySourceDatabaseRelationsAsync(dataSourceId.Value, table, column, cancellationToken)
            : new SourceDatabaseRelationResult(null, new List<object>(), new List<object>(), "MetadataColumn 未找到 dataSourceId。", null);

        var masterCandidates = relatedMetadataColumns
            .Where(x => x.isPrimaryKey == true &&
                        (!string.IsNullOrWhiteSpace(x.semantic?.businessMeaning) ||
                         !string.IsNullOrWhiteSpace(x.semantic?.keywords) ||
                         !string.IsNullOrWhiteSpace(x.semantic?.synonyms)))
            .Take(100)
            .ToList();

        var resolvedPreferredColumnId = preferredColumnId ?? metadataColumns.FirstOrDefault()?.columnId;

        return Ok(new
        {
            passed = metadataColumns.Count > 0,
            input = new { table, column, dimension, preferredColumnId },
            fact = new
            {
                metadataColumnFound = metadataColumns.Count > 0,
                preferredColumnId = resolvedPreferredColumnId,
                columns = metadataColumns
            },
            relationship = new
            {
                dataSourceId,
                sourceDatabase = sourceDatabaseRelations.Database,
                sourceDatabaseType = sourceDatabaseRelations.DatabaseType,
                databaseForeignKeys = sourceDatabaseRelations.ForeignKeys,
                referencedMasterColumns = sourceDatabaseRelations.ReferencedColumns,
                databaseRelationQueryError = sourceDatabaseRelations.Error,
                metadataRelationshipModelExists = false,
                note = "关系查询现在直接读取 MetadataColumn 对应 dataSourceId 的业务 source DB，不再错误查询 SuperBIContext。"
            },
            master = new
            {
                candidateCount = masterCandidates.Count,
                candidates = masterCandidates
            },
            preferredColumn = new
            {
                requested = preferredColumnId,
                resolved = resolvedPreferredColumnId,
                existsInFact = resolvedPreferredColumnId.HasValue && metadataColumns.Any(x => x.columnId == resolvedPreferredColumnId.Value),
                note = "该诊断只观察 preferredColumnId，不改变当前 Resolution 逻辑。"
            }
        });
    }

    /// <summary>
    /// 独立的 source DB 关系诊断 Action。
    /// 这是本次根因追踪的关键入口。
    /// </summary>
    [HttpGet("source-database-relations")]
    public async Task<ActionResult<object>> SourceDatabaseRelations(
        [FromQuery] string table = "wms_storage_receipt_info",
        [FromQuery] string column = "material_id",
        [FromQuery] long? dataSourceId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(table) || string.IsNullOrWhiteSpace(column))
            return BadRequest(new { passed = false, reason = "table/column 不能为空。" });

        var resolvedDataSourceId = dataSourceId;
        if (!resolvedDataSourceId.HasValue)
        {
            resolvedDataSourceId = await _context.MetadataColumns
                .AsNoTracking()
                .Where(x => x.MetadataTable != null &&
                            x.MetadataTable.TableName == table &&
                            x.ColumnName == column)
                .Select(x => (long?)x.MetadataTable!.DataSourceId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (!resolvedDataSourceId.HasValue)
        {
            return NotFound(new
            {
                passed = false,
                table,
                column,
                reason = "无法从 MetadataColumn 解析 dataSourceId。"
            });
        }

        var result = await QuerySourceDatabaseRelationsAsync(
            resolvedDataSourceId.Value,
            table,
            column,
            cancellationToken);

        return Ok(new
        {
            passed = result.Error == null,
            table,
            column,
            dataSourceId = resolvedDataSourceId,
            sourceDatabase = result.Database,
            sourceDatabaseType = result.DatabaseType,
            foreignKeys = result.ForeignKeys,
            referencedColumns = result.ReferencedColumns,
            error = result.Error,
            note = "foreignKeys 表示该字段引用其他主表；referencedColumns 表示其他表引用该字段。"
        });
    }

    private async Task<SourceDatabaseRelationResult> QuerySourceDatabaseRelationsAsync(
        long dataSourceId,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _dataSourceConnectionFactory.CreateAsync(dataSourceId);
            await connection.OpenAsync(cancellationToken);

            var database = connection.Database;
            var databaseType = DetectDatabaseType(connection);
            var foreignKeys = new List<object>();
            var referencedColumns = new List<object>();

            switch (databaseType)
            {
                case "SQLSERVER":
                    await QuerySqlServerRelationsAsync(connection, table, column, foreignKeys, referencedColumns, cancellationToken);
                    break;
                case "MYSQL":
                    await QueryMySqlRelationsAsync(connection, table, column, foreignKeys, referencedColumns, cancellationToken);
                    break;
                case "POSTGRESQL":
                    await QueryPostgreSqlRelationsAsync(connection, table, column, foreignKeys, referencedColumns, cancellationToken);
                    break;
                default:
                    return new SourceDatabaseRelationResult(
                        database,
                        databaseType,
                        foreignKeys,
                        referencedColumns,
                        $"不支持的 source DB 类型: {databaseType}");
            }

            return new SourceDatabaseRelationResult(
                database,
                databaseType,
                foreignKeys,
                referencedColumns,
                null);
        }
        catch (Exception ex)
        {
            return new SourceDatabaseRelationResult(
                null,
                null,
                new List<object>(),
                new List<object>(),
                $"source DB 关系查询失败: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task QuerySqlServerRelationsAsync(
        DbConnection connection,
        string table,
        string column,
        List<object> foreignKeys,
        List<object> referencedColumns,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    fk.name AS constraint_name,
    sch_from.name AS from_schema,
    t_from.name AS from_table,
    c_from.name AS from_column,
    sch_to.name AS referenced_schema,
    t_to.name AS referenced_table,
    c_to.name AS referenced_column
FROM sys.foreign_key_columns AS fkc
INNER JOIN sys.foreign_keys AS fk ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.tables AS t_from ON t_from.object_id = fkc.parent_object_id
INNER JOIN sys.schemas AS sch_from ON sch_from.schema_id = t_from.schema_id
INNER JOIN sys.columns AS c_from ON c_from.object_id = fkc.parent_object_id AND c_from.column_id = fkc.parent_column_id
INNER JOIN sys.tables AS t_to ON t_to.object_id = fkc.referenced_object_id
INNER JOIN sys.schemas AS sch_to ON sch_to.schema_id = t_to.schema_id
INNER JOIN sys.columns AS c_to ON c_to.object_id = fkc.referenced_object_id AND c_to.column_id = fkc.referenced_column_id
WHERE (t_from.name = @table AND c_from.name = @column)
   OR (t_to.name = @table AND c_to.name = @column)
ORDER BY fk.name, fkc.constraint_column_id;";

        await ReadRelationRowsAsync(connection, sql, table, column, foreignKeys, referencedColumns, cancellationToken);
    }

    private static async Task QueryMySqlRelationsAsync(
        DbConnection connection,
        string table,
        string column,
        List<object> foreignKeys,
        List<object> referencedColumns,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    kcu.CONSTRAINT_NAME AS constraint_name,
    kcu.TABLE_SCHEMA AS from_schema,
    kcu.TABLE_NAME AS from_table,
    kcu.COLUMN_NAME AS from_column,
    kcu.REFERENCED_TABLE_SCHEMA AS referenced_schema,
    kcu.REFERENCED_TABLE_NAME AS referenced_table,
    kcu.REFERENCED_COLUMN_NAME AS referenced_column
FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
WHERE ((kcu.TABLE_NAME = @table AND kcu.COLUMN_NAME = @column)
    OR (kcu.REFERENCED_TABLE_NAME = @table AND kcu.REFERENCED_COLUMN_NAME = @column))
  AND kcu.REFERENCED_TABLE_NAME IS NOT NULL
ORDER BY kcu.CONSTRAINT_NAME, kcu.ORDINAL_POSITION;";

        await ReadRelationRowsAsync(connection, sql, table, column, foreignKeys, referencedColumns, cancellationToken);
    }

    private static async Task QueryPostgreSqlRelationsAsync(
        DbConnection connection,
        string table,
        string column,
        List<object> foreignKeys,
        List<object> referencedColumns,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    tc.constraint_name,
    kcu.table_schema AS from_schema,
    kcu.table_name AS from_table,
    kcu.column_name AS from_column,
    ccu.table_schema AS referenced_schema,
    ccu.table_name AS referenced_table,
    ccu.column_name AS referenced_column
FROM information_schema.table_constraints tc
JOIN information_schema.key_column_usage kcu
  ON tc.constraint_name = kcu.constraint_name
 AND tc.table_schema = kcu.table_schema
JOIN information_schema.constraint_column_usage ccu
  ON ccu.constraint_name = tc.constraint_name
 AND ccu.table_schema = tc.table_schema
WHERE tc.constraint_type = 'FOREIGN KEY'
  AND ((kcu.table_name = @table AND kcu.column_name = @column)
    OR (ccu.table_name = @table AND ccu.column_name = @column))
ORDER BY tc.constraint_name, kcu.ordinal_position;";

        await ReadRelationRowsAsync(connection, sql, table, column, foreignKeys, referencedColumns, cancellationToken);
    }

    private static async Task ReadRelationRowsAsync(
        DbConnection connection,
        string sql,
        string table,
        string column,
        List<object> foreignKeys,
        List<object> referencedColumns,
        CancellationToken cancellationToken)
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

            if (string.Equals(fromTable, table, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(fromColumn, column, StringComparison.OrdinalIgnoreCase))
            {
                foreignKeys.Add(relation);
            }

            if (string.Equals(referencedTable, table, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(referencedColumn, column, StringComparison.OrdinalIgnoreCase))
            {
                referencedColumns.Add(relation);
            }
        }
    }

    private static string DetectDatabaseType(DbConnection connection)
    {
        var typeName = connection.GetType().FullName ?? connection.GetType().Name;
        if (typeName.Contains("SqlClient", StringComparison.OrdinalIgnoreCase)) return "SQLSERVER";
        if (typeName.Contains("MySql", StringComparison.OrdinalIgnoreCase)) return "MYSQL";
        if (typeName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)) return "POSTGRESQL";
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

    private async Task<LocalRuntimeCheckResult> CheckSqlServerAsync(CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var connected = await _context.Database.CanConnectAsync(cancellationToken);
            if (!connected)
                return new LocalRuntimeCheckResult(false, "SqlServer", "SuperBIContext 无法连接当前配置的 SQL Server。", stopwatch.ElapsedMilliseconds);
            var result = await _context.Database.SqlQueryRaw<int>("SELECT 1 AS Value").SingleAsync(cancellationToken);
            return new LocalRuntimeCheckResult(result == 1, "SqlServer", result == 1 ? "SQL Server 认证与 SELECT 1 均通过。" : "SQL Server SELECT 1 返回非预期结果。", stopwatch.ElapsedMilliseconds,
                new { connected = true, select1 = result, database = _context.Database.GetDbConnection().Database, server = _context.Database.GetDbConnection().DataSource });
        }
        catch (Exception ex)
        {
            return new LocalRuntimeCheckResult(false, "SqlServer", ex.Message, stopwatch.ElapsedMilliseconds,
                new { connected = false, exceptionType = ex.GetType().FullName, innerMessage = ex.InnerException?.Message });
        }
    }

    private async Task<LocalRuntimeCheckResult> CheckQdrantAsync(CancellationToken cancellationToken)
    {
        var host = _configuration["Qdrant:Host"] ?? "127.0.0.1";
        var grpcPort = _configuration.GetValue<int?>("Qdrant:Port") ?? 6334;
        var httpPort = _configuration.GetValue<int?>("Qdrant:HttpPort") ?? 6333;
        var uri = new Uri($"http://{host}:{httpPort}/healthz");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var response = await client.GetAsync(uri, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return new LocalRuntimeCheckResult(response.IsSuccessStatusCode, "Qdrant", response.IsSuccessStatusCode ? "Qdrant HTTP healthz 通过。" : "Qdrant HTTP healthz 返回失败状态。", stopwatch.ElapsedMilliseconds,
                new { host, httpPort, grpcPort, httpHealthUrl = uri.ToString(), statusCode = (int)response.StatusCode, responseBody = body });
        }
        catch (Exception ex)
        {
            return new LocalRuntimeCheckResult(false, "Qdrant", ex.Message, stopwatch.ElapsedMilliseconds,
                new { host, httpPort, grpcPort, httpHealthUrl = uri.ToString(), exceptionType = ex.GetType().FullName, innerMessage = ex.InnerException?.Message });
        }
    }

    private sealed record SourceDatabaseRelationResult(
        string? Database,
        string? DatabaseType,
        List<object> ForeignKeys,
        List<object> ReferencedColumns,
        string? Error);

    private sealed record LocalRuntimeCheckResult(
        bool Passed,
        string Stage,
        string Message,
        long ElapsedMs,
        object? Details = null);
}
