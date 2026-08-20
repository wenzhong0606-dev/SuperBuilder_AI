using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.5.2 / C.6.3 Golden Dataset Regression Runner。
/// 批量执行 Semantic Applicability → Gate → QueryPlan → Validation/Repair
/// → Evaluation-aware Confidence → Calibration。
/// 不生成 SQL。
/// </summary>
public sealed class GoldenDatasetRunner
{
    private readonly GoldenQueryDatasetSerializer _serializer;
    private readonly SemanticApplicabilityEvaluator _applicabilityEvaluator;
    private readonly QueryPlanEvaluationGate _gate;
    private readonly IQueryUnderstandingService _queryUnderstandingService;
    private readonly IQueryPlanBuilder _queryPlanBuilder;
    private readonly IQueryPlanContextBuilder _queryPlanContextBuilder;
    private readonly IQueryPlanValidationPipeline _validationPipeline;
    private readonly QueryPlanEvaluationConfidenceService _evaluationConfidenceService;
    private readonly GoldenConfidenceCalibrationRunner _calibrationRunner;

    public GoldenDatasetRunner(
        GoldenQueryDatasetSerializer serializer,
        SemanticApplicabilityEvaluator applicabilityEvaluator,
        QueryPlanEvaluationGate gate,
        IQueryUnderstandingService queryUnderstandingService,
        IQueryPlanBuilder queryPlanBuilder,
        IQueryPlanContextBuilder queryPlanContextBuilder,
        IQueryPlanValidationPipeline validationPipeline,
        QueryPlanEvaluationConfidenceService evaluationConfidenceService,
        GoldenConfidenceCalibrationRunner calibrationRunner)
    {
        _serializer = serializer;
        _applicabilityEvaluator = applicabilityEvaluator;
        _gate = gate;
        _queryUnderstandingService = queryUnderstandingService;
        _queryPlanBuilder = queryPlanBuilder;
        _queryPlanContextBuilder = queryPlanContextBuilder;
        _validationPipeline = validationPipeline;
        _evaluationConfidenceService = evaluationConfidenceService;
        _calibrationRunner = calibrationRunner;
    }

    public async Task<GoldenDatasetRunResult> RunAsync(
        string json,
        int topK = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Golden Dataset JSON is required.", nameof(json));
        if (topK < 1 || topK > 100)
            throw new ArgumentOutOfRangeException(nameof(topK), "topK must be between 1 and 100.");

        var dataset = _serializer.Deserialize(json);
        var cases = dataset.Cases ?? new List<GoldenQueryCase>();
        var results = new List<GoldenCaseRunResult>(cases.Count);
        var confidenceResults = new List<QueryPlanEvaluationConfidenceResult>();

        foreach (var goldenCase in cases)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!goldenCase.Enabled)
            {
                results.Add(new GoldenCaseRunResult
                {
                    CaseId = goldenCase.Id,
                    Question = goldenCase.Question,
                    Category = GetCategory(goldenCase),
                    Enabled = false,
                    Stage = "Disabled",
                    Decision = "SKIP",
                    Passed = true,
                    Reason = "Golden Case is disabled."
                });
                continue;
            }

            var (caseResult, confidenceResult) =
                await RunCaseAsync(goldenCase, topK, cancellationToken);

            results.Add(caseResult);
            if (confidenceResult is not null)
                confidenceResults.Add(confidenceResult);
        }

        var calibration = confidenceResults.Count == 0
            ? null
            : _calibrationRunner.Run(confidenceResults);

        return new GoldenDatasetRunResult
        {
            Dataset = dataset.Dataset,
            Version = dataset.Version,
            Total = cases.Count,
            Executed = results.Count(x => x.Enabled),
            Passed = results.Count(x => x.Enabled && x.Passed),
            Failed = results.Count(x => x.Enabled && !x.Passed),
            Blocked = results.Count(x => x.Enabled && x.Decision == "BLOCK"),
            Review = results.Count(x => x.Enabled && x.Decision == "REVIEW"),
            Unresolved = results.Count(x => x.Enabled && string.Equals(x.ApplicabilityState, "NotResolved", StringComparison.OrdinalIgnoreCase)),
            Ambiguous = results.Count(x => x.Enabled && string.Equals(x.ApplicabilityState, "Ambiguous", StringComparison.OrdinalIgnoreCase)),
            Cases = results,
            ConfidenceResults = confidenceResults,
            ConfidenceCalibration = calibration
        };
    }

    private async Task<(GoldenCaseRunResult CaseResult, QueryPlanEvaluationConfidenceResult? ConfidenceResult)> RunCaseAsync(
        GoldenQueryCase goldenCase,
        int topK,
        CancellationToken cancellationToken)
    {
        SemanticApplicabilityResult applicability;
        try
        {
            applicability = await _applicabilityEvaluator.EvaluateAsync(goldenCase, topK);
        }
        catch (Exception ex)
        {
            return (Fail(goldenCase, "SemanticApplicability", "ERROR", $"Semantic Applicability 执行异常：{ex.Message}"), null);
        }

        var decision = _gate.Evaluate(applicability);
        var category = GetCategory(goldenCase);

        if (string.Equals(category, "unresolved", StringComparison.OrdinalIgnoreCase))
            return (ExpectedApplicabilityOutcome(goldenCase, applicability, decision, "NotResolved"), null);

        if (string.Equals(category, "ambiguous", StringComparison.OrdinalIgnoreCase))
            return (ExpectedApplicabilityOutcome(goldenCase, applicability, decision, "Ambiguous"), null);

        if (decision.Blocking)
        {
            return (new GoldenCaseRunResult
            {
                CaseId = goldenCase.Id,
                Question = goldenCase.Question,
                Category = category,
                Enabled = true,
                Stage = "SemanticApplicabilityGate",
                Decision = decision.Decision,
                Passed = false,
                Reason = decision.Reason,
                ApplicabilityState = applicability.State
            }, null);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var intent = await _queryUnderstandingService.UnderstandAsync(goldenCase.Question);
            var resolution = QueryPlanSemanticResolutionFactory.From(applicability);
            var runtimePlan = await _queryPlanBuilder.BuildAsync(intent, resolution);

            var validationContext = await _queryPlanContextBuilder.BuildAsync(runtimePlan);
            var validationPipelineResult = await _validationPipeline.ValidateAsync(
                runtimePlan,
                validationContext,
                goldenCase.Question);

            var evaluationConfidence = await _evaluationConfidenceService.EvaluateAsync(
                goldenCase.Id,
                goldenCase.Question,
                goldenCase.Expected,
                validationPipelineResult.Plan,
                validationPipelineResult,
                validationPipelineResult.RepairTrace,
                applicability,
                cancellationToken);

            var passed = evaluationConfidence.Passed;
            var caseResult = new GoldenCaseRunResult
            {
                CaseId = goldenCase.Id,
                Question = goldenCase.Question,
                Category = category,
                Enabled = true,
                Stage = passed ? "EvaluationConfidence" : "EvaluationConfidenceFailed",
                Decision = passed ? "PASS" : "FAIL",
                Passed = passed,
                Reason = passed
                    ? "QueryPlan Evaluation、Semantic Evidence、Confidence Decision 均通过。"
                    : evaluationConfidence.Decision.Reason,
                ApplicabilityState = applicability.State,
                QueryPlanEvaluationPassed = evaluationConfidence.Evaluation.Passed,
                ConfidenceDecision = evaluationConfidence.Decision.Decision.ToString(),
                ConfidenceLevel = evaluationConfidence.Confidence.Level.ToString(),
                ConfidenceScore = evaluationConfidence.Confidence.Score
            };

            return (caseResult, evaluationConfidence);
        }
        catch (Exception ex)
        {
            return (new GoldenCaseRunResult
            {
                CaseId = goldenCase.Id,
                Question = goldenCase.Question,
                Category = category,
                Enabled = true,
                Stage = "QueryPlanValidationConfidence",
                Decision = "ERROR",
                Passed = false,
                Reason = $"QueryPlan Validation/Confidence 执行异常：{ex.Message}",
                ApplicabilityState = applicability.State,
                QueryPlanEvaluationPassed = false
            }, null);
        }
    }

    private static GoldenCaseRunResult ExpectedApplicabilityOutcome(
        GoldenQueryCase goldenCase,
        SemanticApplicabilityResult applicability,
        QueryPlanEvaluationDecision decision,
        string expectedState)
    {
        var passed = string.Equals(applicability.State, expectedState, StringComparison.OrdinalIgnoreCase);
        return new GoldenCaseRunResult
        {
            CaseId = goldenCase.Id,
            Question = goldenCase.Question,
            Category = GetCategory(goldenCase),
            Enabled = true,
            Stage = "SemanticApplicabilityGate",
            Decision = decision.Decision,
            Passed = passed,
            Reason = passed
                ? $"Golden Case 预期 Applicability={expectedState}，实际匹配。"
                : $"Golden Case 预期 Applicability={expectedState}，实际为 {applicability.State}。",
            ApplicabilityState = applicability.State
        };
    }

    private static GoldenCaseRunResult Fail(GoldenQueryCase item, string stage, string decision, string reason) => new()
    {
        CaseId = item.Id,
        Question = item.Question,
        Category = GetCategory(item),
        Enabled = item.Enabled,
        Stage = stage,
        Decision = decision,
        Passed = false,
        Reason = reason
    };

    private static string GetCategory(GoldenQueryCase item)
    {
        if (item.Tags?.Any(x => string.Equals(x, "negative", StringComparison.OrdinalIgnoreCase)) == true) return "negative";
        if (item.Tags?.Any(x => string.Equals(x, "ambiguous", StringComparison.OrdinalIgnoreCase)) == true) return "ambiguous";
        if (item.Tags?.Any(x => string.Equals(x, "unresolved", StringComparison.OrdinalIgnoreCase)) == true) return "unresolved";
        return "positive";
    }
}