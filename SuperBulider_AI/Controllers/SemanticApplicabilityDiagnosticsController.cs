using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Models.AI;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// Phase 2.6 C.2.1 Semantic Applicability 证据诊断接口。
/// 仅用于观察真实 Metadata Semantic Search 返回结果，不改变 Applicability 判定逻辑。
/// </summary>
[ApiController]
[Route("evaluation/semantic-applicability")]
public sealed class SemanticApplicabilityDiagnosticsController : ControllerBase
{
    private readonly IMetadataSemanticSearchService _semanticSearchService;

    public SemanticApplicabilityDiagnosticsController(IMetadataSemanticSearchService semanticSearchService)
    {
        _semanticSearchService = semanticSearchService;
    }

    [HttpGet("debug")]
    public async Task<ActionResult<object>> Debug(
        [FromQuery] string question,
        [FromQuery] int topK = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
            return BadRequest(new { passed = false, message = "question 不能为空。" });

        if (topK is < 1 or > 100)
            return BadRequest(new { passed = false, message = "topK 必须在 1 到 100 之间。" });

        cancellationToken.ThrowIfCancellationRequested();

        var results = await _semanticSearchService.SearchAsync(question, topK);

        var candidates = results
            .Select((x, index) => new
            {
                rank = index + 1,
                score = x.Score,
                vectorType = x.VectorType,
                vectorId = x.VectorId,
                isSemanticVector = x.IsSemanticVector,
                table = x.Table is null ? null : new
                {
                    id = x.Table.Id,
                    name = x.Table.TableName,
                    comment = x.Table.TableComment,
                    businessDomain = x.Table.BusinessDomain,
                    dataSourceId = x.Table.DataSourceId
                },
                column = x.Column is null ? null : new
                {
                    id = x.Column.Id,
                    name = x.Column.ColumnName,
                    comment = x.Column.ColumnComment,
                    dataType = x.Column.DataType
                },
                semantic = x.Semantic is null ? null : new
                {
                    id = x.Semantic.Id,
                    businessMeaning = x.Semantic.BusinessMeaning,
                    keywords = x.Semantic.Keywords,
                    synonyms = x.Semantic.Synonyms,
                    exampleQuestions = x.Semantic.ExampleQuestions,
                    searchText = x.Semantic.SearchText
                }
            })
            .ToList();

        return Ok(new
        {
            passed = true,
            question,
            topK,
            total = candidates.Count,
            semanticCandidates = candidates.Count(x => x.isSemanticVector),
            tableCandidates = candidates.Count(x => x.table is not null),
            columnCandidates = candidates.Count(x => x.column is not null),
            candidates
        });
    }
}
