using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.13 Golden Dataset Runtime 统一编排服务。
/// Controller 只负责 HTTP；Golden Dataset 的加载、Runtime 执行与 Regression Scorecard
/// 在这里统一，避免多个 Controller 各自复制同一套 Pipeline。
/// </summary>
public sealed class GoldenDatasetRuntimeService
{
    private readonly GoldenDatasetRunner _runner;
    private readonly GoldenDatasetRegressionEvaluator _regressionEvaluator;
    private readonly GoldenQueryDatasetSerializer _serializer;
    private readonly IWebHostEnvironment _environment;

    public GoldenDatasetRuntimeService(
        GoldenDatasetRunner runner,
        GoldenDatasetRegressionEvaluator regressionEvaluator,
        GoldenQueryDatasetSerializer serializer,
        IWebHostEnvironment environment)
    {
        _runner = runner;
        _regressionEvaluator = regressionEvaluator;
        _serializer = serializer;
        _environment = environment;
    }

    public string GetGoldenPath() => Path.Combine(
        _environment.ContentRootPath,
        "Evaluation",
        "Golden",
        "query-plan-golden-v1.json");

    public async Task<GoldenDatasetRuntimeResult> RunAsync(
        int topK,
        CancellationToken cancellationToken = default)
    {
        ValidateTopK(topK);

        var path = GetGoldenPath();
        if (!File.Exists(path))
            throw new FileNotFoundException("Golden Dataset 文件不存在。", path);

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var run = await _runner.RunAsync(json, topK, cancellationToken);
        var scorecard = _regressionEvaluator.Evaluate(run);

        return new GoldenDatasetRuntimeResult(run, scorecard);
    }

    public GoldenQueryDataset LoadDataset()
    {
        var path = GetGoldenPath();
        if (!File.Exists(path))
            throw new FileNotFoundException("Golden Dataset 文件不存在。", path);

        return _serializer.Deserialize(File.ReadAllText(path));
    }

    public static void ValidateTopK(int topK)
    {
        if (topK < 1 || topK > 100)
            throw new ArgumentOutOfRangeException(nameof(topK), "topK 必须在 1 到 100 之间。");
    }
}

public sealed record GoldenDatasetRuntimeResult(
    GoldenDatasetRunResult Run,
    GoldenDatasetRegressionScorecard Scorecard);
