using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.10.5 evaluates Join endpoint semantics, physical binding and join type.
/// Golden semantics are matched against the runtime names because QueryJoin currently carries
/// resolved table/column names and IDs, while Universal Golden Ground Truth remains database-neutral.
/// </summary>
public sealed class QueryPlanJoinScoringService
{
    public QueryPlanJoinEvaluationResult Evaluate(
        IReadOnlyList<GoldenJoinExpectation>? expected,
        IReadOnlyList<QueryJoin>? actual)
    {
        if (expected is null)
            return new() { Applicable = false, Passed = true, Score = 1d, Reason = "Joins are not asserted by this case." };

        if (expected.Count == 0)
        {
            var passed = actual is null || actual.Count == 0;
            return new() { Applicable = true, Passed = passed, Score = passed ? 1d : 0d, Reason = passed ? "No joins expected and none were produced." : "No joins were expected, but runtime produced joins." };
        }

        if (actual is null || actual.Count == 0)
            return new() { Applicable = true, Passed = false, Score = 0d, Reason = "Expected joins are missing at runtime." };

        var items = expected.Select(x => EvaluateJoin(x, actual)).ToList();
        var score = items.Count == 0 ? 1d : items.Average(x => x.Score);
        return new()
        {
            Applicable = true,
            Passed = items.All(x => x.Passed),
            Score = score,
            Items = items,
            Reason = items.All(x => x.Passed) ? "All expected joins matched." : "One or more join assertions failed."
        };
    }

    private static QueryPlanJoinItemScore EvaluateJoin(GoldenJoinExpectation expected, IReadOnlyList<QueryJoin> actual)
    {
        var match = actual.FirstOrDefault(x =>
            EndpointMatch(expected.LeftTableSemanticText, x.LeftTableName) &&
            EndpointMatch(expected.RightTableSemanticText, x.RightTableName) &&
            EndpointMatch(expected.LeftColumnSemanticText, x.LeftColumnName) &&
            EndpointMatch(expected.RightColumnSemanticText, x.RightColumnName));

        // Allow reversed endpoints for INNER joins, where the logical relation is equivalent.
        match ??= actual.FirstOrDefault(x =>
            Equal(expected.JoinType, "INNER") &&
            EndpointMatch(expected.LeftTableSemanticText, x.RightTableName) &&
            EndpointMatch(expected.RightTableSemanticText, x.LeftTableName) &&
            EndpointMatch(expected.LeftColumnSemanticText, x.RightColumnName) &&
            EndpointMatch(expected.RightColumnSemanticText, x.LeftColumnName));

        if (match is null)
            return new()
            {
                LeftTableSemanticText = expected.LeftTableSemanticText,
                RightTableSemanticText = expected.RightTableSemanticText,
                EndpointScore = 0d,
                ColumnScore = 0d,
                JoinTypeScore = 0d,
                BindingScore = 0d,
                Score = 0d,
                Passed = false,
                Reason = "No runtime join matched the expected endpoints and columns."
            };

        var endpoint = EndpointMatch(expected.LeftTableSemanticText, match.LeftTableName) && EndpointMatch(expected.RightTableSemanticText, match.RightTableName) ? 1d : .5d;
        var columns = EndpointMatch(expected.LeftColumnSemanticText, match.LeftColumnName) && EndpointMatch(expected.RightColumnSemanticText, match.RightColumnName) ? 1d : .5d;
        var type = Equal(expected.JoinType, match.JoinType) ? 1d : 0d;
        var binding = match.LeftTableId > 0 && match.LeftColumnId > 0 && match.RightTableId > 0 && match.RightColumnId > 0 ? 1d : 0d;
        var score = endpoint * .30d + columns * .30d + type * .20d + binding * .20d;
        var passed = endpoint == 1d && columns == 1d && type == 1d && binding == 1d;

        var failures = new List<string>();
        if (endpoint < 1d) failures.Add("endpoint");
        if (columns < 1d) failures.Add("columns");
        if (type < 1d) failures.Add($"joinType(expected={expected.JoinType}, actual={match.JoinType})");
        if (binding < 1d) failures.Add("binding");

        return new()
        {
            LeftTableSemanticText = expected.LeftTableSemanticText,
            RightTableSemanticText = expected.RightTableSemanticText,
            RuntimeLeftTable = match.LeftTableName,
            RuntimeLeftColumn = match.LeftColumnName,
            RuntimeRightTable = match.RightTableName,
            RuntimeRightColumn = match.RightColumnName,
            RuntimeJoinType = match.JoinType,
            EndpointScore = endpoint,
            ColumnScore = columns,
            JoinTypeScore = type,
            BindingScore = binding,
            Score = score,
            Passed = passed,
            Reason = failures.Count == 0 ? "Join matched." : $"Join mismatch: {string.Join(", ", failures)}."
        };
    }

    private static bool EndpointMatch(string expected, string? actual)
        => Equal(expected, actual);

    private static bool Equal(string? left, string? right)
        => !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
           string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}

public sealed class QueryPlanJoinEvaluationResult
{
    public bool Applicable { get; init; }
    public bool Passed { get; init; }
    public double Score { get; init; }
    public string Reason { get; init; } = string.Empty;
    public IReadOnlyList<QueryPlanJoinItemScore> Items { get; init; } = Array.Empty<QueryPlanJoinItemScore>();
}

public sealed class QueryPlanJoinItemScore
{
    public string LeftTableSemanticText { get; init; } = string.Empty;
    public string RightTableSemanticText { get; init; } = string.Empty;
    public string? RuntimeLeftTable { get; init; }
    public string? RuntimeLeftColumn { get; init; }
    public string? RuntimeRightTable { get; init; }
    public string? RuntimeRightColumn { get; init; }
    public string? RuntimeJoinType { get; init; }
    public double EndpointScore { get; init; }
    public double ColumnScore { get; init; }
    public double JoinTypeScore { get; init; }
    public double BindingScore { get; init; }
    public double Score { get; init; }
    public bool Passed { get; init; }
    public string Reason { get; init; } = string.Empty;
}
