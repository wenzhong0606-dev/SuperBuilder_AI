using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.7.4
/// 根据 Golden Coverage 缺口生成待补测试场景建议。
/// 只生成 Scenario Gap，不生成或伪造 Golden Ground Truth。
/// </summary>
public sealed class GoldenScenarioGapGenerator
{
    private static readonly IReadOnlyDictionary<string, (string Scenario, string Priority, string Reason, string[] Tags)> Templates =
        new Dictionary<string, (string, string, string, string[])>(StringComparer.OrdinalIgnoreCase)
        {
            ["Intent"] = ("Intent classification", "High", "No enabled Golden Case explicitly exercises intent classification.", new[] { "intent", "coverage-gap" }),
            ["Metric"] = ("Metric aggregation", "Critical", "No enabled Golden Case explicitly exercises metric semantics.", new[] { "metric", "aggregation", "coverage-gap" }),
            ["Dimension"] = ("Dimension grouping", "High", "No enabled Golden Case explicitly exercises dimension semantics.", new[] { "dimension", "grouping", "coverage-gap" }),
            ["Filter"] = ("Filtered query", "High", "No enabled Golden Case explicitly exercises filter semantics.", new[] { "filter", "coverage-gap" }),
            ["Table"] = ("Table resolution", "Critical", "No enabled Golden Case explicitly exercises table binding.", new[] { "table", "resolution", "coverage-gap" }),
            ["Join"] = ("Join query", "Critical", "No enabled Golden Case explicitly exercises join planning.", new[] { "join", "coverage-gap" }),
            ["Order"] = ("Ordered query", "Medium", "No enabled Golden Case explicitly exercises ordering.", new[] { "order", "coverage-gap" }),
            ["Aggregate"] = ("Aggregate query", "High", "No enabled Golden Case explicitly exercises aggregate intent.", new[] { "aggregate", "coverage-gap" }),
            ["Distinct"] = ("Distinct query", "Medium", "No enabled Golden Case explicitly exercises distinct semantics.", new[] { "distinct", "coverage-gap" }),
            ["Limit"] = ("Limited result", "Medium", "No enabled Golden Case explicitly exercises result limits.", new[] { "limit", "coverage-gap" }),
            ["Ranking"] = ("Ranking query", "High", "No enabled Golden Case explicitly exercises ranking.", new[] { "ranking", "coverage-gap" }),
            ["DetailRanking"] = ("Detail ranking query", "High", "No enabled Golden Case explicitly exercises detail ranking.", new[] { "detail-ranking", "ranking", "coverage-gap" }),
            ["AggregateRanking"] = ("Aggregate ranking query", "Critical", "No enabled Golden Case explicitly exercises aggregate ranking.", new[] { "aggregate-ranking", "ranking", "coverage-gap" })
        };

    public GoldenScenarioGapScorecard Generate(GoldenDatasetCoverageScorecard coverage)
    {
        ArgumentNullException.ThrowIfNull(coverage);

        var gaps = coverage.MissingDimensions
            .Where(Templates.ContainsKey)
            .Select(dimension =>
            {
                var template = Templates[dimension];
                return new GoldenScenarioGap
                {
                    Dimension = dimension,
                    ScenarioType = template.Scenario,
                    Priority = template.Priority,
                    Reason = template.Reason,
                    SuggestedTags = template.Tags.ToList()
                };
            })
            .ToList();

        return new GoldenScenarioGapScorecard
        {
            Dataset = coverage.Dataset,
            Version = coverage.Version,
            GapCount = gaps.Count,
            Gaps = gaps
        };
    }
}
