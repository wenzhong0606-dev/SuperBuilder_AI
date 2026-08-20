using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Services.BI.Evaluation;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// Phase 2.6 C.13 Golden Dataset 全链路运行诊断接口。
/// Controller 只负责 HTTP 参数、状态码与响应格式；实际 Golden Runtime 由统一服务编排。
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

    [HttpGet("run")]
    public async Task<ActionResult<object>> Run(
        [FromQuery] int topK = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _runtimeService.RunAsync(topK, cancellationToken);
            var run = result.Run;
            var scorecard = result.Scorecard;

            var categorySummary = run.Cases
                .Where(x => x.Enabled)
                .GroupBy(x => x.Category)
                .ToDictionary(
                    x => x.Key,
                    x => new
                    {
                        total = x.Count(),
                        passed = x.Count(item => item.Passed),
                        failed = x.Count(item => !item.Passed)
                    },
                    StringComparer.OrdinalIgnoreCase);

            var stageSummary = run.Cases
                .Where(x => x.Enabled)
                .GroupBy(x => x.Stage)
                .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);

            return Ok(new
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
                    run.Passed,
                    run.Failed,
                    run.Blocked,
                    run.Review,
                    run.Unresolved,
                    run.Ambiguous
                },
                categorySummary,
                stageSummary,
                scorecard,
                cases = run.Cases,
                confidenceCalibration = run.ConfidenceCalibration
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
                    x.ApplicabilityState,
                    x.QueryPlanEvaluationPassed,
                    x.ConfidenceDecision,
                    x.ConfidenceLevel,
                    x.ConfidenceScore,
                    x.Reason
                }),
                failedCases = run.Cases
                    .Where(x => x.Enabled && !x.Passed)
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
}
