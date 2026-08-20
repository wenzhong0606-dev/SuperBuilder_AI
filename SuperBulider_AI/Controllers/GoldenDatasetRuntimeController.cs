using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Models.BI.Evaluation;
using SuperBuilder_AI.Services.BI.Evaluation;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// Phase 2.6 C.13 Golden Dataset 全链路运行诊断接口。
/// 不创建独立测试项目，直接通过 HTTP Controller 驱动真实 Runtime。
/// </summary>
[ApiController]
[Route("evaluation/golden-runtime")]
public sealed class GoldenDatasetRuntimeController : ControllerBase
{
    private readonly GoldenQueryDatasetSerializer _serializer;
    private readonly IWebHostEnvironment _environment;
    private readonly GoldenDatasetRunner _runner;
    private readonly GoldenDatasetRegressionEvaluator _regressionEvaluator;

    public GoldenDatasetRuntimeController(
        GoldenQueryDatasetSerializer serializer,
        IWebHostEnvironment environment,
        GoldenDatasetRunner runner,
        GoldenDatasetRegressionEvaluator regressionEvaluator)
    {
        _serializer = serializer;
        _environment = environment;
        _runner = runner;
        _regressionEvaluator = regressionEvaluator;
    }

    /// <summary>
    /// C.13：执行当前 Golden Dataset 的真实全链路回归。
    /// Golden Dataset → Semantic Applicability → Gate → QueryPlan → Validation/Repair
    /// → Evaluation → Confidence → Regression Scorecard。
    /// </summary>
    [HttpGet("run")]
    public async Task<ActionResult<object>> Run(
        [FromQuery] int topK = 10,
        CancellationToken cancellationToken = default)
    {
        if (topK < 1 || topK > 100)
            return BadRequest(new { passed = false, message = "topK 必须在 1 到 100 之间。" });

        var path = Path.Combine(
            _environment.ContentRootPath,
            "Evaluation",
            "Golden",
            "query-plan-golden-v1.json");

        if (!System.IO.File.Exists(path))
            return NotFound(new
            {
                passed = false,
                message = "Golden Dataset 文件不存在。",
                path
            });

        var json = await System.IO.File.ReadAllTextAsync(path, cancellationToken);
        var run = await _runner.RunAsync(json, topK, cancellationToken);
        var scorecard = _regressionEvaluator.Evaluate(run);

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
            .ToDictionary(
                x => x.Key,
                x => x.Count(),
                StringComparer.OrdinalIgnoreCase);

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

    /// <summary>
    /// C.13：只返回适合人工快速检查的逐 Case 结果。
    /// </summary>
    [HttpGet("cases")]
    public async Task<ActionResult<object>> Cases(
        [FromQuery] int topK = 10,
        CancellationToken cancellationToken = default)
    {
        if (topK < 1 || topK > 100)
            return BadRequest(new { passed = false, message = "topK 必须在 1 到 100 之间。" });

        var path = Path.Combine(
            _environment.ContentRootPath,
            "Evaluation",
            "Golden",
            "query-plan-golden-v1.json");

        if (!System.IO.File.Exists(path))
            return NotFound(new { passed = false, message = "Golden Dataset 文件不存在。", path });

        var json = await System.IO.File.ReadAllTextAsync(path, cancellationToken);
        var run = await _runner.RunAsync(json, topK, cancellationToken);
        var scorecard = _regressionEvaluator.Evaluate(run);

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

    /// <summary>
    /// C.13：检查当前 Golden Dataset 是否可作为 Runtime Regression 基线。
    /// </summary>
    [HttpGet("release-gate")]
    public async Task<ActionResult<object>> ReleaseGate(
        [FromQuery] int topK = 10,
        CancellationToken cancellationToken = default)
    {
        if (topK < 1 || topK > 100)
            return BadRequest(new { passed = false, message = "topK 必须在 1 到 100 之间。" });

        var path = Path.Combine(
            _environment.ContentRootPath,
            "Evaluation",
            "Golden",
            "query-plan-golden-v1.json");

        if (!System.IO.File.Exists(path))
            return NotFound(new { passed = false, message = "Golden Dataset 文件不存在。", path });

        var json = await System.IO.File.ReadAllTextAsync(path, cancellationToken);
        var dataset = _serializer.Deserialize(json);
        var run = await _runner.RunAsync(json, topK, cancellationToken);
        var scorecard = _regressionEvaluator.Evaluate(run);

        var enabledCases = dataset.Cases.Count(x => x.Enabled);
        var expectedCaseCount = dataset.Cases.Count(x => x.Enabled);
        var allCasesExecuted = run.Executed == expectedCaseCount;
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
                expectedCaseCount,
                executedCases = run.Executed,
                allCasesExecuted,
                regressionScorecardPassed = scorecard.Passed
            },
            scorecard
        });
    }
}
