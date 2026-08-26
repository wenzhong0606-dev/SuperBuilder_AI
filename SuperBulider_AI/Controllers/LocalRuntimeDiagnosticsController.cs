using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using System.Data;
using System.Data.Common;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// C.13.3 本地 Runtime 验证控制器。
/// 用于开发机直接验证 SQL Server / Qdrant 的真实运行链，避免依赖 GitHub Runner 的基础设施差异。
/// </summary>
[ApiController]
[Route("evaluation/local-runtime")]
public sealed class LocalRuntimeDiagnosticsController : ControllerBase
{
    private readonly SuperBIContext _context;
    private readonly IConfiguration _configuration;

    public LocalRuntimeDiagnosticsController(
        SuperBIContext context,
        IConfiguration configuration)
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

    /// <summary>
    /// D14 Dimension Forensic Diagnostic。
    /// 只读查询，不修改 Metadata，不修改 QueryPlan。
    /// 用于闭合：Fact Column -> Metadata Semantic -> DB FK Relationship -> Master PK -> Label Candidate。
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
                        (x.ColumnName!.Equals(column, StringComparison.OrdinalIgnoreCase) ||
                         x.ColumnName!.Equals("material_code", StringComparison.OrdinalIgnoreCase) ||
                         x.ColumnName!.Equals("material_name", StringComparison.OrdinalIgnoreCase)))
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
                        (!string.IsNullOrWhiteSpace(x.semantic?.BusinessMeaning) ||
                         !string.IsNullOrWhiteSpace(x.semantic?.Keywords) ||
                         !string.IsNullOrWhiteSpace(x.semantic?.Synonyms)))
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
                metadataRelationshipModelExists = false,
                note = "当前代码库没有独立 Metadata Relationship 实体；数据库 FK 与 Metadata Master Candidate 分开返回。"
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

    private async Task<(List<object> ForeignKeys, List<object> ReferencedColumns)> QueryDatabaseRelationsAsync(
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        var foreignKeys = new List<object>();
        var referencedColumns = new List<object>();
        await using var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
SELECT
    kcu.CONSTRAINT_NAME,
    kcu.TABLE_SCHEMA,
    kcu.TABLE_NAME,
    kcu.COLUMN_NAME,
    kcu.REFERENCED_TABLE_SCHEMA,
    kcu.REFERENCED_TABLE_NAME,
    kcu.REFERENCED_COLUMN_NAME
FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
WHERE kcu.TABLE_NAME = @table
  AND kcu.COLUMN_NAME = @column
  AND kcu.REFERENCED_TABLE_NAME IS NOT NULL
ORDER BY kcu.CONSTRAINT_NAME;";
            AddParameter(command, "@table", table);
            AddParameter(command, "@column", column);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var item = new
                {
                    constraintName = reader["CONSTRAINT_NAME"]?.ToString(),
                    tableSchema = reader["TABLE_SCHEMA"]?.ToString(),
                    tableName = reader["TABLE_NAME"]?.ToString(),
                    columnName = reader["COLUMN_NAME"]?.ToString(),
                    referencedTableSchema = reader["REFERENCED_TABLE_SCHEMA"]?.ToString(),
                    referencedTableName = reader["REFERENCED_TABLE_NAME"]?.ToString(),
                    referencedColumnName = reader["REFERENCED_COLUMN_NAME"]?.ToString()
                };
                foreignKeys.Add(item);
            }
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
SELECT
    kcu.CONSTRAINT_NAME,
    kcu.TABLE_SCHEMA,
    kcu.TABLE_NAME,
    kcu.COLUMN_NAME,
    kcu.REFERENCED_TABLE_SCHEMA,
    kcu.REFERENCED_TABLE_NAME,
    kcu.REFERENCED_COLUMN_NAME
FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
WHERE kcu.REFERENCED_TABLE_NAME = @table
  AND kcu.REFERENCED_COLUMN_NAME = @column
ORDER BY kcu.CONSTRAINT_NAME;";
            AddParameter(command, "@table", table);
            AddParameter(command, "@column", column);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var item = new
                {
                    constraintName = reader["CONSTRAINT_NAME"]?.ToString(),
                    tableSchema = reader["TABLE_SCHEMA"]?.ToString(),
                    tableName = reader["TABLE_NAME"]?.ToString(),
                    columnName = reader["COLUMN_NAME"]?.ToString(),
                    referencedTableSchema = reader["REFERENCED_TABLE_SCHEMA"]?.ToString(),
                    referencedTableName = reader["REFERENCED_TABLE_NAME"]?.ToString(),
                    referencedColumnName = reader["REFERENCED_COLUMN_NAME"]?.ToString()
                };
                referencedColumns.Add(item);
            }
        }

        return (foreignKeys, referencedColumns);
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
            return new LocalRuntimeCheckResult(
                result == 1,
                "SqlServer",
                result == 1 ? "SQL Server 认证与 SELECT 1 均通过。" : "SQL Server SELECT 1 返回非预期结果。",
                stopwatch.ElapsedMilliseconds,
                new
                {
                    connected = true,
                    select1 = result,
                    database = _context.Database.GetDbConnection().Database,
                    server = _context.Database.GetDbConnection().DataSource
                });
        }
        catch (Exception ex)
        {
            return new LocalRuntimeCheckResult(
                false,
                "SqlServer",
                ex.Message,
                stopwatch.ElapsedMilliseconds,
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
            return new LocalRuntimeCheckResult(
                response.IsSuccessStatusCode,
                "Qdrant",
                response.IsSuccessStatusCode ? "Qdrant HTTP healthz 通过。" : "Qdrant HTTP healthz 返回失败状态。",
                stopwatch.ElapsedMilliseconds,
                new { host, httpPort, grpcPort, httpHealthUrl = uri.ToString(), statusCode = (int)response.StatusCode, responseBody = body });
        }
        catch (Exception ex)
        {
            return new LocalRuntimeCheckResult(
                false,
                "Qdrant",
                ex.Message,
                stopwatch.ElapsedMilliseconds,
                new { host, httpPort, grpcPort, httpHealthUrl = uri.ToString(), exceptionType = ex.GetType().FullName, innerMessage = ex.InnerException?.Message });
        }
    }

    private sealed record LocalRuntimeCheckResult(
        bool Passed,
        string Stage,
        string Message,
        long ElapsedMs,
        object? Details = null);
}
