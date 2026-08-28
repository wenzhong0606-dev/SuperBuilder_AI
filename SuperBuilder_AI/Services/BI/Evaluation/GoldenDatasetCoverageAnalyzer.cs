using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.7
/// 分析 Golden Dataset 对 QueryPlan 能力维度的覆盖情况。
/// Coverage 基于 Golden Ground Truth，而不是运行时结果，避免结果反向污染测试定义。
/// </summary>
public sealed class GoldenDatasetCoverageAnalyzer
{
    private static readonly string[] RequiredDimensions =
    {
        "Intent",
        "Metric",
        "Dimension",
        "Filter",
        "Table",
        "Join",
        "Order",
        "Aggregate",
        "Distinct",
        "Limit",
        "Ranking",
        "DetailRanking",
        "AggregateRanking"
    };

    public GoldenDatasetCoverageScorecard Analyze(GoldenQueryDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        var enabledCases = dataset.Cases.Where(x => x.Enabled).ToList();
        var counts = RequiredDimensions.ToDictionary(
            x => x,
            _ => 0,
            StringComparer.OrdinalIgnoreCase);

        foreach (var goldenCase in enabledCases)
        {
            foreach (var dimension in DetectDimensions(goldenCase.Expected))
            {
                counts[dimension]++;
            }
        }

        var coverage = counts.ToDictionary(
            x => x.Key,
            x => enabledCases.Count == 0 ? 0d : (double)x.Value / enabledCases.Count,
            StringComparer.OrdinalIgnoreCase);

        var missing = counts
            .Where(x => x.Value == 0)
            .Select(x => x.Key)
            .ToList();

        var warnings = new List<string>();

        foreach (var item in counts.Where(x => x.Value > 0 && x.Value < 2))
        {
            warnings.Add($"{item.Key} has only {item.Value} enabled Golden Case; coverage is fragile.");
        }

        if (enabledCases.Count == 0)
            warnings.Add("Golden Dataset contains no enabled cases.");

        return new GoldenDatasetCoverageScorecard
        {
            Dataset = dataset.Dataset,
            Version = dataset.Version,
            TotalCases = dataset.Cases.Count,
            EnabledCases = enabledCases.Count,
            DimensionCounts = counts,
            DimensionCoverage = coverage,
            MissingDimensions = missing,
            Warnings = warnings
        };
    }

    public IReadOnlyList<GoldenDatasetCoverageCase> AnalyzeCases(GoldenQueryDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        return dataset.Cases
            .Select(x => new GoldenDatasetCoverageCase
            {
                CaseId = x.Id,
                Enabled = x.Enabled,
                Dimensions = DetectDimensions(x.Expected).ToList()
            })
            .ToList();
    }

    private static IEnumerable<string> DetectDimensions(GoldenQueryExpectation expectation)
    {
        if (expectation.IntentType is not null)
            yield return "Intent";

        if (expectation.Metrics is not null)
            yield return "Metric";

        if (expectation.Dimensions is not null)
            yield return "Dimension";

        if (expectation.Filters is not null)
            yield return "Filter";

        if (expectation.Tables is not null)
            yield return "Table";

        if (expectation.Joins is not null)
            yield return "Join";

        if (expectation.Orders is not null)
            yield return "Order";

        if (expectation.IsAggregate is not null)
            yield return "Aggregate";

        if (expectation.Distinct is not null)
            yield return "Distinct";

        if (expectation.Limit is not null)
            yield return "Limit";

        if (expectation.IsRanking is not null)
            yield return "Ranking";

        if (expectation.IsDetailRanking is not null)
            yield return "DetailRanking";

        if (expectation.IsAggregateRanking is not null)
            yield return "AggregateRanking";
    }
}
