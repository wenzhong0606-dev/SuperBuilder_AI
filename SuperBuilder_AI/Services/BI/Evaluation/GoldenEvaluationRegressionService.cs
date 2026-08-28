using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.10.10 combines the executable Golden Dataset run with the existing regression policy.
/// This is the code-level regression gate: it evaluates actual current QueryPlan outcomes rather than
/// only comparing Dataset metadata.
/// </summary>
public sealed class GoldenEvaluationRegressionService
{
    private readonly GoldenDatasetRunner _runner;
    private readonly GoldenDatasetRegressionEvaluator _evaluator;

    public GoldenEvaluationRegressionService(
        GoldenDatasetRunner runner,
        GoldenDatasetRegressionEvaluator evaluator)
    {
        _runner = runner;
        _evaluator = evaluator;
    }

    public async Task<GoldenEvaluationRegressionResult> EvaluateAsync(
        string goldenDatasetJson,
        GoldenDatasetRegressionPolicy? policy = null,
        int topK = 10,
        CancellationToken cancellationToken = default)
    {
        var run = await _runner.RunAsync(goldenDatasetJson, topK, cancellationToken);
        var scorecard = _evaluator.Evaluate(run, policy);

        return new GoldenEvaluationRegressionResult
        {
            Dataset = run.Dataset,
            Version = run.Version,
            Passed = scorecard.Passed,
            Decision = scorecard.Decision,
            Run = run,
            Scorecard = scorecard,
            BlockingGates = scorecard.FailedGates
        };
    }
}

public sealed class GoldenEvaluationRegressionResult
{
    public string Dataset { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public bool Passed { get; init; }
    public string Decision { get; init; } = string.Empty;
    public GoldenDatasetRunResult Run { get; init; } = new();
    public GoldenDatasetRegressionScorecard Scorecard { get; init; } = new();
    public IReadOnlyList<string> BlockingGates { get; init; } = Array.Empty<string>();
}
