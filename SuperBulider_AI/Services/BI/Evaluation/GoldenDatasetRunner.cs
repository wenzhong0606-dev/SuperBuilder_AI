using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.5.2 Golden Dataset Regression Runner。
/// 批量执行 Golden Case 的 Semantic Applicability → Gate → QueryPlan → Evaluator 链路。
/// 不执行 Repair，不修改 Golden Dataset，不直接生成 SQL。
/// </summary>
public sealed class GoldenDatasetRunner
{
    private readonly GoldenQueryDatasetSerializer _serializer;
    private readonly SemanticApplicabilityEvaluator _applicabilityEvaluator;
    private readonly QueryPlanEvaluationGate _gate;
    private readonly IQueryUnderstandingService _queryUnderstandingService;
    private readonly IQueryPlanBuilder _queryPlanBuilder;
    private readonly QueryPlanEvaluator _queryPlanEvaluator;

    public GoldenDatasetRunner(
        GoldenQueryDatasetSerializer serializer,
        SemanticApplicabilityEvaluator applicabilityEvaluator,
        QueryPlanEvaluationGate gate,
        IQueryUnderstandingService queryUnderstandingService,
        IQueryPlanBuilder queryPlanBuilder,
        QueryPlanEvaluator queryPlanEvaluator)
    {
        _serializer = serializer;
        _applicabilityEvaluator = applicabilityEvaluator;
        _gate = gate;
        _queryUnderstandingService = queryUnderstandingService;
        _queryPlanBuilder = queryPlanBuilder;
        _queryPlanEvaluator = queryPlanEvaluator;
    }

    public async Task<GoldenDatasetRunResult> RunAsync(string json, int topK = 10, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Golden Dataset JSON is required.", nameof(json));
        if (topK < 1 || topK > 100) throw new ArgumentOutOfRangeException(nameof(topK), "topK must be between 1 and 100.");

        var dataset = _serializer.Deserialize(json);
        var cases = dataset.Cases ?? new List<GoldenQueryCase>();
        var results = new List<GoldenCaseRunResult>(cases.Count);

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

            results.Add(await RunCaseAsync(goldenCase, topK, cancellationToken));
        }

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
            Cases = results
        };
    }

    private async Task<GoldenCaseRunResult> RunCaseAsync(GoldenQueryCase goldenCase, int topK, CancellationToken cancellationToken)
    {
        SemanticApplicabilityResult applicability;
        try
        {
            applicability = await _applicabilityEvaluator.EvaluateAsync(goldenCase, topK);
        }
        catch (Exception ex)
        {
            return Fail(goldenCase, "SemanticApplicability", "ERROR", $"Semantic Applicability 执行异常：{ex.Message}");
        }

        var decision = _gate.Evaluate(applicability);
        var category = GetCategory(goldenCase);

        if (string.Equals(category, "unresolved", StringComparison.OrdinalIgnoreCase))
            return ExpectedApplicabilityOutcome(goldenCase, applicability, decision, "NotResolved");

        if (string.Equals(category, "ambiguous", StringComparison.OrdinalIgnoreCase))
            return ExpectedApplicabilityOutcome(goldenCase, applicability, decision, "Ambiguous");

        if (decision.Blocking)
        {
            return new GoldenCaseRunResult
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
            };
        }

        try
        {
            var intent = await _queryUnderstandingService.UnderstandAsync(goldenCase.Question);
            var resolution = QueryPlanSemanticResolutionFactory.From(applicability);
            var runtimePlan = await _queryPlanBuilder.BuildAsync(intent, resolution);
            var evaluation = _queryPlanEvaluator.Evaluate(goldenCase.Id, goldenCase.Expected, runtimePlan);

            var semanticEvidence = _queryPlanEvaluator.EvaluateSemanticEvidence(
                goldenCase.Id,
                goldenCase.Expected,
                runtimePlan,
                applicability);

            var passed = evaluation.Passed && semanticEvidence.Passed;
            return new GoldenCaseRunResult
            {
                CaseId = goldenCase.Id,
                Question = goldenCase.Question,
                Category = category,
                Enabled = true,
                Stage = passed ? "QueryPlanEvaluation" : "QueryPlanEvaluationFailed",
                Decision = passed ? "PASS" : "FAIL",
                Passed = passed,
                Reason = passed
                    ? "QueryPlan Evaluation 与 Semantic Evidence 均通过。"
                    : evaluation.Passed ? semanticEvidence.Reason : evaluation.BindingConsistency.Reason,
                ApplicabilityState = applicability.State,
                QueryPlanEvaluationPassed = evaluation.Passed
            };
        }
        catch (Exception ex)
        {
            return new GoldenCaseRunResult
            {
                CaseId = goldenCase.Id,
                Question = goldenCase.Question,
                Category = category,
                Enabled = true,
                Stage = "QueryPlanExecution",
                Decision = "ERROR",
                Passed = false,
                Reason = $"QueryPlan 回归执行异常：{ex.Message}",
                ApplicabilityState = applicability.State,
                QueryPlanEvaluationPassed = false
            };
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
