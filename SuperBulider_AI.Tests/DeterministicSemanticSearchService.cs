using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.BI;

namespace SuperBulider_AI.Tests;

internal sealed class DeterministicSemanticSearchService : IMetadataSemanticSearchService
{
    private readonly IReadOnlyList<MetadataSemanticSearchResult> _results;

    public DeterministicSemanticSearchService(params MetadataSemanticSearchResult[] results)
    {
        _results = results;
    }

    public Task<List<MetadataSemanticSearchResult>> SearchAsync(string question, int topK = 10)
        => Task.FromResult(_results.Take(topK).ToList());
}

internal static class DeterministicSemanticFixture
{
    public static MetadataSemanticSearchResult Metric(
        string semanticText,
        string businessMeaning,
        double score,
        long tableId = 1,
        long columnId = 1)
        => new()
        {
            IsSemanticVector = true,
            VectorType = "Semantic",
            VectorId = $"fixture-{columnId}",
            Score = score,
            Table = new MetadataTable
            {
                Id = tableId,
                TableName = "入库单",
                DataSourceId = 1
            },
            Column = new MetadataColumn
            {
                Id = columnId,
                ColumnName = "quantity"
            },
            Semantic = new MetadataSemantic
            {
                BusinessMeaning = businessMeaning,
                Keywords = semanticText,
                Synonyms = semanticText,
                ExampleQuestions = semanticText
            }
        };
}
