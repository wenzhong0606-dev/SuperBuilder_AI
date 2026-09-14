using Microsoft.EntityFrameworkCore;
using System.Threading;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Services;

/// <summary>
/// Metadata 扫描服务：读取物理结构、同步元数据、生成语义并建立向量索引。
/// 富进度通过 ScanTelemetry + IProgress&lt;int&gt; 上报；不改变扫描业务契约。
/// </summary>
public class MetadataScannerService
{
    private readonly SuperBIContext _context;
    private readonly IDataSourceMetadataReader _reader;
    private readonly IMetadataSearchTextBuilder _textBuilder;
    private readonly IMetadataSemanticService _semanticService;
    private readonly IMetadataVectorService _vectorService;

    public MetadataScannerService(
        SuperBIContext context,
        IDataSourceMetadataReader reader,
        IMetadataSearchTextBuilder textBuilder,
        IMetadataSemanticService semanticService,
        IMetadataVectorService vectorService)
    {
        _context = context;
        _reader = reader;
        _textBuilder = textBuilder;
        _semanticService = semanticService;
        _vectorService = vectorService;
    }

    public async Task ScanAsync(
        long tenantId,
        long dataSourceId,
        string connectionString,
        IProgress<int>? progress = null,
        ScanTelemetry? telemetry = null,
        bool cleanupOrphans = false,
        CancellationToken ct = default)
    {
        telemetry ??= new ScanTelemetry();

        var dataSource = await _context.DataSources
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == dataSourceId, ct);
        if (dataSource is null)
            throw new KeyNotFoundException($"数据源 {dataSourceId} 不存在，无法扫描元数据。");
        if (dataSource.TenantId != tenantId)
            throw new InvalidOperationException(
                $"数据源 {dataSourceId} 不属于租户 {tenantId}（实际归属租户 {dataSource.TenantId}），拒绝元数据扫描写入。");

        var effectiveTenantId = dataSource.TenantId;

        ReportStage(telemetry, progress, "Connecting", "正在连接业务数据库…", 3, "DatabaseConnecting");

        var tables = await _reader.GetTablesAsync(connectionString);
        telemetry.Details.TablesDiscovered = tables.Count;
        telemetry.AddEvent("Info", "TablesDiscovered", $"已发现 {tables.Count} 张数据表。", tables.Count, tables.Count);
        ReportStage(telemetry, progress, "DiscoveringColumns", "正在读取字段结构…", 12, "TablesReady");

        var columns = await _reader.GetColumnsAsync(connectionString);
        telemetry.Details.ColumnsDiscovered = columns.Count;
        telemetry.AddEvent("Info", "ColumnsDiscovered", $"已发现 {columns.Count} 个字段。", columns.Count, columns.Count);
        ReportStage(telemetry, progress, "ComparingMetadata", "正在与已有元数据进行比对…", 22, "ColumnsReady");

        var existsTables = await _context.MetadataTables
            .Include(x => x.Columns)
            .Where(x => x.TenantId == effectiveTenantId && x.DataSourceId == dataSourceId)
            .ToListAsync(ct);

        telemetry.AddEvent("Info", "MetadataCompared", $"已加载 {existsTables.Count} 张历史元数据表用于差异比对。");

        if (cleanupOrphans)
        {
            ReportStage(telemetry, progress, "CleaningOrphans", "正在检查失效元数据…", 27, "OrphanCheckStarted");

            var protectedColumnIds = await _context.RowLevelSecurityPolicies
                .Select(p => p.MetadataColumnId)
                .ToListAsync(ct);
            protectedColumnIds.AddRange(await _context.LearningRecords
                .Where(r => r.MetadataColumnId.HasValue)
                .Select(r => r.MetadataColumnId!.Value)
                .ToListAsync(ct));
            var protectedColumns = new HashSet<long>(protectedColumnIds);

            var protectedTables = new HashSet<long>(await _context.RowLevelSecurityPolicies
                .Select(p => p.MetadataTableId)
                .ToListAsync(ct));

            var sourceTableNames = new HashSet<string>(
                tables.Select(t => t.TableName ?? string.Empty),
                StringComparer.OrdinalIgnoreCase);
            var sourceColumnsByTable = columns
                .GroupBy(c => c.TableName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => new HashSet<string>(g.Select(c => c.ColumnName ?? string.Empty), StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);

            var orphanRemoved = 0;
            foreach (var existingTable in existsTables)
            {
                var tableName = existingTable.TableName ?? string.Empty;
                if (!sourceTableNames.Contains(tableName))
                {
                    var tableProtected = protectedTables.Contains(existingTable.Id)
                        || existingTable.Columns.Any(c => protectedColumns.Contains(c.Id));
                    if (!tableProtected)
                    {
                        _context.MetadataTables.Remove(existingTable);
                        orphanRemoved += 1 + existingTable.Columns.Count;
                    }
                    continue;
                }

                if (!sourceColumnsByTable.TryGetValue(tableName, out var sourceCols))
                    continue;

                foreach (var col in existingTable.Columns.ToList())
                {
                    if (!sourceCols.Contains(col.ColumnName ?? string.Empty)
                        && !protectedColumns.Contains(col.Id))
                    {
                        _context.MetadataColumns.Remove(col);
                        orphanRemoved++;
                    }
                }
            }

            telemetry.OrphansDetected = orphanRemoved;
            if (orphanRemoved > 0)
                telemetry.AddEvent("Info", "OrphansRemoved", $"已安全清理 {orphanRemoved} 个失效元数据对象。", orphanRemoved, orphanRemoved);
        }

        ReportStage(telemetry, progress, "SyncingMetadata", "正在同步表与字段结构…", 32, "MetadataSyncStarted");

        for (var tableIndex = 0; tableIndex < tables.Count; tableIndex++)
        {
            ct.ThrowIfCancellationRequested();
            var table = tables[tableIndex];
            telemetry.SetCurrent("Table", table.TableName);

            var metadataTable = existsTables.FirstOrDefault(x => x.TableName == table.TableName);
            if (metadataTable is null)
            {
                metadataTable = new MetadataTable
                {
                    TenantId = effectiveTenantId,
                    DataSourceId = dataSourceId,
                    TableName = table.TableName,
                    TableComment = table.TableComment
                };
                _context.MetadataTables.Add(metadataTable);
                telemetry.Details.AddedTables++;
            }
            else
            {
                metadataTable.TableComment = table.TableComment;
                telemetry.Details.UpdatedTables++;
            }

            var tableColumns = columns
                .Where(x => x.TableName == table.TableName)
                .ToList();

            foreach (var column in tableColumns)
            {
                var metadataColumn = metadataTable.Columns
                    .FirstOrDefault(x => x.ColumnName == column.ColumnName);

                if (metadataColumn is null)
                {
                    metadataColumn = new MetadataColumn
                    {
                        ColumnName = column.ColumnName,
                        ColumnComment = column.ColumnComment,
                        DataType = column.DataType,
                        Length = column.Length,
                        IsNullable = column.IsNullable,
                        IsPrimaryKey = column.IsPrimaryKey
                    };
                    metadataTable.Columns.Add(metadataColumn);
                    telemetry.Details.AddedColumns++;
                }
                else
                {
                    metadataColumn.ColumnComment = column.ColumnComment;
                    metadataColumn.DataType = column.DataType;
                    telemetry.Details.UpdatedColumns++;
                }

                metadataColumn.SearchText = _textBuilder.BuildColumnText(
                    table.TableName,
                    column.ColumnName,
                    column.ColumnComment,
                    column.DataType);
            }

            metadataTable.SearchText = _textBuilder.BuildMetadataText(
                table.TableName,
                table.TableComment,
                metadataTable.Columns.Select(x => x.SearchText ?? ""));

            telemetry.Details.TablesProcessed = tableIndex + 1;
            telemetry.Details.ColumnsProcessed += tableColumns.Count;
            progress?.Report(ProgressBetween(32, 52, tableIndex + 1, Math.Max(1, tables.Count)));
        }

        telemetry.SetCurrent(null, null);
        await _context.SaveChangesAsync(ct);
        telemetry.AddEvent(
            "Info",
            "MetadataSynced",
            $"元数据结构同步完成：新增 {telemetry.Details.AddedTables} 张表 / {telemetry.Details.AddedColumns} 个字段，更新 {telemetry.Details.UpdatedTables} 张表 / {telemetry.Details.UpdatedColumns} 个字段。");
        progress?.Report(55);

        var semanticColumns = await _context.MetadataColumns
            .Include(x => x.MetadataTable)
            .Include(x => x.Semantic)
            .Where(x =>
                x.MetadataTable!.TenantId == effectiveTenantId
                && x.MetadataTable.DataSourceId == dataSourceId
                && x.Semantic == null)
            .ToListAsync(ct);

        telemetry.Details.SemanticsTotal = semanticColumns.Count;
        ReportStage(
            telemetry,
            progress,
            "GeneratingSemantics",
            semanticColumns.Count == 0 ? "无需生成新的业务语义。" : $"AI 正在为 {semanticColumns.Count} 个字段生成业务语义…",
            60,
            "SemanticGenerationStarted");

        if (semanticColumns.Count > 0)
        {
            var generated = await _semanticService.GenerateBatchAsync(
                semanticColumns,
                semanticProgress =>
                {
                    telemetry.Details.SemanticsProcessed = semanticProgress.FieldsCompleted;
                    telemetry.Details.SemanticsGenerated = semanticProgress.FieldsGenerated;
                    telemetry.SetStage(
                        "GeneratingSemantics",
                        "AI 业务语义生成中：已处理 " + semanticProgress.FieldsCompleted + "/" + semanticProgress.FieldsTotal
                            + "，成功 " + semanticProgress.FieldsGenerated
                            + (semanticProgress.FieldsFailed > 0 ? "，失败 " + semanticProgress.FieldsFailed : string.Empty)
                            + "；批次 " + semanticProgress.BatchesCompleted + "/" + semanticProgress.BatchesTotal + "。",
                        "SemanticBatchProgress");
                    progress?.Report(ProgressBetween(
                        60,
                        75,
                        semanticProgress.FieldsCompleted,
                        Math.Max(1, semanticProgress.FieldsTotal)));
                },
                ct);
            telemetry.Details.SemanticsProcessed = semanticColumns.Count;
            telemetry.Details.SemanticsGenerated = generated.Count;

            if (generated.Count < semanticColumns.Count)
            {
                telemetry.AddWarning(
                    "SemanticPartial",
                    $"业务语义已生成 {generated.Count}/{semanticColumns.Count}，部分字段未生成成功，可在扫描后继续完善。");
            }
            else
            {
                telemetry.AddEvent("Info", "SemanticGenerationCompleted", $"业务语义生成完成：{generated.Count}/{semanticColumns.Count}。", generated.Count, semanticColumns.Count);
            }
        }
        progress?.Report(75);

        var syncTables = await _context.MetadataTables
            .Include(x => x.Columns)
            .ThenInclude(x => x.Semantic)
            .Where(x => x.TenantId == effectiveTenantId && x.DataSourceId == dataSourceId)
            .ToListAsync(ct);

        var vectorTables = syncTables.Where(NeedsIndex).ToList();
        telemetry.Details.VectorsTotal = vectorTables.Sum(ExpectedVectorCount);
        ReportStage(
            telemetry,
            progress,
            "IndexingVectors",
            vectorTables.Count == 0 ? "向量索引已是最新状态。" : $"正在为 {vectorTables.Count} 张表更新向量索引…",
            78,
            "VectorIndexStarted");

		/*
         * 阶段4：表间并发索引。
         * 表内批量 embedding + upsert 已在 MetadataVectorService 内完成，
         * 此处进一步对多张表并行处理，受并发度限制。
         */
		const int maxVectorConcurrency = 4;
		using var indexSemaphore =
			new SemaphoreSlim(maxVectorConcurrency, maxVectorConcurrency);
		var completedTables = 0;
		var tallyLock = new object();
		var tasks = vectorTables
			.Select(async table =>
			{
				await indexSemaphore.WaitAsync(ct);
				try
				{
					ct.ThrowIfCancellationRequested();
					telemetry.SetCurrent("Table", table.TableName);

					var result = await _vectorService.IndexAsync(table);

					// 单表向量索引失败（如 Qwen 瞬时限流/5xx）重试一次。
					if (table.VectorStatus == "Failed")
						result = await _vectorService.IndexAsync(table);

					if (!string.IsNullOrWhiteSpace(result.TableVectorId))
						table.VectorId = result.TableVectorId;

					foreach (var column in table.Columns)
					{
						if (result.ColumnVectors.TryGetValue(column.Id, out var columnVectorId))
							column.VectorId = columnVectorId;

						if (column.Semantic != null
							&& result.SemanticVectors.TryGetValue(column.Semantic.Id, out var semanticVectorId))
							column.Semantic.VectorId = semanticVectorId;
					}

					var indexed = (string.IsNullOrWhiteSpace(result.TableVectorId) ? 0 : 1)
						+ result.ColumnVectors.Count
						+ result.SemanticVectors.Count;

					int processedTotal;
					lock (tallyLock)
					{
						telemetry.Details.VectorsProcessed += indexed;
						telemetry.Details.VectorsIndexed += indexed;
						processedTotal = telemetry.Details.VectorsProcessed;
					}

					var done = Interlocked.Increment(ref completedTables);
					progress?.Report(ProgressBetween(78, 94, done, Math.Max(1, vectorTables.Count)));
				}
				finally
				{
					indexSemaphore.Release();
				}
			})
			.ToList();

		await Task.WhenAll(tasks);

        telemetry.SetCurrent(null, null);
        telemetry.AddEvent(
            "Info",
            "VectorIndexCompleted",
            telemetry.Details.VectorsTotal == 0
                ? "无需更新向量索引。"
                : $"向量索引更新完成：{telemetry.Details.VectorsProcessed}/{telemetry.Details.VectorsTotal}。",
            telemetry.Details.VectorsProcessed,
            telemetry.Details.VectorsTotal);

        ReportStage(telemetry, progress, "Finalizing", "正在保存扫描结果并执行最终一致性检查…", 97, "Finalizing");
        await _context.SaveChangesAsync(ct);

        telemetry.SetCurrent(null, null);
        telemetry.SetStage("Succeeded", "扫描处理完成。", "ScanCompleted");
        progress?.Report(100);
    }

    private static void ReportStage(
        ScanTelemetry telemetry,
        IProgress<int>? progress,
        string stage,
        string message,
        int percent,
        string eventCode)
    {
        telemetry.SetStage(stage, message, eventCode);
        progress?.Report(percent);
    }

    private static int ProgressBetween(int from, int to, int current, int total)
    {
        if (total <= 0) return to;
        var ratio = Math.Clamp(current / (double)total, 0d, 1d);
        return from + (int)Math.Round((to - from) * ratio);
    }

    private static bool NeedsIndex(MetadataTable metadataTable)
    {
        if (string.IsNullOrWhiteSpace(metadataTable.VectorId))
            return true;

        return metadataTable.Columns.Any(x =>
            string.IsNullOrWhiteSpace(x.VectorId)
            || x.Semantic != null && string.IsNullOrWhiteSpace(x.Semantic.VectorId));
    }

    private static int ExpectedVectorCount(MetadataTable metadataTable)
        => 1 + metadataTable.Columns.Count + metadataTable.Columns.Count(x => x.Semantic != null);
}
