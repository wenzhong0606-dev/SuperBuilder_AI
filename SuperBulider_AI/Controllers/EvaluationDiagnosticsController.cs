using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;
using SuperBuilder_AI.Services.BI.Evaluation;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// Phase 2.6 Evaluation Dataset 诊断入口。
/// 用于验证 Golden Dataset Contract、源码资产加载、运行时 Semantic 检索诊断、
/// Semantic Applicability、QueryPlan Evaluation Gate 与真实 QueryPlan Evaluation，不执行 Repair。
/// </summary>
[ApiController]
[Route("evaluation/diagnostics")]
public sealed class EvaluationDiagnosticsController : ControllerBase
{
	private readonly GoldenQueryDatasetSerializer _serializer;
	private readonly IWebHostEnvironment _environment;
	private readonly IMetadataSemanticSearchService _metadataSemanticSearchService;
	private readonly SemanticApplicabilityEvaluator _semanticApplicabilityEvaluator;
	private readonly QueryPlanEvaluationGate _queryPlanEvaluationGate;
	private readonly IQueryUnderstandingService _queryUnderstandingService;
	private readonly IQueryPlanBuilder _queryPlanBuilder;
	private readonly QueryPlanEvaluator _queryPlanEvaluator;

	public EvaluationDiagnosticsController(
		GoldenQueryDatasetSerializer serializer,
		IWebHostEnvironment environment,
		IMetadataSemanticSearchService metadataSemanticSearchService,
		SemanticApplicabilityEvaluator semanticApplicabilityEvaluator,
		QueryPlanEvaluationGate queryPlanEvaluationGate,
		IQueryUnderstandingService queryUnderstandingService,
		IQueryPlanBuilder queryPlanBuilder,
		QueryPlanEvaluator queryPlanEvaluator)
	{
		_serializer = serializer;
		_environment = environment;
		_metadataSemanticSearchService = metadataSemanticSearchService;
		_semanticApplicabilityEvaluator = semanticApplicabilityEvaluator;
		_queryPlanEvaluationGate = queryPlanEvaluationGate;
		_queryUnderstandingService = queryUnderstandingService;
		_queryPlanBuilder = queryPlanBuilder;
		_queryPlanEvaluator = queryPlanEvaluator;
	}

	[HttpGet("golden-dataset")]
	public ActionResult<object> GoldenDataset()
	{
		var path = Path.Combine(_environment.ContentRootPath, "Evaluation", "Golden", "query-plan-golden-v1.json");
		if (!System.IO.File.Exists(path))
			return NotFound(new { passed = false, message = "Golden Dataset asset was not found.", path });

		var dataset = _serializer.Deserialize(System.IO.File.ReadAllText(path));
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
				dimensionsState = x.Expected.Dimensions is null ? "null" : x.Expected.Dimensions.Count == 0 ? "empty" : "values",
				filtersState = x.Expected.Filters is null ? "null" : x.Expected.Filters.Count == 0 ? "empty" : "values"
			}),
			sourcePath = path
		});
	}

	[HttpGet("semantic")]
	public async Task<ActionResult<object>> Semantic([FromQuery] string question, [FromQuery] int topK = 10)
	{
		if (string.IsNullOrWhiteSpace(question))
			return BadRequest(new { passed = false, message = "question is required." });
		if (topK < 1 || topK > 100)
			return BadRequest(new { passed = false, message = "topK must be between 1 and 100." });

		var results = await _metadataSemanticSearchService.SearchAsync(question, topK);
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

	[HttpGet("applicability")]
	public async Task<ActionResult<object>> Applicability([FromQuery] string caseId, [FromQuery] int topK = 10)
	{
		if (string.IsNullOrWhiteSpace(caseId))
			return BadRequest(new { passed = false, message = "caseId is required." });
		if (topK < 1 || topK > 100)
			return BadRequest(new { passed = false, message = "topK must be between 1 and 100." });

		var goldenCase = LoadGoldenCase(caseId);
		if (goldenCase is null)
			return NotFound(new { passed = false, message = $"Golden Case '{caseId}' was not found." });

		var result = await _semanticApplicabilityEvaluator.EvaluateAsync(goldenCase, topK);
		return Ok(new { passed = result.State == "Resolved", result });
	}

	/// <summary>
	/// Phase 2.6.3.5-C.3 QueryPlan Evaluation Gate。
	/// 先执行 C.2 Applicability，再由 Gate 决定 PASS / BLOCK / REVIEW。
	/// </summary>
	[HttpGet("query-plan-gate")]
	public async Task<ActionResult<object>> QueryPlanGate([FromQuery] string caseId, [FromQuery] int topK = 10)
	{
		if (string.IsNullOrWhiteSpace(caseId))
			return BadRequest(new { passed = false, message = "caseId is required." });
		if (topK < 1 || topK > 100)
			return BadRequest(new { passed = false, message = "topK must be between 1 and 100." });

		var goldenCase = LoadGoldenCase(caseId);
		if (goldenCase is null)
			return NotFound(new { passed = false, message = $"Golden Case '{caseId}' was not found." });

		var applicability = await _semanticApplicabilityEvaluator.EvaluateAsync(goldenCase, topK);
		var decision = _queryPlanEvaluationGate.Evaluate(applicability);
		return Ok(new { passed = decision.Decision == "PASS", decision, applicability });
	}

	/// <summary>
	/// Phase 2.6.4：使用真实 QueryUnderstandingService + QueryPlanBuilder 生成 Runtime QueryPlan，
	/// 再交给 QueryPlanEvaluator 与 Golden Contract 比较。
	/// 不执行 SQL、不执行 Repair、不修改 Metadata 或 Qdrant。
	/// </summary>
	[HttpGet("query-plan-evaluation")]
	public async Task<ActionResult<object>> QueryPlanEvaluation([FromQuery] string caseId, [FromQuery] int topK = 10)
	{
		if (string.IsNullOrWhiteSpace(caseId))
			return BadRequest(new { passed = false, message = "caseId is required." });
		if (topK < 1 || topK > 100)
			return BadRequest(new { passed = false, message = "topK must be between 1 and 100." });

		var goldenCase = LoadGoldenCase(caseId);
		if (goldenCase is null)
			return NotFound(new { passed = false, message = $"Golden Case '{caseId}' was not found." });

		var applicability = await _semanticApplicabilityEvaluator.EvaluateAsync(goldenCase, topK);
		var decision = _queryPlanEvaluationGate.Evaluate(applicability);
		if (decision.Blocking)
		{
			return Ok(new
			{
				passed = false,
				stage = "SemanticApplicabilityGate",
				decision,
				applicability
			});
		}

		var intent = await _queryUnderstandingService.UnderstandAsync(goldenCase.Question);
		var runtimePlan = await _queryPlanBuilder.BuildAsync(intent);
		var evaluation = _queryPlanEvaluator.Evaluate(goldenCase.Id, goldenCase.Expected, runtimePlan);

		return Ok(new
		{
			passed = evaluation.Passed,
			stage = "QueryPlanEvaluation",
			caseId = goldenCase.Id,
			question = goldenCase.Question,
			applicability,
			gate = decision,
			intent = new
			{
				intent.OriginalQuestion,
				intent.IntentType,
				metrics = intent.Metrics.Select(x => new
				{
					x.Name,
					x.Field,
					aggregation = x.GetAggregation().ToString(),
					x.SemanticType
				}),
				dimensions = intent.Dimensions,
				filters = intent.Filters.Count
			},
			runtimePlan = new
			{
				intentType = runtimePlan.Intent?.IntentType.ToString(),
				metrics = runtimePlan.Metrics.Select(x => new
				{
					x.Name,
					x.Field,
					aggregation = x.GetAggregation().ToString(),
					x.SemanticType
				}),
				dimensions = runtimePlan.Dimensions.Count,
				filters = runtimePlan.Filters.Count,
				tables = runtimePlan.Tables.Count,
				joins = runtimePlan.Joins.Count,
				isAggregate = runtimePlan.IsAggregate,
				distinct = runtimePlan.Distinct,
				limit = runtimePlan.Limit,
				isRanking = runtimePlan.IsRanking,
				isDetailRanking = runtimePlan.IsDetailRanking,
				isAggregateRanking = runtimePlan.IsAggregateRanking
			},
			evaluation
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
                "metrics": [{ "semanticText": "入库数量", "aggregation": "sum" }]
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
			metricsState = expected?.Metrics is null ? "null" : expected.Metrics.Count == 0 ? "empty" : "values",
			dimensionsState = expected?.Dimensions is null ? "null" : expected.Dimensions.Count == 0 ? "empty" : "values",
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
        { "version": "1.0", "dataset": "query-plan-golden", "cases": [{ "id": "STATE-NULL", "name": "Missing Collection", "question": "验证缺失集合", "expected": {} }] }
        """;
		const string emptyJson = """
        { "version": "1.0", "dataset": "query-plan-golden", "cases": [{ "id": "STATE-EMPTY", "name": "Empty Collection", "question": "验证空集合", "expected": { "metrics": [] } }] }
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

	private GoldenQueryCase? LoadGoldenCase(string caseId)
	{
		var path = Path.Combine(_environment.ContentRootPath, "Evaluation", "Golden", "query-plan-golden-v1.json");
		if (!System.IO.File.Exists(path))
			return null;

		var dataset = _serializer.Deserialize(System.IO.File.ReadAllText(path));
		return dataset.Cases.SingleOrDefault(x => string.Equals(x.Id, caseId, StringComparison.OrdinalIgnoreCase));
	}
}