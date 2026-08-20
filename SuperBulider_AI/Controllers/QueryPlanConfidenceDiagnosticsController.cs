using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;
using SuperBuilder_AI.Services.BI.Evaluation;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// Phase 2.4-E QueryPlan Confidence 诊断入口。
///
/// 与原 QueryPlan Evaluation Endpoint 隔离：
/// 1. 先执行 Semantic Applicability Gate；
/// 2. 构建 Runtime QueryPlan；
/// 3. 执行现有 Validation + Repair Pipeline；
/// 4. 消费真实 ValidationResult + RepairTrace 计算 Confidence；
/// 5. 不执行 SQL。
///
/// 该入口用于 Confidence Evidence 校准，不改变原有 GQ-001/GQ-002 回归接口。
/// </summary>
[ApiController]
[Route("evaluation/diagnostics")]
public sealed class QueryPlanConfidenceDiagnosticsController : ControllerBase
{
    private readonly GoldenQueryDatasetSerializer _serializer;
    private readonly IWebHostEnvironment _environment;
    private readonly SemanticApplicabilityEvaluator _semanticApplicabilityEvaluator;
    private readonly QueryPlanEvaluationGate _queryPlanEvaluationGate;
    private readonly IQueryUnderstandingService _queryUnderstandingService;
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
        IQueryUnderstandingService queryUnderstandingService,
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
        _queryUnderstandingService = queryUnderstandingService;
        _queryPlanBuilder = queryPlanBuilder;
        _queryPlanEvaluator = queryPlanEvaluator;
        _queryPlanContextBuilder = queryPlanContextBuilder;
        _queryPlanValidationPipeline = queryPlanValidationPipeline;
        _queryPlanConfidenceService = queryPlanConfidenceService;
    }

    /// <summary>
    /// 评估 QueryPlan Confidence Evidence。
    ///
    /// 注意：该接口会调用现有 Validation + Repair Pipeline，因此它是诊断/校准入口，
    /// 不应被 SQL 执行链直接调用。
    /// </summary>
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

        var applicability = await _semanticApplicabilityEvaluator
            .EvaluateAsync(goldenCase, topK);

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

        var intent = await _queryUnderstandingService
            .UnderstandAsync(goldenCase.Question);

        var resolution = QueryPlanSemanticResolutionFactory.From(applicability);
        var runtimePlan = await _queryPlanBuilder.BuildAsync(intent, resolution);

        var evaluation = _queryPlanEvaluator
            .Evaluate(goldenCase.Id, goldenCase.Expected, runtimePlan);

        var validationContext = await _queryPlanContextBuilder
            .BuildAsync(runtimePlan);

        var validationResult = await _queryPlanValidationPipeline
            .ValidateAsync(runtimePlan, validationContext, goldenCase.Question);

        cancellationToken.ThrowIfCancellationRequested();

        var confidence = await _queryPlanConfidenceService
            .EvaluateAsync(
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
                passed = validationResult.ValidationResult?.Passed ?? false,
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
