using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Services;

/// <summary>
/// CI 专用的确定性元数据语义生成器。
/// 不调用任何外部 LLM；用于让 DataSource -> Scan -> Semantic -> Vector E2E
/// 在零 AI Token 的前提下真实走完数据库与索引链路。
/// </summary>
public sealed class CiFakeMetadataSemanticService : IMetadataSemanticService
{
    private const int ProgressBatchSize = 25;
    private readonly SuperBIContext _context;

    public CiFakeMetadataSemanticService(SuperBIContext context)
    {
        _context = context;
    }

    public async Task<MetadataSemantic?> GenerateAsync(MetadataColumn column)
    {
        var result = await GenerateBatchAsync(new List<MetadataColumn> { column });
        return result.FirstOrDefault();
    }

    public Task<List<MetadataSemantic>> GenerateBatchAsync(List<MetadataColumn> columns)
        => GenerateBatchAsync(columns, null, CancellationToken.None);

    public async Task<List<MetadataSemantic>> GenerateBatchAsync(
        List<MetadataColumn> columns,
        Action<SemanticGenerationProgress>? progress,
        CancellationToken ct)
    {
        var pending = columns
            .Where(x => x.Semantic == null)
            .ToList();

        if (pending.Count == 0)
            return new List<MetadataSemantic>();

        var result = new List<MetadataSemantic>(pending.Count);
        var totalBatches = (int)Math.Ceiling(pending.Count / (double)ProgressBatchSize);
        var completed = 0;
        var batchNo = 0;

        foreach (var batch in pending.Chunk(ProgressBatchSize))
        {
            ct.ThrowIfCancellationRequested();
            batchNo++;

            foreach (var column in batch)
            {
                var tableName = column.MetadataTable?.TableName ?? "table";
                var meaning = $"CI语义：{tableName}.{column.ColumnName}";
                var semantic = new MetadataSemantic
                {
                    MetadataColumnId = column.Id,
                    BusinessMeaning = meaning,
                    Keywords = MetadataSemantic.FormatList(new[] { column.ColumnName, tableName }),
                    Synonyms = column.ColumnName,
                    ExampleQuestions = $"查询 {column.ColumnName}",
                    BusinessDomain = "CI-E2E",
                    Confidence = 1m,
                    Source = SemanticSource.AI,
                    SearchText = $"{tableName} {column.ColumnName} {column.ColumnComment} {meaning}"
                };

                _context.MetadataSemantics.Add(semantic);
                result.Add(semantic);
            }

            completed += batch.Length;
            progress?.Invoke(new SemanticGenerationProgress(
                batchNo,
                totalBatches,
                completed,
                pending.Count,
                completed,
                0,
                $"CI Fake Semantic：{completed}/{pending.Count}"));
        }

        await _context.SaveChangesAsync(ct);
        return result;
    }
}
