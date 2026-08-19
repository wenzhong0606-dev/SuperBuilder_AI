using Microsoft.AspNetCore.Mvc;
using SuperBulider_AI.Interfaces;
using SuperBulider_AI.Models.BI;
using SuperBulider_AI.Models.BI.Evaluation;
using SuperBulider_AI.Services.BI.Evaluation;

namespace SuperBulider_AI.Controllers;

/// <summary>
/// Phase 2.6 Evaluation Dataset 诊断入口。
/// 仅用于验证 Golden Dataset Contract、源码资产加载与运行时 Semantic 检索诊断，
/// 不执行 QueryPlan Evaluation。
/// </summary>
[ApiController]
[Route("evaluation/diagnostics")]
public sealed class EvaluationDiagnosticsController : ControllerBase
{
    private readonly GoldenQueryDatasetSerializer _serializer;
    private readonly IWebHostEnvironment _environment;
    private readonly IMetadataSemanticSearchService _metadataSemanticSearchService;

    public EvaluationDiagnosticsController(
        GoldenQueryDatasetSerializer serializer,
        IWebHostEnvironment environment,
        IMetadataSemanticSearchService metadataSemanticSearchService)
    {
        _serializer = serializer;
        _environment = environment;
        _metadataSemanticSearchService = metadataSemanticSearchService;
    }

    [HttpGet("golden-dataset")]
    public ActionResult<object> GoldenDataset()
    {
        var path = Path.Combine(
            _environment.ContentRootPath,
            "Evaluation",
            "Golden",
            "query-plan-golden-v1.json");

        if (!System.IO.File.Exists(path))
        {
            return NotFound(new
            {
                passed = false,
                message = "Golden Dataset asset was not found.",
                path
            });
        }

        var sourceJson = System.IO.File.ReadAllText(path);
        var dataset = _serializer.Deserialize(sourceJson);

        var gq001 = dataset.Cases.SingleOrDefault(x => x.Id == "GQ-001");
        var gq002 = dataset.Cases.SingleOrDefault(x => x.Id == "GQ-002");

        var gq001Metric = gq001?.Expected?.Metrics?.SingleOrDefault();
        var gq002Metric = gq002?.Expected?.Metrics?.SingleOrDefault();

        var passed = dataset.Version == "1.0"
                     && dataset.Dataset == "query-plan-golden"
                     && dataset.Cases.Count == 2
                     && gq001 is not null
                     && gq002 is not null
                     && gq001Metric?.SemanticText == "入库数量"
                     && gq001Metric.Aggregation == QueryAggregation.Sum
                     && gq002Metric?.SemanticText == "入库单数量"
                     && gq002Metric.Aggregation == QueryAggregation.Count
                     && gq001.Expected.Dimensions is { Count: 0 }
                     && gq002.Expected.Dimensions is { Count: 0 }
                     && gq001.Expected.Filters is null
                     && gq002.Expected.Filters is null
                     && gq001.Expected.Tables is null
                     && gq002.Expected.Tables is null
                     && gq001.Expected.Joins is null
                     && gq002.Expected.Joins is null
                     && gq001.Expected.Orders is null
                     && gq002.Expected.Orders is null
                     && gq001.Expected.IsAggregate == true
                     && gq002.Expected.IsAggregate == true;

        return Ok(new
        {
            passed,
            dataset = dataset.Dataset,
            version = dataset.Version,
            caseCount = dataset.Cases.Count,
            cases = dataset.Cases.Select(x => new
            {
                x.Id,
                x.Name,
                x.Question,
                metric = x.Expected.Metrics?.SingleOrDefault()?.SemanticText,
                aggregation = x.Expected.Metrics?.SingleOrDefault()?.Aggregation.ToString(),
                dimensionsState = x.Expected.Dimensions is null
                    ? "null"
                    : x.Expected.Dimensions.Count == 0 ? "empty" : "values",
                filtersState = x.Expected.Filters is null
                    ? "null"
                    : x.Expected.Filters.Count == 0 ? "empty" : "values"
            }),
            sourcePath = path
        });
    }

    /// <summary>
    /// Phase 2.6.3.5-C.1 Semantic Diagnostic。
    /// 仅观察当前运行时 Metadata + Qdrant 的真实语义检索结果，
    /// 不进行 RESOLVED / NOT_FOUND / AMBIGUOUS 判定，也不设置 Score 阈值。
    /// </summary>
    [HttpGet("semantic")]
    public async Task<ActionResult<object>> Semantic(
        [FromQuery] string question,
        [FromQuery] int topK = 10)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return BadRequest(new
            {
                passed = false,
                message = "question is required."
            });
        }

        if (topK < 1 || topK > 100)
        {
            return BadRequest(new
            {
                passed = false,
                message = "topK must be between 1 and 100."
            });
        }

        var results = await _metadataSemanticSearchService
            .SearchAsync(question, topK);

        return Ok(new
        {
            question,
            topK,
            count = results.Count,
            results = results.Select(x => new
            {
                x.VectorType,
                x.VectorId,
                x.Score,
                businessMeaning = x.Semantic?.BusinessMeaning,
                keywords = x.Semantic?.Keywords,
                synonyms = x.Semantic?.Synonyms,
                exampleQuestions = x.Semantic?.ExampleQuestions,
                table = x.Table?.TableName,
                column = x.Column?.ColumnName,
                dataType = x.Column?.DataType
            })
        });
    }

    [HttpGet("serialization-roundtrip")]
    public ActionResult<object> SerializationRoundTrip()
    {
        const string sourceJson = """
        {
          "version": "1.0",
          "dataset": "query-plan-golden",
          "cases": [
            {
              "id": "SER-001",
              "name": "Serialization Contract",
              "question": "验证 Golden Dataset 序列化契约",
              "expected": {
                "intentType": "Aggregate",
                "dimensions": [],
                "metrics": [
                  {
                    "semanticText": "入库数量",
                    "aggregation": "sum"
                  }
                ]
              },
              "version": "1.0",
              "enabled": true
            }
          ]
        }
        """;

        var dataset = _serializer.Deserialize(sourceJson);
        var caseItem = dataset.Cases.Single();
        var expected = caseItem.Expected;

        var serializedJson = _serializer.Serialize(dataset);

        return Ok(new
        {
            passed = expected is not null
                     && expected.Metrics is { Count: 1 }
                     && expected.Metrics[0].Aggregation == QueryAggregation.Sum
                     && expected.Metrics[0].SemanticText == "入库数量"
                     && expected.Dimensions is { Count: 0 }
                     && !serializedJson.Contains("businessKey", StringComparison.OrdinalIgnoreCase),
            metricsState = expected?.Metrics is null
                ? "null"
                : expected.Metrics.Count == 0 ? "empty" : "values",
            dimensionsState = expected?.Dimensions is null
                ? "null"
                : expected.Dimensions.Count == 0 ? "empty" : "values",
            aggregation = expected?.Metrics?.FirstOrDefault()?.Aggregation.ToString(),
            semanticText = expected?.Metrics?.FirstOrDefault()?.SemanticText,
            containsBusinessKey = serializedJson.Contains("businessKey", StringComparison.OrdinalIgnoreCase),
            serializedJson
        });
    }

    [HttpGet("three-state")]
    public ActionResult<object> ThreeState()
    {
        const string missingJson = """
        {
          "version": "1.0",
          "dataset": "query-plan-golden",
          "cases": [
            {
              "id": "STATE-NULL",
              "name": "Missing Collection",
              "question": "验证缺失集合",
              "expected": {}
            }
          ]
        }
        """;

        const string emptyJson = """
        {
          "version": "1.0",
          "dataset": "query-plan-golden",
          "cases": [
            {
              "id": "STATE-EMPTY",
              "name": "Empty Collection",
              "question": "验证空集合",
              "expected": {
                "metrics": []
              }
            }
          ]
        }
        """;

        var missing = _serializer.Deserialize(missingJson).Cases.Single().Expected;
        var empty = _serializer.Deserialize(emptyJson).Cases.Single().Expected;

        return Ok(new
        {
            missingMetricsIsNull = missing.Metrics is null,
            emptyMetricsIsNotNull = empty.Metrics is not null,
            emptyMetricsCount = empty.Metrics?.Count
        });
    }
}
