using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;
using SuperBuilder_AI.Services.BI.Evaluation;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// Phase 2.4-E QueryPlan Confidence 诊断入口。
/// 使用 Golden Case 已确定的 Intent 构造 QueryIntent，避免 Confidence 校准被外部 Qwen API 状态干扰。
/// </summary>
[ApiController]
[Route("evaluation/diagnostics")]
public sealed class QueryPlanConfidenceDiagnosticsController : ControllerBase
{
    private readonly GoldenQueryDatasetSerializer _serializer;
    private readonly IWebHostEnvironment _environment;
    private readonly SemanticApplicabilityEvaluator _semanticApplicabilityEvaluator;
    private readonly QueryPlanEvaluationGate _queryPlanEvaluationGate;
    private readonly IQueryPlanBuilder _queryPlanBuilder;
    private readonly QueryPlanEvaluator _queryPlanEvaluator;
    private readonly IQueryPlanContextBuilder _queryPlanContextBuilder;
    private readonly IQueryPlanValidationPipeline _queryPlanValidationPipeline;
    private readonly IQueryPlanConfidenceService _queryPlanConfidenceService;

    public QueryPlanConfidenceDiagnosticsController(
        GoldenQueryDatasetSerializer serializer,
        IWebHostEnvironment environment,
        SemanticApplicabilityEvaluator semanticApplicabilityEvaluator,
        QueryPlanEvaluationGate queryPlanEvaluationGate,
        IQueryPlanBuilder queryPlanBuilder,
        QueryPlanEvaluator queryPlanEvaluator,
        IQueryPlanContextBuilder queryPlanContextBuilder,
        IQueryPlanValidationPipeline queryPlanValidationPipeline,
        IQueryPlanConfidenceService queryPlanConfidenceService)
    {
        _serializer = serializer;
        _environment = environment;
        _semanticApplicabilityEvaluator = semanticApplicabilityEvaluator;
        _queryPlanEvaluationGate = queryPlanEvaluationGate;
        _queryPlanBuilder = queryPlanBuilder;
        _queryPlanEvaluator = queryPlanEvaluator;
        _queryPlanContextBuilder = queryPlanContextBuilder;
        _queryPlanValidationPipeline = queryPlanValidationPipeline;
        _queryPlanConfidenceService = queryPlanConfidenceService;
    }

    [HttpGet("query-plan-confidence")]
    public async Task<ActionResult<object>> QueryPlanConfidence(
        [FromQuery] string caseId,
        [FromQuery] int topK = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(caseId))
            return BadRequest(new { passed = false, message = "caseId is required." });

        if (topK < 1 || topK > 100)
            return BadRequest(new { passed = false, message = "topK must be between 1 and 100." });

        var goldenCase = LoadGoldenCase(caseId);
        if (goldenCase is null)
            return NotFound(new { passed = false, message = $"Golden Case '{caseId}' was not found." });

        var applicability = await _semanticApplicabilityEvaluator.EvaluateAsync(goldenCase, topK);
        var gate = _queryPlanEvaluationGate.Evaluate(applicability);

        if (gate.Blocking)
        {
            return Ok(new
            {
                passed = false,
                stage = "SemanticApplicabilityGate",
                decision = gate,
                applicability,
                confidence = (object?)null
            });
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Confidence 校准必须可重复。Golden Case 已经定义期望 Intent，因此这里不再次调用 Qwen。
        var intent = BuildIntentFromGoldenCase(goldenCase);
        var resolution = QueryPlanSemanticResolutionFactory.From(applicability);
        var runtimePlan = await _queryPlanBuilder.BuildAsync(intent, resolution);
        var evaluation = _queryPlanEvaluator.Evaluate(goldenCase.Id, goldenCase.Expected, runtimePlan);

        var validationContext = await _queryPlanContextBuilder.BuildAsync(runtimePlan);
        var validationResult = await _queryPlanValidationPipeline
            .ValidateAsync(runtimePlan, validationContext, goldenCase.Question);

        cancellationToken.ThrowIfCancellationRequested();

        var confidence = await _queryPlanConfidenceService.EvaluateAsync(
            runtimePlan,
            validationResult,
            validationResult.RepairTrace,
            goldenCase.Question,
            cancellationToken);

        return Ok(new
        {
            passed = evaluation.Passed && confidence.CanProceed,
            stage = "QueryPlanConfidence",
            caseId = goldenCase.Id,
            question = goldenCase.Question,
            applicability,
            resolution,
            gate,
            evaluation,
            validation = new
            {
                passed = validationResult.ValidationResult?.IsValid ?? false,
                errors = validationResult.ValidationResult?.ErrorItems.Count() ?? 0,
                warnings = validationResult.ValidationResult?.WarningItems.Count() ?? 0,
                repairStatus = validationResult.RepairTrace?.Status.ToString()
            },
            confidence = new
            {
                confidence.Score,
                level = confidence.Level.ToString(),
                confidence.CanProceed,
                confidence.Evidence,
                confidence.Reasons,
                confidence.BlockingReasons
            }
        });
    }

    private static QueryIntent BuildIntentFromGoldenCase(GoldenQueryCase goldenCase)
    {
        var expectedJson = JsonSerializer.SerializeToElement(goldenCase.Expected);
        var intentType = expectedJson.TryGetProperty("intentType", out var intentTypeElement)
            ? intentTypeElement.GetString() ?? string.Empty
            : string.Empty;

        var intent = new QueryIntent
        {
            OriginalQuestion = goldenCase.Question,
            IntentType = intentType
        };

        if (expectedJson.TryGetProperty("metrics", out var metricsElement) &&
            metricsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var metricElement in metricsElement.EnumerateArray())
            {
                intent.Metrics.Add(new QueryMetric
                {
                    Name = metricElement.TryGetProperty("semanticText", out var semanticText)
                        ? semanticText.GetString() ?? string.Empty
                        : string.Empty,
                    Field = metricElement.TryGetProperty("field", out var field)
                        ? field.GetString() ?? string.Empty
                        : string.Empty,
                    Aggregation = metricElement.TryGetProperty("aggregation", out var aggregation)
                        ? aggregation.GetString() ?? "NONE"
                        : "NONE"
                });
            }
        }

        return intent;
    }

    private GoldenQueryCase? LoadGoldenCase(string caseId)
    {
        var path = Path.Combine(
            _environment.ContentRootPath,
            "Evaluation",
            "Golden",
            "query-plan-golden-v1.json");

        if (!System.IO.File.Exists(path))
            return null;

        var dataset = _serializer.Deserialize(System.IO.File.ReadAllText(path));
        return dataset.Cases.SingleOrDefault(x =>
            string.Equals(x.Id, caseId, StringComparison.OrdinalIgnoreCase));
    }
}
