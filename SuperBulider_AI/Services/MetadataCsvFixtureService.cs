using System.Globalization;
using System.Text;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.Organization;

namespace SuperBuilder_AI.Services;

/// <summary>
/// C.13.3 Controller 回归使用的 Metadata CSV Fixture 装载器。
///
/// CSV 是从正式 Metadata 数据库导出的测试快照，不是生产数据源。
/// 装载后仍通过正式 SuperBIContext -> MetadataVectorIndexService -> Qdrant 链路建立索引。
/// </summary>
public sealed class MetadataCsvFixtureService : IMetadataCsvFixtureService
{
    private readonly SuperBIContext _context;
    private readonly IWebHostEnvironment _environment;

    public MetadataCsvFixtureService(SuperBIContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    public async Task<MetadataCsvFixtureResult> ImportAsync()
    {
        var documentPath = Path.Combine(_environment.ContentRootPath, "Document");
        var tableRows = await ReadCsvAsync(Path.Combine(documentPath, "table.csv"));
        var columnRows = await ReadCsvAsync(Path.Combine(documentPath, "column.csv"));
        var semanticRows = await ReadCsvAsync(Path.Combine(documentPath, "Semantic.csv"));

        Validate(tableRows, columnRows, semanticRows);

        await using var transaction = await _context.Database.BeginTransactionAsync();

        // SQL Server FK actions in Phase 3.1 include NoAction/Restrict relationships.
        // Delete each dependent level and flush it before deleting its referenced owner.
        _context.PhysicalBindings.RemoveRange(_context.PhysicalBindings);
        await _context.SaveChangesAsync();

        _context.BusinessEntityRelationships.RemoveRange(_context.BusinessEntityRelationships);
        await _context.SaveChangesAsync();

        _context.BusinessEntities.RemoveRange(_context.BusinessEntities);
        await _context.SaveChangesAsync();

        _context.MetadataSemantics.RemoveRange(_context.MetadataSemantics);
        await _context.SaveChangesAsync();

        _context.MetadataColumns.RemoveRange(_context.MetadataColumns);
        await _context.SaveChangesAsync();

        _context.MetadataTables.RemoveRange(_context.MetadataTables);
        await _context.SaveChangesAsync();

        _context.DataSources.RemoveRange(_context.DataSources);
        await _context.SaveChangesAsync();

        _context.Tenants.RemoveRange(_context.Tenants);
        await _context.SaveChangesAsync();

        var tenant = new Tenant
        {
            TenantCode = "C13_3_CSV_FIXTURE",
            TenantName = "C.13.3 Metadata CSV Fixture",
            Enabled = true
        };
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();

        var dataSource = new DataSource
        {
            TenantId = tenant.Id,
            Name = "C.13.3 CSV Metadata Fixture",
            DbType = "SQLSERVER",
            ConnectionString = "Server=fixture;Database=fixture;Trusted_Connection=True;",
            Enabled = true
        };
        _context.DataSources.Add(dataSource);
        await _context.SaveChangesAsync();

        var tableMap = new Dictionary<long, MetadataTable>();
        foreach (var row in tableRows)
        {
            var sourceId = Long(row, 0);
            var table = new MetadataTable
            {
                TenantId = tenant.Id,
                DataSourceId = dataSource.Id,
                TableName = Null(row, 3),
                TableComment = Null(row, 4),
                BusinessDomain = null,
                SearchText = Null(row, 6),
                VectorId = Null(row, 7),
                CreatedTime = Date(row, 8)
            };
            _context.MetadataTables.Add(table);
            tableMap[sourceId] = table;
        }
        await _context.SaveChangesAsync();

        var columnMap = new Dictionary<long, MetadataColumn>();
        foreach (var row in columnRows)
        {
            var sourceId = Long(row, 0);
            var sourceTableId = Long(row, 1);
            if (!tableMap.TryGetValue(sourceTableId, out var table))
                throw new InvalidOperationException($"column.csv 引用了不存在的 table.csv Id={sourceTableId}。");

            var columnName = Null(row, 2);
            var column = new MetadataColumn
            {
                MetadataTableId = table.Id,
                MetadataTable = table,
                BusinessKey = $"{dataSource.Id}.{table.TableName}.{columnName}",
                ColumnName = columnName,
                ColumnComment = Null(row, 3),
                DataType = Null(row, 4),
                Length = NullableLong(row, 5),
                IsNullable = NullableBool(row, 6),
                IsPrimaryKey = NullableBool(row, 7),
                SearchText = Null(row, 8),
                VectorId = Null(row, 9),
                CreatedTime = Date(row, 10)
            };
            _context.MetadataColumns.Add(column);
            columnMap[sourceId] = column;
        }
        await _context.SaveChangesAsync();

        foreach (var row in semanticRows)
        {
            var sourceColumnId = Long(row, 1);
            if (!columnMap.TryGetValue(sourceColumnId, out var column))
                throw new InvalidOperationException($"Semantic.csv 引用了不存在的 column.csv Id={sourceColumnId}。");

            _context.MetadataSemantics.Add(new MetadataSemantic
            {
                MetadataColumnId = column.Id,
                MetadataColumn = column,
                BusinessMeaning = Null(row, 2),
                Keywords = Null(row, 3),
                Synonyms = Null(row, 4),
                ExampleQuestions = Null(row, 5),
                BusinessDomain = Null(row, 6),
                Confidence = NullableDecimal(row, 7),
                Source = Null(row, 8),
                CreatedTime = Date(row, 9),
                SearchText = Null(row, 10),
                VectorId = Null(row, 11),
                EmbeddingModel = Null(row, 12),
                VectorDimension = NullableInt(row, 13)
            });
        }
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return new MetadataCsvFixtureResult
        {
            TableCount = tableRows.Count,
            ColumnCount = columnRows.Count,
            SemanticCount = semanticRows.Count
        };
    }

    private static void Validate(IReadOnlyList<string[]> tables, IReadOnlyList<string[]> columns, IReadOnlyList<string[]> semantics)
    {
        if (tables.Count == 0 || columns.Count == 0 || semantics.Count == 0)
            throw new InvalidOperationException("Metadata CSV Fixture 不能为空。");
        if (tables.Any(x => x.Length < 11)) throw new InvalidOperationException("table.csv 字段数量不足。");
        if (columns.Any(x => x.Length < 12)) throw new InvalidOperationException("column.csv 字段数量不足。");
        if (semantics.Any(x => x.Length < 14)) throw new InvalidOperationException("Semantic.csv 字段数量不足。");
    }

    private static async Task<List<string[]>> ReadCsvAsync(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Metadata CSV Fixture 文件不存在。", path);
        var rows = new List<string[]>();
        await using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream, new UTF8Encoding(true));
        while (await reader.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            rows.Add(ParseCsvLine(line));
        }
        return rows;
    }

    private static string[] ParseCsvLine(string line)
    {
        var values = new List<string>();
        var value = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"') { value.Append('"'); i++; }
                else quoted = !quoted;
                continue;
            }
            if (c == ',' && !quoted) { values.Add(value.ToString()); value.Clear(); continue; }
            value.Append(c);
        }
        values.Add(value.ToString());
        return values.ToArray();
    }

    private static long Long(string[] row, int index) => long.Parse(row[index], CultureInfo.InvariantCulture);
    private static string? Null(string[] row, int index) => string.IsNullOrWhiteSpace(row[index]) || string.Equals(row[index], "NULL", StringComparison.OrdinalIgnoreCase) ? null : row[index];
    private static DateTime Date(string[] row, int index) => DateTime.Parse(row[index], CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);
    private static long? NullableLong(string[] row, int index) => Null(row, index) is { } value && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;
    private static int? NullableInt(string[] row, int index) => Null(row, index) is { } value && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;
    private static decimal? NullableDecimal(string[] row, int index) => Null(row, index) is { } value && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) ? result : null;
    private static bool? NullableBool(string[] row, int index) => Null(row, index) switch { "0" => false, "1" => true, "true" => true, "false" => false, _ => null };
}
