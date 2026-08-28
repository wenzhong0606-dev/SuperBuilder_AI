using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.10.8 turns Golden Dataset coverage into an actionable regression report.
/// It deliberately reuses the existing Coverage Analyzer and does not infer coverage from runtime results.
/// </summary>
public sealed class GoldenDatasetCoverageReportService
{
    private readonly GoldenDatasetCoverageAnalyzer _analyzer;

    public GoldenDatasetCoverageReportService(GoldenDatasetCoverageAnalyzer analyzer)
    {
        _analyzer = analyzer;
    }

    public GoldenDatasetCoverageReport Generate(GoldenQueryDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        var scorecard = _analyzer.Analyze(dataset);
        var cases = _analyzer.AnalyzeCases(dataset);
        var priorities = BuildPriorities(scorecard);

        return new GoldenDatasetCoverageReport
        {
            Dataset = scorecard.Dataset,
            Version = scorecard.Version,
            TotalCases = scorecard.TotalCases,
            EnabledCases = scorecard.EnabledCases,
            Scorecard = scorecard,
            CaseCoverage = cases,
            Priorities = priorities
        };
    }

    private static IReadOnlyList<GoldenCoverageGapPriority> BuildPriorities(GoldenDatasetCoverageScorecard scorecard)
    {
        var priorities = new List<GoldenCoverageGapPriority>();

        foreach (var dimension in scorecard.DimensionCounts.Keys)
        {
            var count = scorecard.DimensionCounts[dimension];
            var coverage = scorecard.DimensionCoverage[dimension];

            if (count == 0)
            {
                priorities.Add(new()
                {
                    Dimension = dimension,
                    Priority = "P0",
                    Count = count,
                    Coverage = coverage,
                    Reason = "No enabled Golden Case covers this QueryPlan dimension."
                });
            }
            else if (count == 1)
            {
                priorities.Add(new()
                {
                    Dimension = dimension,
                    Priority = "P1",
                    Count = count,
                    Coverage = coverage,
                    Reason = "Only one enabled Golden Case covers this dimension; regression coverage is fragile."
                });
            }
            else if (coverage < 0.20d)
            {
                priorities.Add(new()
                {
                    Dimension = dimension,
                    Priority = "P2",
                    Count = count,
                    Coverage = coverage,
                    Reason = "Dimension is covered, but scenario density remains low."
                });
            }
        }

        return priorities
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Coverage)
            .ThenBy(x => x.Dimension)
            .ToList();
    }
}

public sealed class GoldenDatasetCoverageReport
{
    public string Dataset { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public int TotalCases { get; init; }
    public int EnabledCases { get; init; }
    public GoldenDatasetCoverageScorecard Scorecard { get; init; } = new();
    public IReadOnlyList<GoldenDatasetCoverageCase> CaseCoverage { get; init; } = Array.Empty<GoldenDatasetCoverageCase>();
    public IReadOnlyList<GoldenCoverageGapPriority> Priorities { get; init; } = Array.Empty<GoldenCoverageGapPriority>();
}

public sealed class GoldenCoverageGapPriority
{
    public string Dimension { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public int Count { get; init; }
    public double Coverage { get; init; }
    public string Reason { get; init; } = string.Empty;
}
