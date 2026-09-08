using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Models.BI.Evaluation;
using SuperBuilder_AI.Services.BI.Evaluation;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// Phase 2.6 C.13 Golden Dataset Runtime HTTP 入口。
/// Controller 只负责参数校验、结果筛选、HTTP 状态码与响应格式；
/// Golden Runtime Pipeline 统一由 GoldenDatasetRuntimeService 编排。
/// </summary>
[ApiController]
[Route("evaluation/golden-runtime")]
public sealed class GoldenDatasetRuntimeController : ControllerBase
{
    private readonly GoldenDatasetRuntimeService _runtimeService;

    public GoldenDatasetRuntimeController(GoldenDatasetRuntimeService runtimeService)
    {
        _runtimeService = runtimeService;
    }

    /// <summary>
    /// 运行 Golden Dataset。
    /// 不传 caseId：执行完整 Dataset Regression。
    /// 传 caseId：仍使用统一 Runtime Pipeline 执行 Dataset，但只返回指定 Case 的诊断结果。
    /// </summary>
    [HttpGet("run")]
    public async Task<ActionResult<object>> Run(
        [FromQuery] string? caseId = null,
        [FromQuery] int topK = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            GoldenDatasetRuntimeService.ValidateTopK(topK);

            GoldenQueryCase? selectedCase = null;
            if (!string.IsNullOrWhiteSpace(caseId))
            {
                var dataset = _runtimeService.LoadDataset();
                selectedCase = dataset.Cases.FirstOrDefault(x =>
                    x.Enabled && string.Equals(x.Id, caseId, StringComparison.OrdinalIgnoreCase));

                if (selectedCase is null)
                {
                    return NotFound(new
                    {
                        passed = false,
                        decision = "NOT_FOUND",
                        message = $"Golden Case '{caseId}' was not found or is disabled."
                    });
                }
            }

            var result = await _runtimeService.RunAsync(topK, cancellationToken);
            var run = result.Run;
            var scorecard = result.Scorecard;

            if (selectedCase is not null)
            {
                var goldenCase = run.Cases.FirstOrDefault(x =>
                    string.Equals(x.CaseId, selectedCase.Id, StringComparison.OrdinalIgnoreCase));

                if (goldenCase is null)
                {
                    return NotFound(new
                    {
                        passed = false,
                        decision = "NOT_FOUND",
                        message = $"Golden Case '{selectedCase.Id}' did not produce a runtime result."
                    });
                }

                var expectedOutcomeSatisfied = EvaluateExpectedOutcome(goldenCase);

                return Ok(new
                {
                    // caseId 诊断接口的 passed 表示 Golden Expected Outcome 是否满足。
                    // GoldenCaseRunResult.Passed 保留 Runtime 原始执行结果；Negative Case
                    // 被正确 FAIL/BLOCK 时，原始 Passed=false，但 ExpectedOutcomeSatisfied=true。
                    passed = expectedOutcomeSatisfied,
                    decision = expectedOutcomeSatisfied ? "PASS" : goldenCase.Decision,
                    stage = goldenCase.Stage,
                    phase = "Phase 2.6 C.13",
                    dataset = run.Dataset,
                    version = run.Version,
                    caseId = goldenCase.CaseId,
                    question = goldenCase.Question,
                    category = goldenCase.Category,
                    applicabilityState = goldenCase.ApplicabilityState,
                    queryPlanEvaluationPassed = goldenCase.QueryPlanEvaluationPassed,
                    confidenceDecision = goldenCase.ConfidenceDecision,
                    confidenceLevel = goldenCase.ConfidenceLevel,
                    confidenceScore = goldenCase.ConfidenceScore,
                    rawRuntimePassed = goldenCase.Passed,
                    expectedOutcomeSatisfied,
                    reason = expectedOutcomeSatisfied
                        ? BuildExpectedOutcomeReason(goldenCase)
                        : goldenCase.Reason,
                    caseResult = goldenCase,
                    validationDiagnostics = goldenCase.ValidationDiagnostics,
                    evaluationDiagnostics = goldenCase.EvaluationDiagnostics,
                    fullRun = new
                    {
                        total = run.Total,
                        executed = run.Executed,
                        passed = run.Passed,
                        failed = run.Failed,
                        blocked = run.Blocked,
                        review = run.Review,
                        unresolved = run.Unresolved,
                        ambiguous = run.Ambiguous,
                        regressionPassed = scorecard.Passed,
                        overallPassRate = scorecard.OverallPassRate,
                        positivePassRate = scorecard.PositivePassRate,
                        negativeDetectionRate = scorecard.NegativeDetectionRate
                    }
                });
            }

            return Ok(BuildFullRunResponse(run, scorecard));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { passed = false, message = ex.Message });
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { passed = false, message = ex.Message, path = ex.FileName });
        }
    }

    [HttpGet("cases")]
    public async Task<ActionResult<object>> Cases(
        [FromQuery] int topK = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _runtimeService.RunAsync(topK, cancellationToken);
            var run = result.Run;
            var scorecard = result.Scorecard;

            return Ok(new
            {
                passed = scorecard.Passed,
                decision = scorecard.Decision,
                dataset = run.Dataset,
                version = run.Version,
                cases = run.Cases.Select(x => new
                {
                    x.CaseId,
                    x.Category,
                    x.Enabled,
                    x.Stage,
                    x.Decision,
                    x.Passed,
                    expectedOutcomeSatisfied = EvaluateExpectedOutcome(x),
                    x.ApplicabilityState,
                    x.QueryPlanEvaluationPassed,
                    x.ConfidenceDecision,
                    x.ConfidenceLevel,
                    x.ConfidenceScore,
                    x.Reason
                }),
                failedCases = run.Cases
                    .Where(x => x.Enabled && !EvaluateExpectedOutcome(x))
                    .Select(x => new
                    {
                        x.CaseId,
                        x.Category,
                        x.Stage,
                        x.Decision,
                        x.ApplicabilityState,
                        x.Reason
                    }),
                scorecard
            });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { passed = false, message = ex.Message });
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { passed = false, message = ex.Message, path = ex.FileName });
        }
    }

    [HttpGet("release-gate")]
    public async Task<ActionResult<object>> ReleaseGate(
        [FromQuery] int topK = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _runtimeService.RunAsync(topK, cancellationToken);
            var dataset = _runtimeService.LoadDataset();
            var run = result.Run;
            var scorecard = result.Scorecard;

            var enabledCases = dataset.Cases.Count(x => x.Enabled);
            var allCasesExecuted = run.Executed == enabledCases;
            var release = scorecard.Passed && allCasesExecuted && enabledCases > 0;

            return Ok(new
            {
                passed = release,
                decision = release ? "RELEASE" : "BLOCK",
                phase = "Phase 2.6 C.13",
                checks = new
                {
                    goldenDatasetLoaded = true,
                    enabledCases,
                    expectedCaseCount = enabledCases,
                    executedCases = run.Executed,
                    allCasesExecuted,
                    regressionScorecardPassed = scorecard.Passed
                },
                scorecard
            });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { passed = false, message = ex.Message });
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { passed = false, message = ex.Message, path = ex.FileName });
        }
    }

    private static bool EvaluateExpectedOutcome(GoldenCaseRunResult item)
    {
        return item.Category.ToLowerInvariant() switch
        {
            "positive" => item.Passed,
            "negative" => !item.Passed && item.Decision is "FAIL" or "ERROR" or "BLOCK" or "REVIEW",
            "ambiguous" => item.Passed && string.Equals(item.ApplicabilityState, "Ambiguous", StringComparison.OrdinalIgnoreCase),
            "unresolved" => item.Passed && string.Equals(item.ApplicabilityState, "NotResolved", StringComparison.OrdinalIgnoreCase),
            _ => item.Passed
        };
    }

    private static string BuildExpectedOutcomeReason(GoldenCaseRunResult item)
    {
        return item.Category.ToLowerInvariant() switch
        {
            "negative" => $"Negative Case 正确检测到预期失败/拒绝：Runtime Decision={item.Decision}，Golden Expected Outcome 满足。",
            "ambiguous" => "Golden Case 预期 Applicability=Ambiguous，实际匹配。",
            "unresolved" => "Golden Case 预期 Applicability=NotResolved，实际匹配。",
            _ => "Golden Case Expected Outcome 满足。"
        };
    }

    private static object BuildFullRunResponse(
        GoldenDatasetRunResult run,
        GoldenDatasetRegressionScorecard scorecard)
    {
        var categorySummary = run.Cases
            .Where(x => x.Enabled)
            .GroupBy(x => x.Category)
            .ToDictionary(
                x => x.Key,
                x => new
                {
                    total = x.Count(),
                    passed = x.Count(item => EvaluateExpectedOutcome(item)),
                    failed = x.Count(item => !EvaluateExpectedOutcome(item))
                },
                StringComparer.OrdinalIgnoreCase);

        var stageSummary = run.Cases
            .Where(x => x.Enabled)
            .GroupBy(x => x.Stage)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);

        return new
        {
            passed = scorecard.Passed,
            decision = scorecard.Decision,
            phase = "Phase 2.6 C.13",
            pipeline = new[]
            {
                "Golden Dataset",
                "Semantic Applicability",
                "Applicability Gate",
                "Query Understanding",
                "QueryPlan Build",
                "Validation/Repair",
                "QueryPlan Evaluation",
                "Evaluation-aware Confidence",
                "Regression Scorecard"
            },
            dataset = run.Dataset,
            version = run.Version,
            summary = new
            {
                run.Total,
                run.Executed,
                rawRuntimePassed = run.Passed,
                rawRuntimeFailed = run.Failed,
                expectedOutcomePassed = run.Cases.Count(x => x.Enabled && EvaluateExpectedOutcome(x)),
                expectedOutcomeFailed = run.Cases.Count(x => x.Enabled && !EvaluateExpectedOutcome(x)),
                run.Blocked,
                run.Review,
                run.Unresolved,
                run.Ambiguous
            },
            categorySummary,
            stageSummary,
            scorecard,
            cases = run.Cases.Select(x => new
            {
                x.CaseId,
                x.Question,
                x.Category,
                x.Enabled,
                x.Stage,
                x.Decision,
                rawRuntimePassed = x.Passed,
                expectedOutcomeSatisfied = EvaluateExpectedOutcome(x),
                x.Reason,
                x.ApplicabilityState,
                x.QueryPlanEvaluationPassed,
                x.ConfidenceDecision,
                x.ConfidenceLevel,
                x.ConfidenceScore,
                x.ValidationDiagnostics,
                x.EvaluationDiagnostics
            }),
            confidenceCalibration = run.ConfidenceCalibration
        };
    }
}
