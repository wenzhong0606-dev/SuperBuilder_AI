using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.10.4 evaluates filter semantics without binding the Golden contract to a database column.
/// </summary>
public sealed class QueryPlanFilterScoringService
{
    public QueryPlanFilterEvaluationResult Evaluate(
        IReadOnlyList<GoldenFilterExpectation>? expected,
        IReadOnlyList<QueryFilter>? actual)
    {
        if (expected is null)
            return new() { Applicable = false, Passed = true, Score = 1d, Reason = "Filters are not asserted by this case." };

        if (expected.Count == 0)
        {
            var passed = actual is null || actual.Count == 0;
            return new() { Applicable = true, Passed = passed, Score = passed ? 1d : 0d, Reason = passed ? "No filters expected and none were produced." : "No filters were expected, but runtime produced filters." };
        }

        if (actual is null || actual.Count == 0)
            return new() { Applicable = true, Passed = false, Score = 0d, Reason = "Expected filters are missing at runtime." };

        var items = expected.Select(x => EvaluateFilter(x, actual)).ToList();
        var score = items.Count == 0 ? 1d : items.Average(x => x.Score);
        return new()
        {
            Applicable = true,
            Passed = items.All(x => x.Passed),
            Score = score,
            Items = items,
            Reason = items.All(x => x.Passed) ? "All expected filters matched." : "One or more filter assertions failed."
        };
    }

    private static QueryPlanFilterItemScore EvaluateFilter(GoldenFilterExpectation expected, IReadOnlyList<QueryFilter> actual)
    {
        var match = actual.FirstOrDefault(x => Equal(expected.SemanticText, x.Field));
        if (match is null)
            return new()
            {
                SemanticText = expected.SemanticText,
                SemanticScore = 0d,
                OperatorScore = 0d,
                ValueScore = 0d,
                BindingScore = 0d,
                Score = 0d,
                Passed = false,
                Reason = "No runtime filter matched the expected semantic field."
            };

        var semantic = Equal(expected.SemanticText, match.Field) ? 1d : 0d;
        var op = Equal(expected.Operator, match.Operator) ? 1d : 0d;
        var value = expected.Value is null ? 1d : Equal(NormalizeValue(expected.Value), NormalizeValue(match.Value)) ? 1d : 0d;
        var binding = !string.IsNullOrWhiteSpace(match.Field) ? 1d : 0d;
        var score = semantic * .35d + op * .30d + value * .25d + binding * .10d;
        var passed = semantic == 1d && op == 1d && value == 1d && binding == 1d;

        var failures = new List<string>();
        if (semantic < 1d) failures.Add("semantic");
        if (op < 1d) failures.Add($"operator(expected={expected.Operator}, actual={match.Operator})");
        if (value < 1d) failures.Add($"value(expected={expected.Value}, actual={match.Value})");
        if (binding < 1d) failures.Add("binding");

        return new()
        {
            SemanticText = expected.SemanticText,
            RuntimeField = match.Field,
            RuntimeOperator = match.Operator,
            RuntimeValue = match.Value,
            SemanticScore = semantic,
            OperatorScore = op,
            ValueScore = value,
            BindingScore = binding,
            Score = score,
            Passed = passed,
            Reason = failures.Count == 0 ? "Filter matched." : $"Filter mismatch: {string.Join(", ", failures)}."
        };
    }

    private static string NormalizeValue(string value) => value.Trim().Trim('\'', '"');

    private static bool Equal(string? left, string? right)
        => !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right)
           && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}

public sealed class QueryPlanFilterEvaluationResult
{
    public bool Applicable { get; init; }
    public bool Passed { get; init; }
    public double Score { get; init; }
    public string Reason { get; init; } = string.Empty;
    public IReadOnlyList<QueryPlanFilterItemScore> Items { get; init; } = Array.Empty<QueryPlanFilterItemScore>();
}

public sealed class QueryPlanFilterItemScore
{
    public string SemanticText { get; init; } = string.Empty;
    public string RuntimeField { get; init; } = string.Empty;
    public string RuntimeOperator { get; init; } = string.Empty;
    public string RuntimeValue { get; init; } = string.Empty;
    public double SemanticScore { get; init; }
    public double OperatorScore { get; init; }
    public double ValueScore { get; init; }
    public double BindingScore { get; init; }
    public double Score { get; init; }
    public bool Passed { get; init; }
    public string Reason { get; init; } = string.Empty;
}
