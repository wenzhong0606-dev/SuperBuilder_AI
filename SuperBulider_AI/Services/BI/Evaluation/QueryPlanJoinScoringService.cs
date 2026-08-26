using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.10.5 / C.14：评价 Runtime Join 的物理 Contract。
/// Golden Join 仅作为期望结构；Evaluator 不重新执行 Semantic Resolution。
/// </summary>
public sealed class QueryPlanJoinScoringService
{
    public QueryPlanJoinEvaluationResult Evaluate(IReadOnlyList<GoldenJoinExpectation>? expected, IReadOnlyList<QueryJoin>? actual)
    {
        if (expected is null) return new() { Applicable = false, Passed = true, Score = 1d, Reason = "Joins are not asserted by this case." };
        if (expected.Count == 0)
        {
            var passed = actual is null || actual.Count == 0;
            return new() { Applicable = true, Passed = passed, Score = passed ? 1d : 0d, Reason = passed ? "No joins expected and none were produced." : "No joins were expected, but runtime produced joins." };
        }
        if (actual is null || actual.Count == 0) return new() { Applicable = true, Passed = false, Score = 0d, Reason = "Expected joins are missing at runtime." };

        var items = expected.Select(x => EvaluateJoin(x, actual)).ToList();
        var score = items.Count == 0 ? 1d : items.Average(x => x.Score);
        var passedAll = items.All(x => x.Passed);
        return new() { Applicable = true, Passed = passedAll, Score = score, Items = items, Reason = passedAll ? "All expected joins matched the Runtime Join Contract." : "One or more join assertions failed." };
    }

    private static QueryPlanJoinItemScore EvaluateJoin(GoldenJoinExpectation expected, IReadOnlyList<QueryJoin> actual)
    {
        // Golden Join 已经代表“应该发生 Join”的 Contract；这里只比较 Runtime 的实际物理 Endpoint。
        // 不再根据 Golden SemanticText 去 Metadata 做第二次推理。
        var match = actual.FirstOrDefault(x =>
            Equal(expected.JoinType, x.JoinType)
            && EqualSemanticEndpoint(expected.LeftTableSemanticText, x.LeftTableSemanticText, x.LeftTableName)
            && EqualSemanticEndpoint(expected.RightTableSemanticText, x.RightTableSemanticText, x.RightTableName)
            && EqualSemanticEndpoint(expected.LeftColumnSemanticText, x.LeftColumnSemanticText, x.LeftColumnName)
            && EqualSemanticEndpoint(expected.RightColumnSemanticText, x.RightColumnSemanticText, x.RightColumnName));

        // INNER JOIN 允许左右端点交换。
        match ??= actual.FirstOrDefault(x =>
            Equal(expected.JoinType, "INNER")
            && Equal(expected.LeftTableSemanticText, x.RightTableSemanticText)
            && Equal(expected.RightTableSemanticText, x.LeftTableSemanticText)
            && Equal(expected.LeftColumnSemanticText, x.RightColumnSemanticText)
            && Equal(expected.RightColumnSemanticText, x.LeftColumnSemanticText));

        if (match is null)
        {
            return new QueryPlanJoinItemScore
            {
                LeftTableSemanticText = expected.LeftTableSemanticText,
                RightTableSemanticText = expected.RightTableSemanticText,
                EndpointScore = 0d,
                ColumnScore = 0d,
                JoinTypeScore = 0d,
                BindingScore = 0d,
                Score = 0d,
                Passed = false,
                Reason = "Runtime 没有提供与 Golden Join Contract 对应的物理 Join。"
            };
        }

        var type = Equal(expected.JoinType, match.JoinType) ? 1d : 0d;
        var endpoint = 1d;
        var columns = 1d;
        var binding = match.LeftTableId > 0 && match.LeftColumnId > 0 && match.RightTableId > 0 && match.RightColumnId > 0 ? 1d : 0d;
        var score = endpoint * .30d + columns * .30d + type * .20d + binding * .20d;
        var passed = type == 1d && binding == 1d;

        return new QueryPlanJoinItemScore
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
            Reason = passed ? "Runtime Join Contract 与 Golden 一致。" : "Runtime Join 物理绑定或 JoinType 无效。"
        };
    }

    private static bool EqualSemanticEndpoint(string? expected, string? runtimeSemantic, string? runtimePhysical)
        => Equal(expected, runtimeSemantic) || Equal(expected, runtimePhysical);

    private static bool Equal(string? left, string? right)
        => !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right)
           && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
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
