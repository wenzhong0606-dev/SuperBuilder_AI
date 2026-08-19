using Microsoft.AspNetCore.Mvc;
using SuperBulider_AI.Models.BI;
using SuperBulider_AI.Models.BI.Evaluation;
using SuperBulider_AI.Services.BI.Evaluation;

namespace SuperBulider_AI.Controllers;

/// <summary>
/// Phase 2.6 Evaluation Dataset 序列化诊断入口。
/// 仅用于验证 Golden Dataset Contract，不执行 QueryPlan Evaluation。
/// </summary>
[ApiController]
[Route("evaluation/diagnostics")]
public sealed class EvaluationDiagnosticsController : ControllerBase
{
    private readonly GoldenQueryDatasetSerializer _serializer;

    public EvaluationDiagnosticsController(GoldenQueryDatasetSerializer serializer)
    {
        _serializer = serializer;
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
                    "semanticText": "销售金额",
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
                     && expected.Metrics[0].SemanticText == "销售金额"
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
