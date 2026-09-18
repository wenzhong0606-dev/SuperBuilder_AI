using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Threading;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Models.DTO;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Application.Metadata;

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
    private readonly VectorBackfillGate _backfillGate;

    public MetadataScannerService(
        SuperBIContext context,
        IDataSourceMetadataReader reader,
        IMetadataSearchTextBuilder textBuilder,
        IMetadataSemanticService semanticService,
        IMetadataVectorService vectorService,
        VectorBackfillGate backfillGate)
    {
        _context = context;
        _reader = reader;
        _textBuilder = textBuilder;
        _semanticService = semanticService;
        _vectorService = vectorService;
        _backfillGate = backfillGate;
    }

    public async Task ScanAsync(
        long tenantId,
        long dataSourceId,
        string connectionString,
        int batchVersion,
        int seedVersion,
        IProgress<int>? progress = null,
        ScanTelemetry? telemetry = null,
        bool cleanupOrphans = false,
        ScanScope? scope = null,
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

        // §L.6 / C2：范围扫描先按 scope 发现（可跨库逐库汇总），再在结果上过滤（Databases/Schemas/系统库/前缀/视图/空表）。
        var (tables, columns, foreignKeys) = await DiscoverScopedAsync(connectionString, dataSource.DbType, scope, telemetry, ct);
        telemetry.Details.TablesDiscovered = tables.Count;
        telemetry.AddEvent("Info", "TablesDiscovered", $"已发现 {tables.Count} 张数据表。", tables.Count, tables.Count);
        ReportStage(telemetry, progress, "DiscoveringColumns", "正在读取字段结构…", 12, "TablesReady");
        telemetry.Details.ColumnsDiscovered = columns.Count;
        telemetry.AddEvent("Info", "ColumnsDiscovered", $"已发现 {columns.Count} 个字段。", columns.Count, columns.Count);

        // 按表名分组（大小写不敏感），供外键展示列优选与字典角色解析复用。
        var columnsByTable = columns
            .GroupBy(c => c.TableName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var fkByColumn = new Dictionary<string, ForeignKeyMetadataDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var fk in foreignKeys)
        {
            if (string.IsNullOrWhiteSpace(fk.TableName) || string.IsNullOrWhiteSpace(fk.ColumnName))
                continue;
            // §10.1：按三键（catalog/schema/table/column）定位外键归属表，避免同名跨 schema/跨库串表。
            fkByColumn[PhyColKey(fk.CatalogName, fk.SchemaName, fk.TableName, fk.ColumnName)] = fk;
        }

        if (fkByColumn.Count > 0)
            telemetry.AddEvent("Info", "ForeignKeysDiscovered", $"已发现 {fkByColumn.Count} 条外键关系。", fkByColumn.Count, fkByColumn.Count);

        ReportStage(telemetry, progress, "ComparingMetadata", "正在与已有元数据进行比对…", 22, "ColumnsReady");

        var existsTables = await _context.MetadataTables
            .Include(x => x.Columns)
            .Where(x => x.TenantId == effectiveTenantId && x.DataSourceId == dataSourceId && x.MetadataVersion == batchVersion)
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

            var metadataTable = existsTables.FirstOrDefault(x =>
                x.TableName == table.TableName
                && (x.CatalogName ?? string.Empty) == (table.CatalogName ?? string.Empty)
                && (x.SchemaName ?? string.Empty) == (table.SchemaName ?? string.Empty));
            if (metadataTable is null)
            {
                metadataTable = new MetadataTable
                {
                    TenantId = effectiveTenantId,
                    DataSourceId = dataSourceId,
                    TableName = table.TableName ?? string.Empty,
                    TableComment = table.TableComment ?? string.Empty,
                    CatalogName = table.CatalogName,
                    SchemaName = table.SchemaName,
                    ObjectKind = table.ObjectKind,
                    MetadataVersion = batchVersion
                };
                _context.MetadataTables.Add(metadataTable);
                telemetry.Details.AddedTables++;
            }
            else
            {
                metadataTable.TableComment = table.TableComment ?? string.Empty;
                telemetry.Details.UpdatedTables++;
            }

            // §10.1：按三键（catalog/schema/table）匹配本表列，避免同名跨 schema/跨库串列。
            var tableColumns = columns
                .Where(x => PhyTableKey(x.CatalogName, x.SchemaName, x.TableName)
                         == PhyTableKey(table.CatalogName, table.SchemaName, table.TableName))
                .ToList();

            foreach (var column in tableColumns)
            {
                var metadataColumn = metadataTable.Columns
                    .FirstOrDefault(x => x.ColumnName == column.ColumnName);

                if (metadataColumn is null)
                {
                    metadataColumn = new MetadataColumn
                    {
                        ColumnName = column.ColumnName ?? string.Empty,
                        ColumnComment = column.ColumnComment ?? string.Empty,
                        DataType = column.DataType ?? string.Empty,
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

                // 注释图例 → 码值映射：实测 WMS 注释普遍自带「0否 1是」「0-已创建 1-执行中」形态，
                // 是零成本、随租户 schema 自带的译码来源（此前无写入方，该层长期静默）。
                // 仅在为空时写入 —— 管理侧声明与学习规则优先级更高，绝不覆盖。
                if (string.IsNullOrWhiteSpace(metadataColumn.ValueMapJson)
                    && MetadataDiscoveryHeuristics.TryParseValueMapFromComment(
                        column.ColumnComment, out var legendJson))
                {
                    metadataColumn.ValueMapJson = legendJson;
                }

                // 外键角色：同数据源内 FK 直接落库（引用表 / 引用列 / 展示列）。
                // 跨数据源引用不在此层处理，由声明/学习规则承载。
                if (fkByColumn.TryGetValue(PhyColKey(table.CatalogName, table.SchemaName, table.TableName, column.ColumnName), out var fk))
                {
                    metadataColumn.ReferencedTable = fk.ReferencedTableName;
                    metadataColumn.ReferencedColumn = fk.ReferencedColumnName;
                    metadataColumn.ReferencedDisplayColumn = MetadataDiscoveryHeuristics.PickDisplayColumn(
                        columnsByTable,
                        fk.ReferencedTableName,
                        fk.ReferencedColumnName);
                }
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

        // 字典表启发式：识别 code+name(+type) 形态的字典表并记录角色映射（per 数据源）。
        // 仅描述「本数据源」的字典结构；跨源绑定（如 WMS 列 → PMIS 字典）由声明/学习规则承载。
        var dictConfigs = await SyncDictionaryConfigsAsync(
            effectiveTenantId,
            dataSourceId,
            columnsByTable,
            ct);
        if (dictConfigs > 0)
            telemetry.AddEvent("Info", "DictionaryDiscovered", $"已识别 {dictConfigs} 张字典表并记录角色映射。");

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
                && x.MetadataTable.MetadataVersion == batchVersion
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
            .Where(x => x.TenantId == effectiveTenantId && x.DataSourceId == dataSourceId && x.MetadataVersion == batchVersion)
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
		var vectorFailures = new ConcurrentBag<string>();
		var progressLock = new object();
		var tasks = vectorTables
			.Select(async table =>
			{
				await indexSemaphore.WaitAsync(ct);
				try
				{
					ct.ThrowIfCancellationRequested();
					telemetry.SetCurrent("Table", table.TableName);

					var result = await _vectorService.IndexAsync(table, ct);

					// 单表向量索引失败（如 Qwen 瞬时限流/5xx）重试一次。
					if (table.VectorStatus == "Failed")
						result = await _vectorService.IndexAsync(table, ct);

					if (table.VectorStatus == "Failed")
					{
						var tableName = string.IsNullOrWhiteSpace(table.TableName)
							? $"MetadataTable:{table.Id}"
							: table.TableName;
						var errorCode = string.IsNullOrWhiteSpace(table.VectorErrorCode)
							? "Unknown"
							: table.VectorErrorCode;
						vectorFailures.Add($"{tableName}({errorCode})");
					}

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

					if (table.VectorStatus == "Failed")
						indexed = ExpectedVectorCount(table);

					lock (tallyLock)
					{
						telemetry.Details.VectorsProcessed += indexed;
						telemetry.Details.VectorsIndexed += indexed;
					}

					var done = Interlocked.Increment(ref completedTables);

					// IProgress<int> 在后台扫描中会同步持久化同一个 DbContext。
					// 多表向量任务可并发，但进度落库必须串行，避免 DbContext 并发访问。
					lock (progressLock)
					{
						progress?.Report(
							ProgressBetween(78, 94, done, Math.Max(1, vectorTables.Count)));
					}
				}
				finally
				{
					indexSemaphore.Release();
				}
			})
			.ToList();

		await Task.WhenAll(tasks);

		if (!vectorFailures.IsEmpty)
		{
			var failures = vectorFailures
				.OrderBy(x => x, StringComparer.Ordinal)
				.ToList();
			var sample = string.Join("，", failures.Take(5));
			var message = failures.Count == 1
				? $"向量索引失败：{sample}。"
				: $"向量索引失败 {failures.Count} 张表：{sample}{(failures.Count > 5 ? "…" : string.Empty)}。";
			telemetry.AddEvent(
				"Error",
				"VectorIndexFailed",
				message,
				telemetry.Details.VectorsProcessed,
				telemetry.Details.VectorsTotal);
			throw new InvalidOperationException(message);
		}

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
        // C10：成功级别事件（绿），便于页面按级别着色与无障碍高亮。
        telemetry.AddSuccess("ScanSucceeded",
            $"扫描全部完成：{telemetry.Details.TablesProcessed} 张表 / {telemetry.Details.ColumnsProcessed} 个字段 / {telemetry.Details.VectorsProcessed} 向量。",
            telemetry.Details.TablesProcessed, telemetry.Details.TablesDiscovered);
        progress?.Report(100);
    }

    /// <summary>
    /// 读取外键关系；失败时记录告警并返回空集合，绝不阻断扫描。
    /// </summary>
    private async Task<List<ForeignKeyMetadataDto>> TryLoadForeignKeysAsync(
        string connectionString,
        string? dbType,
        ScanTelemetry telemetry,
        CancellationToken ct)
    {
        try
        {
            var fks = await _reader.GetForeignKeysAsync(connectionString, dbType);
            return fks ?? new List<ForeignKeyMetadataDto>();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            telemetry.AddWarning(
                "ForeignKeyReadFailed",
                $"外键关系读取失败（{ex.GetType().Name}），本次扫描跳过外键角色识别，其余元数据不受影响。");
            return new List<ForeignKeyMetadataDto>();
        }
    }

    /// <summary>
    /// 范围感知的元数据发现（C2 / §10.1 / §L.6）。
    /// ① 若 scope 指定 <see cref="ScanScope.Databases"/>，则枚举实例下数据库并逐库替换连接串汇总（跨库唯一策略）；
    /// ② 否则单连接（连接串指定库）发现；
    /// ③ scope 非空时，在发现结果上按 <see cref="ScanScope.IsTableInScope"/> 过滤表/列/外键，
    ///    并支持 <see cref="ScanScope.ExcludeEmptyTables"/>（排除无可用列的表）。
    /// 未指定 scope 时行为与历史单库发现完全一致，不影响既有用例。
    /// </summary>
    private async Task<(List<TableMetadataDto> tables, List<ColumnMetadataDto> columns, List<ForeignKeyMetadataDto> foreignKeys)>
        DiscoverScopedAsync(string connectionString, string dbType, ScanScope? scope, ScanTelemetry telemetry, CancellationToken ct)
    {
        var tables = new List<TableMetadataDto>();
        var columns = new List<ColumnMetadataDto>();
        var foreignKeys = new List<ForeignKeyMetadataDto>();

        var targets = new List<string>();
        if (scope is { Databases.Count: > 0 })
        {
            var allDatabases = await _reader.GetDatabasesAsync(connectionString, dbType, ct);
            foreach (var db in allDatabases)
            {
                if (scope.IsDatabaseIncluded(db))
                    targets.Add(WithDatabase(connectionString, db));
            }
        }
        else
        {
            targets.Add(connectionString);
        }

        foreach (var cs in targets)
        {
            tables.AddRange(await _reader.GetTablesAsync(cs, dbType, ct));
            columns.AddRange(await _reader.GetColumnsAsync(cs, dbType, ct));
            foreignKeys.AddRange(await TryLoadForeignKeysAsync(cs, dbType, telemetry, ct));
        }

        if (scope is not null)
        {
            var inScope = tables.Where(scope.IsTableInScope).ToList();

            // 排除空表：无可用列（被源读取到的列）的表对 Ask 无意义，按 §10.1 备注剔除。
            if (scope.ExcludeEmptyTables)
            {
                var nonEmpty = new HashSet<string>(
                    columns.Select(c => PhyTableKey(c.CatalogName, c.SchemaName, c.TableName)));
                inScope = inScope
                    .Where(t => nonEmpty.Contains(PhyTableKey(t.CatalogName, t.SchemaName, t.TableName)))
                    .ToList();
            }

            var keys = new HashSet<string>(
                inScope.Select(t => PhyTableKey(t.CatalogName, t.SchemaName, t.TableName)));
            tables = inScope;
            columns = columns
                .Where(c => keys.Contains(PhyTableKey(c.CatalogName, c.SchemaName, c.TableName)))
                .ToList();
            foreignKeys = foreignKeys
                .Where(f => keys.Contains(PhyTableKey(f.CatalogName, f.SchemaName, f.TableName)))
                .ToList();
        }

        return (tables, columns, foreignKeys);
    }

    /// <summary>
    /// 将连接串的当前数据库改写为指定库名（跨库逐库连接，§10.1）。
    /// SQL Server 用 Initial Catalog，MySQL/PostgreSQL 用 Database；二者皆缺时补 Database。
    /// </summary>
    private static string WithDatabase(string connectionString, string database)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        if (builder.ContainsKey("Initial Catalog"))
            builder["Initial Catalog"] = database;
        else
            builder["Database"] = database;
        return builder.ConnectionString;
    }

    /// <summary>
    /// 识别本数据源内的字典表并 upsert 角色映射配置。
    /// 匹配键：TenantId + DataSourceId + TableName。返回新增/更新的配置数。
    /// </summary>
    private async Task<int> SyncDictionaryConfigsAsync(
        long tenantId,
        long dataSourceId,
        Dictionary<string, List<ColumnMetadataDto>> columnsByTable,
        CancellationToken ct)
    {
        // IgnoreQueryFilters + 显式租户过滤：既避免全局租户过滤器影响，又确保跨租户不串写。
        var existing = await _context.MetadataDictionaryConfigs
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.DataSourceId == dataSourceId)
            .ToListAsync(ct);
        var existingByTable = existing
            .GroupBy(x => x.TableName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var changed = 0;
        foreach (var pair in columnsByTable)
        {
            var tableName = pair.Key;
            var columns = pair.Value;

            if (!MetadataDiscoveryHeuristics.IsDictionaryCandidate(tableName))
                continue;

            // 字典表语义列很少（code/name/type），但真实字典表常带大量 extend_*/审计列。
            // 有效列宽 + 角色解析都跑在「语义列」上 —— 实测 PMIS.js_sys_dict_data 共 44 列、
            // 语义列仅 6 列；若按原始列数设限，或直接在原始列上解析角色，都会选中
            // parent_codes / tree_names 这类噪声列，导致该表永久无法被正确发现。
            var semanticColumns = MetadataDiscoveryHeuristics.SemanticColumns(columns);
            if (semanticColumns.Count is < 2 or > 12)
                continue;
            // 原始列数绝对兜底：防止异常宽表进入角色解析。
            if (columns.Count > 256)
                continue;

            if (!MetadataDiscoveryHeuristics.TryResolveDictionaryRoles(
                    semanticColumns, out var codeColumn, out var nameColumn, out var typeColumn))
                continue;

            if (!existingByTable.TryGetValue(tableName, out var config))
            {
                config = new MetadataDictionaryConfig
                {
                    TenantId = tenantId,
                    DataSourceId = dataSourceId,
                    TableName = tableName
                };
                _context.MetadataDictionaryConfigs.Add(config);
                existingByTable[tableName] = config;
                changed++;
            }
            else if (config.CodeColumn == codeColumn
                && config.NameColumn == nameColumn
                && config.TypeColumn == typeColumn)
            {
                // 角色未变，无需写入。
                continue;
            }
            else
            {
                changed++;
            }

            config.CodeColumn = codeColumn;
            config.NameColumn = nameColumn;
            config.TypeColumn = typeColumn;
            config.IsEnabled = true;
        }

        return changed;
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

    /// <summary>
    /// 版本化 clone-on-write（§L.3）：从种子版本（=当前 active）深拷贝出 staging 行（版本=batchVersion），清空向量状态。
    /// 新扫描在 staging 上改写，永不修改 active 版本行；激活前对 Ask 不可见。
    /// </summary>
    public async Task PrepareStagingAsync(
        long tenantId,
        long dataSourceId,
        int batchVersion,
        int seedVersion,
        CancellationToken ct = default)
    {
        var seedTables = await _context.MetadataTables
            .Include(t => t.Columns)
            .ThenInclude(c => c.Semantic)
            .Where(t => t.TenantId == tenantId && t.DataSourceId == dataSourceId && t.MetadataVersion == seedVersion)
            .ToListAsync(ct);

        foreach (var src in seedTables)
        {
            var clone = new MetadataTable
            {
                TenantId = src.TenantId,
                DataSourceId = src.DataSourceId,
                TableName = src.TableName,
                TableComment = src.TableComment,
                ObjectKind = src.ObjectKind,
                CatalogName = src.CatalogName,
                SchemaName = src.SchemaName,
                BusinessDomain = src.BusinessDomain,
                SearchText = src.SearchText,
                MetadataVersion = batchVersion,
                // 克隆清空向量状态（§L.3）：逼 NeedsIndex 重索引出新版本 point
                VectorId = null,
                VectorStatus = null,
                VectorSyncTime = null,
                VectorErrorCode = null,
                EmbeddingModel = null,
                VectorDimension = null
            };

            foreach (var col in src.Columns)
            {
                var cClone = new MetadataColumn
                {
                    ColumnName = col.ColumnName,
                    ColumnComment = col.ColumnComment,
                    DataType = col.DataType,
                    Length = col.Length,
                    IsNullable = col.IsNullable,
                    IsPrimaryKey = col.IsPrimaryKey,
                    Ordinal = col.Ordinal,
                    NativeType = col.NativeType,
                    Precision = col.Precision,
                    Scale = col.Scale,
                    SearchText = col.SearchText,
                    ValueMapJson = col.ValueMapJson,
                    ReferencedTable = col.ReferencedTable,
                    ReferencedColumn = col.ReferencedColumn,
                    ReferencedDisplayColumn = col.ReferencedDisplayColumn,
                    IsDictBacked = col.IsDictBacked,
                    DictConfigId = col.DictConfigId,
                    DictCategoryValue = col.DictCategoryValue,
                    BusinessKey = col.BusinessKey,
                    MetadataVersion = batchVersion,
                    VectorId = null,
                    VectorStatus = null,
                    VectorSyncTime = null,
                    VectorErrorCode = null,
                    EmbeddingModel = null,
                    VectorDimension = null
                };

                if (col.Semantic != null)
                {
                    cClone.Semantic = new MetadataSemantic
                    {
                        BusinessMeaning = col.Semantic.BusinessMeaning,
                        Keywords = col.Semantic.Keywords,
                        Synonyms = col.Semantic.Synonyms,
                        ExampleQuestions = col.Semantic.ExampleQuestions,
                        BusinessDomain = col.Semantic.BusinessDomain,
                        BusinessDomainId = col.Semantic.BusinessDomainId,
                        Confidence = col.Semantic.Confidence,
                        Source = col.Semantic.Source,
                        SearchText = col.Semantic.SearchText,
                        MetadataVersion = batchVersion,
                        VectorId = null,
                        VectorStatus = null,
                        VectorSyncTime = null,
                        VectorErrorCode = null,
                        EmbeddingModel = null,
                        VectorDimension = null
                    };
                }

                clone.Columns.Add(cClone);
            }

            _context.MetadataTables.Add(clone);
        }

        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// 激活：向量完整性闸门（§10.6）+ 引用重映射（§L.5b）+ 仅翻指针（§L.5）。
    /// 与翻指针同事务原子；任一闸门失败抛 <see cref="MetadataActivationBlockedException"/>，指针不翻转、active 不变。
    /// </summary>
    public async Task ActivateAsync(MetadataScanJob job, DataSource dataSource, CancellationToken ct = default)
    {
        var tenantId = job.TenantId;
        var dsId = job.DataSourceId;
        var batch = job.BatchVersion;
        var seed = job.SeedVersion;

        // 0) 向量回填闸门（§10.4 / §L.4）：严格过滤阶段下，未回填源禁止激活（409）。
        _backfillGate.AssertCanActivate(dataSource);

        // 1) 向量完整性闸门（§10.6）：必需 point 须 VectorId 非空且 VectorStatus=="Synced"。
        var newTables = await _context.MetadataTables
            .Include(t => t.Columns).ThenInclude(c => c.Semantic)
            .Where(t => t.TenantId == tenantId && t.DataSourceId == dsId && t.MetadataVersion == batch)
            .ToListAsync(ct);

        foreach (var t in newTables)
        {
            if (!NeedsIndex(t)) continue;
            if (string.IsNullOrWhiteSpace(t.VectorId) || t.VectorStatus != "Synced")
                throw new MetadataActivationBlockedException("vector_index_incomplete");
            foreach (var c in t.Columns)
            {
                if (string.IsNullOrWhiteSpace(c.VectorId) || c.VectorStatus != "Synced")
                    throw new MetadataActivationBlockedException("vector_index_incomplete");
                if (c.Semantic != null
                    && (string.IsNullOrWhiteSpace(c.Semantic.VectorId) || c.Semantic.VectorStatus != "Synced"))
                    throw new MetadataActivationBlockedException("vector_index_incomplete");
            }
        }

        // 2) 引用重映射（§L.5b）：先按物理键建立 旧Id→新Id 映射，孤儿引用先收集、不立即改写。
        var oldTables = await _context.MetadataTables
            .Include(t => t.Columns).ThenInclude(c => c.Semantic)
            .Where(t => t.TenantId == tenantId && t.DataSourceId == dsId && t.MetadataVersion == seed)
            .ToListAsync(ct);

        var tableMap = new Dictionary<string, long>();
        var colMap = new Dictionary<string, long>();
        var oldTableKeyById = new Dictionary<long, string>();
        var oldColKeyById = new Dictionary<long, string>();
        foreach (var ot in oldTables)
        {
            var tk = PhyTableKey(ot.CatalogName, ot.SchemaName, ot.TableName);
            oldTableKeyById[ot.Id] = tk;
            var nt = newTables.FirstOrDefault(n =>
                (n.CatalogName ?? string.Empty) == (ot.CatalogName ?? string.Empty)
                && (n.SchemaName ?? string.Empty) == (ot.SchemaName ?? string.Empty)
                && n.TableName == ot.TableName);
            if (nt is not null) tableMap[tk] = nt.Id;
            foreach (var oc in ot.Columns)
            {
                var ck = PhyColKey(ot.CatalogName, ot.SchemaName, ot.TableName, oc.ColumnName);
                oldColKeyById[oc.Id] = ck;
                var nc = nt?.Columns.FirstOrDefault(x => x.ColumnName == oc.ColumnName);
                if (nc is not null) colMap[ck] = nc.Id;
            }
        }

        var orphans = new List<OrphanReference>();

        // RLS（表级 + 列级，§L.5b 默认 Block；MetadataTableId/MetadataColumnId 为非空 long）
        var rlsList = await _context.RowLevelSecurityPolicies
            .Where(p => p.DataSourceId == dsId)
            .ToListAsync(ct);
        foreach (var p in rlsList)
        {
            if (oldTableKeyById.TryGetValue(p.MetadataTableId, out var tk) && tableMap.TryGetValue(tk, out var nid))
                p.MetadataTableId = nid;
            else
                orphans.Add(new OrphanReference { Kind = "RLS", Id = p.Id, DataSourceId = dsId, Table = TableNameById(oldTables, p.MetadataTableId) });

            if (oldColKeyById.TryGetValue(p.MetadataColumnId, out var ck) && colMap.TryGetValue(ck, out var ncid))
                p.MetadataColumnId = ncid;
            else
                orphans.Add(new OrphanReference { Kind = "RLS", Id = p.Id, DataSourceId = dsId, Column = ColNameById(oldTables, p.MetadataColumnId) });
        }

        // PhysicalBinding（表级 + 列级，非空 long）
        var bindings = await _context.PhysicalBindings
            .Where(b => b.DataSourceId == dsId)
            .ToListAsync(ct);
        foreach (var b in bindings)
        {
            if (oldTableKeyById.TryGetValue(b.MetadataTableId, out var tk) && tableMap.TryGetValue(tk, out var nid))
                b.MetadataTableId = nid;
            else
                orphans.Add(new OrphanReference { Kind = "BINDING", Id = b.Id, DataSourceId = dsId, Table = TableNameById(oldTables, b.MetadataTableId) });

            if (oldColKeyById.TryGetValue(b.MetadataColumnId, out var ck) && colMap.TryGetValue(ck, out var ncid))
                b.MetadataColumnId = ncid;
            else
                orphans.Add(new OrphanReference { Kind = "BINDING", Id = b.Id, DataSourceId = dsId, Column = ColNameById(oldTables, b.MetadataColumnId) });
        }

        // LearningRecord（列级，SetNull 保留学习 → 重映射以保留；MetadataColumnId 可空）
        var records = await _context.LearningRecords
            .Where(r => r.MetadataColumnId != null)
            .ToListAsync(ct);
        foreach (var r in records)
        {
            if (r.MetadataColumnId.HasValue && oldColKeyById.TryGetValue(r.MetadataColumnId.Value, out var ck) && colMap.TryGetValue(ck, out var ncid))
                r.MetadataColumnId = ncid;
            else if (r.MetadataColumnId.HasValue)
                orphans.Add(new OrphanReference { Kind = "LEARNING", Id = r.Id, DataSourceId = dsId, Column = ColNameById(oldTables, r.MetadataColumnId.Value) });
        }

        // 孤儿引用（默认 Block，§0-4 / §8 已确认）：阻断激活，绝不静默丢失安全策略。
        if (orphans.Count > 0)
            throw new MetadataActivationBlockedException("orphaned_references", orphans);

        // 3) 仅翻指针（§L.5）：激活前所有检查通过。
        dataSource.ActiveMetadataVersion = batch;
        job.ActivatedVersion = batch;
        await _context.SaveChangesAsync(ct);
    }

    private static string PhyTableKey(string? catalog, string? schema, string? table)
        => $"{(catalog ?? string.Empty).ToLowerInvariant()}|{(schema ?? string.Empty).ToLowerInvariant()}|{(table ?? string.Empty).ToLowerInvariant()}";

    private static string PhyColKey(string? catalog, string? schema, string? table, string? column)
        => $"{PhyTableKey(catalog, schema, table)}|{(column ?? string.Empty).ToLowerInvariant()}";

    private static string? TableNameById(List<MetadataTable> tables, long id)
        => tables.FirstOrDefault(t => t.Id == id)?.TableName;

    private static string? ColNameById(List<MetadataTable> tables, long colId)
    {
        foreach (var t in tables)
            foreach (var c in t.Columns)
                if (c.Id == colId) return c.ColumnName;
        return null;
    }
}
