using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.10.3 evaluates dimensions against the current database-neutral Golden contract.
/// The current GoldenDimensionExpectation only asserts SemanticText, so no ungrounded field/role
/// assertion is introduced here.
/// </summary>
public sealed class QueryPlanDimensionScoringService
{
    public QueryPlanDimensionEvaluationResult Evaluate(
        IReadOnlyList<GoldenDimensionExpectation>? expected,
        IReadOnlyList<QueryDimension>? actual)
    {
        if (expected is null)
            return new() { Applicable = false, Passed = true, Score = 1d, Reason = "Dimensions are not asserted by this case." };

        if (expected.Count == 0)
        {
            var passed = actual is null || actual.Count == 0;
            return new()
            {
                Applicable = true,
                Passed = passed,
                Score = passed ? 1d : 0d,
                Reason = passed ? "No dimensions expected and none were produced." : "No dimensions were expected, but runtime produced dimensions."
            };
        }

        if (actual is null || actual.Count == 0)
            return new() { Applicable = true, Passed = false, Score = 0d, Reason = "Expected dimensions are missing at runtime." };

        var items = expected.Select(x => EvaluateDimension(x, actual)).ToList();
        var score = items.Count == 0 ? 1d : items.Average(x => x.Score);

        return new()
        {
            Applicable = true,
            Passed = items.All(x => x.Passed),
            Score = score,
            Items = items,
            Reason = items.All(x => x.Passed) ? "All expected dimensions matched." : "One or more dimension assertions failed."
        };
    }

    private static QueryPlanDimensionItemScore EvaluateDimension(
        GoldenDimensionExpectation expected,
        IReadOnlyList<QueryDimension> actual)
    {
        var match = actual.FirstOrDefault(x => Equal(expected.SemanticText, x.Alias))
            ?? actual.FirstOrDefault(x => Equal(expected.SemanticText, x.SemanticType))
            ?? actual.FirstOrDefault(x => Equal(expected.SemanticText, x.ColumnName));

        if (match is null)
        {
            return new()
            {
                SemanticText = expected.SemanticText,
                SemanticScore = 0d,
                BindingScore = 0d,
                Score = 0d,
                Passed = false,
                Reason = "No runtime dimension matched the expected semantic dimension."
            };
        }

        var semantic = Equal(expected.SemanticText, match.Alias) ||
                       Equal(expected.SemanticText, match.SemanticType) ||
                       Equal(expected.SemanticText, match.ColumnName)
            ? 1d : 0d;

        // A QueryDimension with a resolved MetadataColumnId/ColumnName is physically bound.
        var binding = match.MetadataColumnId > 0 && !string.IsNullOrWhiteSpace(match.ColumnName) ? 1d : 0d;
        var score = semantic * .70d + binding * .30d;
        var passed = semantic == 1d && binding == 1d;

        return new()
        {
            SemanticText = expected.SemanticText,
            RuntimeAlias = match.Alias,
            RuntimeColumn = match.ColumnName,
            RuntimeSemanticType = match.SemanticType,
            SemanticScore = semantic,
            BindingScore = binding,
            Score = score,
            Passed = passed,
            Reason = passed ? "Dimension matched and is physically bound." :
                binding < 1d ? "Dimension semantic match exists, but physical column binding is incomplete." :
                "Dimension semantic match failed."
        };
    }

    private static bool Equal(string? left, string? right)
        => !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
           string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}

public sealed class QueryPlanDimensionEvaluationResult
{
    public bool Applicable { get; init; }
    public bool Passed { get; init; }
    public double Score { get; init; }
    public string Reason { get; init; } = string.Empty;
    public IReadOnlyList<QueryPlanDimensionItemScore> Items { get; init; } = Array.Empty<QueryPlanDimensionItemScore>();
}

public sealed class QueryPlanDimensionItemScore
{
    public string SemanticText { get; init; } = string.Empty;
    public string? RuntimeAlias { get; init; }
    public string RuntimeColumn { get; init; } = string.Empty;
    public string? RuntimeSemanticType { get; init; }
    public double SemanticScore { get; init; }
    public double BindingScore { get; init; }
    public double Score { get; init; }
    public bool Passed { get; init; }
    public string Reason { get; init; } = string.Empty;
}
