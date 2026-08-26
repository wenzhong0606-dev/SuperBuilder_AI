using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using System.Data;
using System.Data.Common;

namespace SuperBuilder_AI.Controllers;

[ApiController]
[Route("evaluation/local-runtime")]
public sealed class LocalRuntimeDiagnosticsController : ControllerBase
{
    private readonly SuperBIContext _context;
    private readonly IConfiguration _configuration;

    public LocalRuntimeDiagnosticsController(SuperBIContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
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

        // 第一层：确认 MetadataColumn 本身，以及它的语义证据。
        // 注意：这里绝不能把 source DB 的关系信息混进 metadata DB 查询。
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

        // EF Core 不支持 Equals(string, StringComparison) 的 SQL 翻译。
        // 当前 SQL Server collation 已经负责普通字符串比较，因此这里使用 ==。
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

        var databaseRelations = await QueryDatabaseRelationsAsync(table, column, cancellationToken);

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
                databaseForeignKeys = databaseRelations.ForeignKeys,
                referencedMasterColumns = databaseRelations.ReferencedColumns,
                databaseRelationQueryError = databaseRelations.Error,
                metadataRelationshipModelExists = false,
                note = "当前代码库没有独立 Metadata Relationship 实体；此处只诊断当前 SuperBIContext 数据库中的 SQL Server FK。source DB 的关系需要通过 DataSourceConnectionFactory/IDataSourceMetadataReader 单独读取。"
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
    /// 读取当前 SuperBIContext 所连接 SQL Server 的 FK。
    /// 这里故意不使用 REFERENCED_TABLE_NAME / REFERENCED_COLUMN_NAME 作为 SQL 别名，
    /// 避免与 MySQL INFORMATION_SCHEMA 的字段名混淆，也避免旧代码/缓存导致的同名列解析问题。
    /// 更重要的是：查询失败不能让诊断接口 500；关系诊断失败必须作为 evidence 返回。
    /// </summary>
    private async Task<DatabaseRelationResult> QueryDatabaseRelationsAsync(
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        var foreignKeys = new List<object>();
        var referencedColumns = new List<object>();
        string? error = null;

        await using var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT
    fk.name AS fk_constraint,
    sch_from.name AS from_schema,
    t_from.name AS from_table,
    c_from.name AS from_column,
    sch_to.name AS ref_schema,
    t_to.name AS ref_table,
    c_to.name AS ref_column
FROM sys.foreign_key_columns AS fkc
INNER JOIN sys.foreign_keys AS fk
    ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.tables AS t_from
    ON t_from.object_id = fkc.parent_object_id
INNER JOIN sys.schemas AS sch_from
    ON sch_from.schema_id = t_from.schema_id
INNER JOIN sys.columns AS c_from
    ON c_from.object_id = fkc.parent_object_id
   AND c_from.column_id = fkc.parent_column_id
INNER JOIN sys.tables AS t_to
    ON t_to.object_id = fkc.referenced_object_id
INNER JOIN sys.schemas AS sch_to
    ON sch_to.schema_id = t_to.schema_id
INNER JOIN sys.columns AS c_to
    ON c_to.object_id = fkc.referenced_object_id
   AND c_to.column_id = fkc.referenced_column_id
WHERE t_from.name = @table
  AND c_from.name = @column
ORDER BY fk.name, fkc.constraint_column_id;";
            AddParameter(command, "@table", table);
            AddParameter(command, "@column", column);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                foreignKeys.Add(ReadDatabaseRelation(reader));
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT
    fk.name AS fk_constraint,
    sch_from.name AS from_schema,
    t_from.name AS from_table,
    c_from.name AS from_column,
    sch_to.name AS ref_schema,
    t_to.name AS ref_table,
    c_to.name AS ref_column
FROM sys.foreign_key_columns AS fkc
INNER JOIN sys.foreign_keys AS fk
    ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.tables AS t_from
    ON t_from.object_id = fkc.parent_object_id
INNER JOIN sys.schemas AS sch_from
    ON sch_from.schema_id = t_from.schema_id
INNER JOIN sys.columns AS c_from
    ON c_from.object_id = fkc.parent_object_id
   AND c_from.column_id = fkc.parent_column_id
INNER JOIN sys.tables AS t_to
    ON t_to.object_id = fkc.referenced_object_id
INNER JOIN sys.schemas AS sch_to
    ON sch_to.schema_id = t_to.schema_id
INNER JOIN sys.columns AS c_to
    ON c_to.object_id = fkc.referenced_object_id
   AND c_to.column_id = fkc.referenced_column_id
WHERE t_to.name = @table
  AND c_to.name = @column
ORDER BY fk.name, fkc.constraint_column_id;";
            AddParameter(command, "@table", table);
            AddParameter(command, "@column", column);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                referencedColumns.Add(ReadDatabaseRelation(reader));
        }
        catch (Exception ex)
        {
            error ??= ex.Message;
        }

        return new DatabaseRelationResult(foreignKeys, referencedColumns, error);
    }

    private static object ReadDatabaseRelation(DbDataReader reader) => new
    {
        constraintName = reader["fk_constraint"]?.ToString(),
        tableSchema = reader["from_schema"]?.ToString(),
        tableName = reader["from_table"]?.ToString(),
        columnName = reader["from_column"]?.ToString(),
        referencedTableSchema = reader["ref_schema"]?.ToString(),
        referencedTableName = reader["ref_table"]?.ToString(),
        referencedColumnName = reader["ref_column"]?.ToString()
    };

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

    private sealed record DatabaseRelationResult(
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
