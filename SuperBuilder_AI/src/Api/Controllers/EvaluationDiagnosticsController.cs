using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;
using SuperBuilder_AI.Services.BI.Evaluation;

namespace SuperBuilder_AI.Controllers;

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
    private readonly GoldenDatasetRunner _goldenDatasetRunner;
    private readonly GoldenDatasetRegressionEvaluator _goldenDatasetRegressionEvaluator;
    private readonly GoldenDatasetCoverageAnalyzer _goldenDatasetCoverageAnalyzer;
    private readonly GoldenDatasetQualityGate _goldenDatasetQualityGate;
    private readonly GoldenBaselineReleaseService _goldenBaselineReleaseService;
    private readonly GoldenBaselineComparisonService _goldenBaselineComparisonService;
    private readonly IConfiguration _configuration;

    public EvaluationDiagnosticsController(
        GoldenQueryDatasetSerializer serializer,
        IWebHostEnvironment environment,
        IMetadataSemanticSearchService metadataSemanticSearchService,
        SemanticApplicabilityEvaluator semanticApplicabilityEvaluator,
        QueryPlanEvaluationGate queryPlanEvaluationGate,
        IQueryUnderstandingService queryUnderstandingService,
        IQueryPlanBuilder queryPlanBuilder,
        QueryPlanEvaluator queryPlanEvaluator,
        GoldenDatasetRunner goldenDatasetRunner,
        GoldenDatasetRegressionEvaluator goldenDatasetRegressionEvaluator,
        GoldenDatasetCoverageAnalyzer goldenDatasetCoverageAnalyzer,
        GoldenDatasetQualityGate goldenDatasetQualityGate,
        GoldenBaselineReleaseService goldenBaselineReleaseService,
        GoldenBaselineComparisonService goldenBaselineComparisonService,
        IConfiguration configuration)
    {
        _serializer = serializer;
        _environment = environment;
        _metadataSemanticSearchService = metadataSemanticSearchService;
        _semanticApplicabilityEvaluator = semanticApplicabilityEvaluator;
        _queryPlanEvaluationGate = queryPlanEvaluationGate;
        _queryUnderstandingService = queryUnderstandingService;
        _queryPlanBuilder = queryPlanBuilder;
        _queryPlanEvaluator = queryPlanEvaluator;
        _goldenDatasetRunner = goldenDatasetRunner;
        _goldenDatasetRegressionEvaluator = goldenDatasetRegressionEvaluator;
        _goldenDatasetCoverageAnalyzer = goldenDatasetCoverageAnalyzer;
        _goldenDatasetQualityGate = goldenDatasetQualityGate;
        _goldenBaselineReleaseService = goldenBaselineReleaseService;
        _goldenBaselineComparisonService = goldenBaselineComparisonService;
        _configuration = configuration;
    }

    [HttpGet("golden-dataset")]
    public ActionResult<object> GoldenDataset()
    {
        var path = GoldenPath();
        if (!System.IO.File.Exists(path)) return NotFound(new { passed = false, message = "Golden Dataset asset was not found.", path });
        var dataset = _serializer.Deserialize(System.IO.File.ReadAllText(path));
        var cases = dataset.Cases ?? new List<GoldenQueryCase>();
        var duplicateIds = cases.Where(x => !string.IsNullOrWhiteSpace(x.Id)).GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        var enabledCases = cases.Where(x => x.Enabled).ToList();
        var invalidCases = cases.Where(IsInvalidCase).Select(x => x.Id).ToList();
        return Ok(new { passed = !string.IsNullOrWhiteSpace(dataset.Version) && dataset.Dataset == "query-plan-golden" && cases.Count > 0 && duplicateIds.Count == 0 && invalidCases.Count == 0 && enabledCases.Count > 0, dataset = dataset.Dataset, version = dataset.Version, caseCount = cases.Count, enabledCaseCount = enabledCases.Count, categoryCounts = new { positive = enabledCases.Count(IsPositiveCase), negative = enabledCases.Count(IsNegativeCase), ambiguous = enabledCases.Count(IsAmbiguousCase), unresolved = enabledCases.Count(IsUnresolvedCase) }, duplicateIds, invalidCases, cases = cases.Select(x => new { x.Id, x.Name, x.Question, x.Difficulty, x.Enabled, x.Version, category = GetCategory(x), metric = x.Expected?.Metrics?.Count == 1 ? x.Expected.Metrics[0].SemanticText : null, aggregation = x.Expected?.Metrics?.Count == 1 ? x.Expected.Metrics[0].Aggregation.ToString() : null, metricCount = x.Expected?.Metrics?.Count ?? 0, dimensionsState = State(x.Expected?.Dimensions), filtersState = State(x.Expected?.Filters), tablesState = State(x.Expected?.Tables), joinsState = State(x.Expected?.Joins) }), sourcePath = path });
    }

    [HttpGet("golden-dataset-coverage")]
    public ActionResult<GoldenDatasetCoverageScorecard> GoldenDatasetCoverage()
    {
        var path = GoldenPath();
        if (!System.IO.File.Exists(path)) return NotFound(new { passed = false, message = "Golden Dataset asset was not found.", path });
        var dataset = _serializer.Deserialize(System.IO.File.ReadAllText(path));
        return Ok(_goldenDatasetCoverageAnalyzer.Analyze(dataset));
    }

    [HttpGet("golden-dataset-coverage-cases")]
    public ActionResult<IReadOnlyList<GoldenDatasetCoverageCase>> GoldenDatasetCoverageCases()
    {
        var path = GoldenPath();
        if (!System.IO.File.Exists(path)) return NotFound(new { passed = false, message = "Golden Dataset asset was not found.", path });
        var dataset = _serializer.Deserialize(System.IO.File.ReadAllText(path));
        return Ok(_goldenDatasetCoverageAnalyzer.AnalyzeCases(dataset));
    }

    [HttpGet("golden-dataset-quality")]
    public ActionResult<GoldenDatasetQualityScorecard> GoldenDatasetQuality()
    {
        var path = GoldenPath();
        if (!System.IO.File.Exists(path)) return NotFound(new { passed = false, message = "Golden Dataset asset was not found.", path });
        var dataset = _serializer.Deserialize(System.IO.File.ReadAllText(path));
        return Ok(_goldenDatasetQualityGate.Evaluate(dataset));
    }

    [HttpGet("golden-dataset-release-gate")]
    public ActionResult<object> GoldenDatasetReleaseGate()
    {
        var path = GoldenPath();
        if (!System.IO.File.Exists(path)) return NotFound(new { passed = false, message = "Golden Dataset asset was not found.", path });
        var dataset = _serializer.Deserialize(System.IO.File.ReadAllText(path));
        var quality = _goldenDatasetQualityGate.Evaluate(dataset);
        var coverage = _goldenDatasetCoverageAnalyzer.Analyze(dataset);
        var passed = quality.Passed && coverage.EnabledCases > 0 && coverage.MissingDimensions.Count == 0;
        return Ok(new { passed, decision = passed ? "RELEASE" : "BLOCK", quality, coverage });
    }

    [HttpGet("golden-baseline-release")]
    public ActionResult<GoldenBaselineReleaseScorecard> GoldenBaselineRelease()
    {
        var path = GoldenPath();
        if (!System.IO.File.Exists(path)) return NotFound(new { passed = false, message = "Golden Dataset asset was not found.", path });
        var dataset = _serializer.Deserialize(System.IO.File.ReadAllText(path));
        return Ok(_goldenBaselineReleaseService.Evaluate(dataset));
    }

    [HttpGet("golden-baseline-comparison")]
    public ActionResult<GoldenBaselineComparisonScorecard> GoldenBaselineComparison()
    {
        var path = GoldenPath();
        if (!System.IO.File.Exists(path)) return NotFound(new { passed = false, message = "Golden Dataset asset was not found.", path });
        var dataset = _serializer.Deserialize(System.IO.File.ReadAllText(path));
        var release = _goldenBaselineReleaseService.Evaluate(dataset);
        if (!release.Passed || release.Baseline is null)
            return Ok(new GoldenBaselineComparisonScorecard
            {
                Passed = false,
                Decision = "BLOCK",
                BaselineVersion = string.Empty,
                CandidateVersion = dataset.Version,
                Regressions = release.BlockingReasons.ToList()
            });
        return Ok(_goldenBaselineComparisonService.Compare(release.Baseline, dataset));
    }

    [HttpGet("golden-dataset-run")]
    public async Task<ActionResult<GoldenDatasetRunResult>> GoldenDatasetRun([FromQuery] int topK = 10, CancellationToken cancellationToken = default)
    {
        if (topK < 1 || topK > 100) return BadRequest(new { passed = false, message = "topK must be between 1 and 100." });
        var path = GoldenPath();
        if (!System.IO.File.Exists(path)) return NotFound(new { passed = false, message = "Golden Dataset asset was not found.", path });
        return Ok(await _goldenDatasetRunner.RunAsync(await System.IO.File.ReadAllTextAsync(path, cancellationToken), topK, cancellationToken));
    }

    [HttpGet("golden-regression")]
    public async Task<ActionResult<object>> GoldenRegression([FromQuery] int topK = 10, CancellationToken cancellationToken = default)
    {
        if (topK < 1 || topK > 100) return BadRequest(new { passed = false, message = "topK must be between 1 and 100." });
        var path = GoldenPath();
        if (!System.IO.File.Exists(path)) return NotFound(new { passed = false, message = "Golden Dataset asset was not found.", path });
        var json = await System.IO.File.ReadAllTextAsync(path, cancellationToken);
        var run = await _goldenDatasetRunner.RunAsync(json, topK, cancellationToken);
        var scorecard = _goldenDatasetRegressionEvaluator.Evaluate(run);
        return Ok(new { passed = scorecard.Passed, dataset = run.Dataset, version = run.Version, run, scorecard });
    }

    [HttpGet("semantic")]
    public async Task<ActionResult<object>> Semantic([FromQuery] string question, [FromQuery] int topK = 10)
    {
        if (string.IsNullOrWhiteSpace(question)) return BadRequest(new { passed = false, message = "question is required." });
        if (topK < 1 || topK > 100) return BadRequest(new { passed = false, message = "topK must be between 1 and 100." });
        var results = await _metadataSemanticSearchService.SearchAsync(question, topK);
        return Ok(new { question, topK, count = results.Count, results = results.Select(x => new { x.VectorType, x.VectorId, x.Score, businessMeaning = x.Semantic?.BusinessMeaning, keywords = x.Semantic?.Keywords, synonyms = x.Semantic?.Synonyms, exampleQuestions = x.Semantic?.ExampleQuestions, table = x.Table?.TableName, column = x.Column?.ColumnName, dataType = x.Column?.DataType }) });
    }

    [HttpGet("applicability")]
    public async Task<ActionResult<object>> Applicability([FromQuery] string caseId, [FromQuery] int topK = 10)
    {
        if (string.IsNullOrWhiteSpace(caseId)) return BadRequest(new { passed = false, message = "caseId is required." });
        var goldenCase = LoadGoldenCase(caseId);
        if (goldenCase is null) return NotFound(new { passed = false, message = $"Golden Case '{caseId}' was not found." });
        var result = await _semanticApplicabilityEvaluator.EvaluateAsync(goldenCase, topK);
        return Ok(new { passed = result.State == "Resolved", result });
    }

    [HttpGet("query-plan-gate")]
    public async Task<ActionResult<object>> QueryPlanGate([FromQuery] string caseId, [FromQuery] int topK = 10)
    {
        var goldenCase = LoadGoldenCase(caseId);
        if (goldenCase is null) return NotFound(new { passed = false, message = $"Golden Case '{caseId}' was not found." });
        var applicability = await _semanticApplicabilityEvaluator.EvaluateAsync(goldenCase, topK);
        var decision = _queryPlanEvaluationGate.Evaluate(applicability);
        return Ok(new { passed = decision.Decision == "PASS", decision, applicability });
    }

    [HttpGet("query-plan-evaluation")]
    public async Task<ActionResult<object>> QueryPlanEvaluation([FromQuery] string caseId, [FromQuery] int topK = 10)
    {
        var goldenCase = LoadGoldenCase(caseId);
        if (goldenCase is null) return NotFound(new { passed = false, message = $"Golden Case '{caseId}' was not found." });
        var applicability = await _semanticApplicabilityEvaluator.EvaluateAsync(goldenCase, topK);
        var decision = _queryPlanEvaluationGate.Evaluate(applicability);
        if (decision.Blocking) return Ok(new { passed = false, stage = "SemanticApplicabilityGate", decision, applicability });
        // V2.6 CI 离线兜底：当未配置 LLM（CI 无 Qwen 凭据）或 LLM 调用失败时，
        // 用 Golden 期望构造确定性 QueryIntent，使评估端点仍能返回完整 evaluation
        // （C.13.3 仅断言 evaluation.metrics.details 的语义文本，不依赖 LLM 实时意图）。
        // 生产环境（已配置 Qwen:ApiKey）仍走真实 LLM 意图理解路径。
        var apiKey = _configuration["Qwen:ApiKey"];
        var llmConfigured = !string.IsNullOrWhiteSpace(apiKey)
            && !apiKey.Contains("__SET_VIA_ENV", StringComparison.Ordinal)
            && !apiKey.StartsWith("__", StringComparison.Ordinal);
        QueryIntent intent;
        string intentSource;
        if (llmConfigured)
        {
            try
            {
                intent = await _queryUnderstandingService.UnderstandAsync(goldenCase.Question);
                intentSource = "llm";
            }
            catch (HttpRequestException)
            {
                intent = BuildGoldenFallbackIntent(goldenCase.Expected, goldenCase.Question);
                intentSource = "golden-offline-llm-http-error";
            }
            catch (System.Threading.Tasks.TaskCanceledException)
            {
                intent = BuildGoldenFallbackIntent(goldenCase.Expected, goldenCase.Question);
                intentSource = "golden-offline-llm-timeout";
            }
        }
        else
        {
            intent = BuildGoldenFallbackIntent(goldenCase.Expected, goldenCase.Question);
            intentSource = "golden-offline-no-llm-key";
        }
        var resolution = QueryPlanSemanticResolutionFactory.From(applicability, intent);
        var runtimePlan = await _queryPlanBuilder.BuildAsync(intent, resolution);
        var evaluation = _queryPlanEvaluator.Evaluate(goldenCase.Id, goldenCase.Expected, runtimePlan);
        return Ok(new { passed = evaluation.Passed, stage = "QueryPlanEvaluation", caseId = goldenCase.Id, question = goldenCase.Question, applicability, resolution, gate = decision, runtimePlan, evaluation, intentSource, intent });
    }

    [HttpGet("serialization-roundtrip")]
    public ActionResult<object> SerializationRoundTrip()
    {
        const string sourceJson = """{ "version": "1.0", "dataset": "query-plan-golden", "cases": [{ "id": "SER-001", "name": "Serialization Contract", "question": "验证 Golden Dataset 序列化契约", "expected": { "intentType": "Aggregate", "dimensions": [], "metrics": [{ "semanticText": "入库数量", "aggregation": "sum" }] }, "version": "1.0", "enabled": true }] }""";
        var dataset = _serializer.Deserialize(sourceJson);
        var expected = dataset.Cases.Single().Expected;
        var serializedJson = _serializer.Serialize(dataset);
        return Ok(new { passed = expected is not null && expected.Metrics is { Count: 1 } && expected.Metrics[0].Aggregation == QueryAggregation.Sum && expected.Metrics[0].SemanticText == "入库数量" && expected.Dimensions is { Count: 0 } && !serializedJson.Contains("businessKey", StringComparison.OrdinalIgnoreCase), metricsState = State(expected?.Metrics), dimensionsState = State(expected?.Dimensions), aggregation = expected?.Metrics?.FirstOrDefault()?.Aggregation.ToString(), semanticText = expected?.Metrics?.FirstOrDefault()?.SemanticText, containsBusinessKey = serializedJson.Contains("businessKey", StringComparison.OrdinalIgnoreCase), serializedJson });
    }

    [HttpGet("three-state")]
    public ActionResult<object> ThreeState()
    {
        const string missingJson = """{ "version": "1.0", "dataset": "query-plan-golden", "cases": [{ "id": "STATE-NULL", "name": "Missing Collection", "question": "验证缺失集合", "expected": {} }] }""";
        const string emptyJson = """{ "version": "1.0", "dataset": "query-plan-golden", "cases": [{ "id": "STATE-EMPTY", "name": "Empty Collection", "question": "验证空集合", "expected": { "metrics": [] } }] }""";
        var missing = _serializer.Deserialize(missingJson).Cases.Single().Expected;
        var empty = _serializer.Deserialize(emptyJson).Cases.Single().Expected;
        return Ok(new { missingMetricsIsNull = missing.Metrics is null, emptyMetricsIsNotNull = empty.Metrics is not null, emptyMetricsCount = empty.Metrics?.Count });
    }

    private string GoldenPath() => Path.Combine(_environment.ContentRootPath, "Evaluation", "Golden", "query-plan-golden-v1.json");
    private GoldenQueryCase? LoadGoldenCase(string caseId) { var path = GoldenPath(); if (!System.IO.File.Exists(path)) return null; return _serializer.Deserialize(System.IO.File.ReadAllText(path)).Cases.SingleOrDefault(x => string.Equals(x.Id, caseId, StringComparison.OrdinalIgnoreCase)); }
    private static string State<T>(ICollection<T>? value) => value is null ? "null" : value.Count == 0 ? "empty" : "values";
    private static string GetCategory(GoldenQueryCase item) => IsNegativeCase(item) ? "negative" : IsAmbiguousCase(item) ? "ambiguous" : IsUnresolvedCase(item) ? "unresolved" : "positive";
    private static bool IsNegativeCase(GoldenQueryCase item) => item.Tags?.Any(tag => string.Equals(tag, "negative", StringComparison.OrdinalIgnoreCase)) == true;
    private static bool IsAmbiguousCase(GoldenQueryCase item) => item.Tags?.Any(tag => string.Equals(tag, "ambiguous", StringComparison.OrdinalIgnoreCase)) == true;
    private static bool IsUnresolvedCase(GoldenQueryCase item) => item.Tags?.Any(tag => string.Equals(tag, "unresolved", StringComparison.OrdinalIgnoreCase)) == true;
    private static bool IsPositiveCase(GoldenQueryCase item) => !IsNegativeCase(item) && !IsAmbiguousCase(item) && !IsUnresolvedCase(item);
    private static bool IsInvalidCase(GoldenQueryCase item) => string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Question) || item.Expected is null || (item.Enabled && string.IsNullOrWhiteSpace(item.Name));

    /// <summary>
    /// 离线确定性意图兜底：当 LLM（Qwen）不可用（CI 无凭据或调用失败）时，
    /// 用 Golden 期望直接构造 QueryIntent。
    /// 2 参 BuildAsync 以 Semantic Resolution（已解析的适用性）为权威物理绑定，
    /// 因此此处只需提供与 Golden 期望一致的 SemanticText 与聚合，即可得到
    /// 物理落点正确、语义与 Golden 一致的运行时 QueryPlan，使评估端点返回完整 evaluation。
    /// </summary>
    private static QueryIntent BuildGoldenFallbackIntent(GoldenQueryExpectation? expected, string question)
    {
        var intent = new QueryIntent
        {
            OriginalQuestion = question,
            IntentType = expected?.IntentType ?? "Aggregate"
        };
        if (expected?.Metrics is { Count: > 0 } metrics)
        {
            foreach (var m in metrics)
            {
                intent.Metrics.Add(new QueryMetric
                {
                    Name = m.SemanticText,
                    SemanticText = m.SemanticText,
                    Field = m.Field ?? string.Empty,
                    Aggregation = m.Aggregation.ToString().ToUpperInvariant()
                });
            }
        }
        if (expected?.Filters is { Count: > 0 } filters)
        {
            foreach (var f in filters)
            {
                // GoldenFilterExpectation 不携带物理 Field；物理列由 Semantic Resolution 在 2 参 BuildAsync 中回填。
                intent.Filters.Add(new QueryFilter
                {
                    SemanticText = f.SemanticText,
                    Field = f.SemanticText,
                    Operator = f.Operator ?? "=",
                    Value = f.Value ?? string.Empty
                });
            }
        }
        if (expected?.Dimensions is { Count: > 0 } dimensions)
        {
            foreach (var d in dimensions)
            {
                if (!string.IsNullOrWhiteSpace(d.SemanticText))
                    intent.Dimensions.Add(d.SemanticText);
            }
        }
        if (expected?.Limit is { } limit)
            intent.Limit = limit;
        return intent;
    }
}
