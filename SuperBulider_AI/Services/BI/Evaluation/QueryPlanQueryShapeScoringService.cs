using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.10.6 evaluates execution-shape semantics independently from SQL text.
/// </summary>
public sealed class QueryPlanQueryShapeScoringService
{
    public QueryPlanQueryShapeEvaluationResult Evaluate(GoldenQueryExpectation? expected, QueryPlan? actual)
    {
        if (expected is null)
            return new() { Applicable = false, Passed = true, Score = 1d, Reason = "Query shape is not asserted by this case." };

        if (actual is null)
            return new() { Applicable = true, Passed = false, Score = 0d, Reason = "Runtime QueryPlan is missing." };

        var checks = new List<QueryPlanQueryShapeCheck>();
        Add(checks, "IsAggregate", expected.IsAggregate, actual.IsAggregate);
        Add(checks, "Distinct", expected.Distinct, actual.Distinct);
        Add(checks, "Limit", expected.Limit, actual.Limit);
        Add(checks, "IsRanking", expected.IsRanking, actual.IsRanking);
        Add(checks, "IsDetailRanking", expected.IsDetailRanking, actual.IsDetailRanking);
        Add(checks, "IsAggregateRanking", expected.IsAggregateRanking, actual.IsAggregateRanking);

        var orderScore = EvaluateOrders(expected.Orders, actual.Orders, checks);
        var applicable = checks.Count > 0 || expected.Orders is not null;
        if (!applicable)
            return new() { Applicable = false, Passed = true, Score = 1d, Checks = checks, Reason = "No query-shape assertions are defined." };

        var score = checks.Count == 0 ? 1d : checks.Average(x => x.Score);
        if (expected.Orders is not null)
            score = checks.Count == 0 ? orderScore : (score + orderScore) / 2d;

        return new()
        {
            Applicable = true,
            Passed = checks.All(x => x.Passed),
            Score = score,
            Checks = checks,
            Reason = checks.All(x => x.Passed) ? "All query-shape assertions matched." : "One or more query-shape assertions failed."
        };
    }

    private static void Add(List<QueryPlanQueryShapeCheck> checks, string name, bool? expected, bool actual)
    {
        if (expected is null) return;
        checks.Add(new() { Name = name, Expected = expected.Value.ToString(), Actual = actual.ToString(), Score = expected.Value == actual ? 1d : 0d, Passed = expected.Value == actual });
    }

    private static void Add(List<QueryPlanQueryShapeCheck> checks, string name, int? expected, int? actual)
    {
        if (expected is null) return;
        checks.Add(new() { Name = name, Expected = expected.Value.ToString(), Actual = actual?.ToString() ?? "null", Score = expected == actual ? 1d : 0d, Passed = expected == actual });
    }

    private static double EvaluateOrders(IReadOnlyList<GoldenOrderExpectation>? expected, IReadOnlyList<QueryOrder> actual, List<QueryPlanQueryShapeCheck> checks)
    {
        if (expected is null) return 1d;
        if (expected.Count == 0)
        {
            var pass = actual.Count == 0;
            checks.Add(new() { Name = "Orders", Expected = "empty", Actual = actual.Count.ToString(), Score = pass ? 1d : 0d, Passed = pass });
            return pass ? 1d : 0d;
        }

        if (actual.Count == 0)
        {
            checks.Add(new() { Name = "Orders", Expected = expected.Count.ToString(), Actual = "0", Score = 0d, Passed = false });
            return 0d;
        }

        var items = expected.Select(e =>
        {
            var match = actual.FirstOrDefault(a => a.IsMetric == e.IsMetric &&
                (e.IsMetric ? Equal(e.MetricSemanticText, a.MetricName) : Equal(e.SemanticText, a.Field)));
            if (match is null)
                return 0d;
            var direction = Equal(e.Direction, match.Direction) ? 1d : 0d;
            var aggregation = !e.IsMetric || e.Aggregation == QueryAggregation.None || e.Aggregation == match.Aggregation ? 1d : 0d;
            return direction * .5d + aggregation * .5d;
        }).ToList();

        var score = items.Average();
        checks.Add(new() { Name = "Orders", Expected = expected.Count.ToString(), Actual = actual.Count.ToString(), Score = score, Passed = items.All(x => x >= 1d) });
        return score;
    }

    private static bool Equal(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}

public sealed class QueryPlanQueryShapeEvaluationResult
{
    public bool Applicable { get; init; }
    public bool Passed { get; init; }
    public double Score { get; init; }
    public string Reason { get; init; } = string.Empty;
    public IReadOnlyList<QueryPlanQueryShapeCheck> Checks { get; init; } = Array.Empty<QueryPlanQueryShapeCheck>();
}

public sealed class QueryPlanQueryShapeCheck
{
    public string Name { get; init; } = string.Empty;
    public string Expected { get; init; } = string.Empty;
    public string Actual { get; init; } = string.Empty;
    public double Score { get; init; }
    public bool Passed { get; init; }
}
