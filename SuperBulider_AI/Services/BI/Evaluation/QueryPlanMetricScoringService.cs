using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.13.2：Metric Structural Evaluation。
/// SemanticText 与物理 Field 严格分离；Semantic Match 不再要求 Golden SemanticText 等于物理字段名。
/// </summary>
public sealed class QueryPlanMetricScoringService
{
    public QueryPlanMetricEvaluationResult Evaluate(IReadOnlyList<GoldenMetricExpectation>? expected, IReadOnlyList<QueryMetric>? actual)
    {
        if (expected is null) return new() { Applicable = false, Passed = true, Score = 1d, Reason = "Metrics are not asserted by this case." };
        if (expected.Count == 0)
        {
            var passed = actual is null || actual.Count == 0;
            return new() { Applicable = true, Passed = passed, Score = passed ? 1d : 0d, Reason = passed ? "No metrics expected and none were produced." : "No metrics were expected, but runtime produced metrics." };
        }
        if (actual is null || actual.Count == 0) return new() { Applicable = true, Passed = false, Score = 0d, Reason = "Expected metrics are missing at runtime." };

        var items = expected.Select(x => EvaluateMetric(x, actual)).ToList();
        var score = items.Count == 0 ? 1d : items.Average(x => x.Score);
        return new()
        {
            Applicable = true,
            Passed = items.All(x => x.Passed),
            Score = score,
            Items = items,
            Reason = items.All(x => x.Passed) ? "All expected metrics matched." : "One or more metric assertions failed."
        };
    }

    private static QueryPlanMetricItemScore EvaluateMetric(GoldenMetricExpectation expected, IReadOnlyList<QueryMetric> actual)
    {
        var match = FindBestMatch(expected, actual);
        if (match is null)
            return new() { SemanticText = expected.SemanticText, SemanticScore = 0d, FieldScore = expected.Field is null ? null : 0d, AggregationScore = 0d, BindingScore = 0d, Score = 0d, Passed = false, Reason = "No runtime metric matched the expected semantic metric." };

        var runtimeSemantic = !string.IsNullOrWhiteSpace(match.SemanticText) ? match.SemanticText : match.Name;
        var semantic = TextEqual(expected.SemanticText, runtimeSemantic) ? 1d : SemanticCompatible(expected.SemanticText, match) ? .5d : 0d;
        var field = expected.Field is null ? (double?)null : TextEqual(expected.Field, match.Field) ? 1d : 0d;
        var aggregation = expected.Aggregation == QueryAggregation.None || expected.Aggregation == match.GetAggregation() ? 1d : 0d;
        var binding = !string.IsNullOrWhiteSpace(match.Field) && match.GetAggregation() != QueryAggregation.None || expected.Aggregation == QueryAggregation.None ? 1d : 0d;
        var score = semantic * .35d + (field ?? 1d) * .20d + aggregation * .35d + binding * .10d;
        var passed = semantic >= 1d && (field is null || field >= 1d) && aggregation >= 1d && binding >= 1d;

        return new()
        {
            SemanticText = expected.SemanticText,
            RuntimeName = match.Name,
            RuntimeField = match.Field,
            RuntimeAggregation = match.GetAggregation().ToString(),
            SemanticScore = semantic,
            FieldScore = field,
            AggregationScore = aggregation,
            BindingScore = binding,
            Score = score,
            Passed = passed,
            Reason = BuildReason(expected, match, semantic, field, aggregation, binding)
        };
    }

    private static QueryMetric? FindBestMatch(GoldenMetricExpectation expected, IReadOnlyList<QueryMetric> actual)
        => actual.FirstOrDefault(x => TextEqual(expected.SemanticText, x.SemanticText))
           ?? actual.FirstOrDefault(x => TextEqual(expected.SemanticText, x.Name))
           ?? (expected.Field is null ? null : actual.FirstOrDefault(x => TextEqual(expected.Field, x.Field)));

    private static bool SemanticCompatible(string expected, QueryMetric actual)
        => TextEqual(expected, actual.SemanticType) || TextEqual(expected, actual.Alias) || TextEqual(expected, actual.Name);

    private static bool TextEqual(string? left, string? right)
        => !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string BuildReason(GoldenMetricExpectation expected, QueryMetric actual, double semantic, double? field, double aggregation, double binding)
    {
        var failures = new List<string>();
        if (semantic < 1d) failures.Add("semantic");
        if (field is not null && field < 1d) failures.Add("field");
        if (aggregation < 1d) failures.Add($"aggregation(expected={expected.Aggregation}, actual={actual.GetAggregation()})");
        if (binding < 1d) failures.Add("binding");
        return failures.Count == 0 ? "Metric matched." : $"Metric mismatch: {string.Join(", ", failures)}.";
    }
}

public sealed class QueryPlanMetricEvaluationResult
{
    public bool Applicable { get; init; }
    public bool Passed { get; init; }
    public double Score { get; init; }
    public string Reason { get; init; } = string.Empty;
    public IReadOnlyList<QueryPlanMetricItemScore> Items { get; init; } = Array.Empty<QueryPlanMetricItemScore>();
}

public sealed class QueryPlanMetricItemScore
{
    public string SemanticText { get; init; } = string.Empty;
    public string RuntimeName { get; init; } = string.Empty;
    public string RuntimeField { get; init; } = string.Empty;
    public string RuntimeAggregation { get; init; } = string.Empty;
    public double SemanticScore { get; init; }
    public double? FieldScore { get; init; }
    public double AggregationScore { get; init; }
    public double BindingScore { get; init; }
    public double Score { get; init; }
    public bool Passed { get; init; }
    public string Reason { get; init; } = string.Empty;
}
