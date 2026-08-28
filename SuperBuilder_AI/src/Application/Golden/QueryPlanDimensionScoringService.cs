using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.13.2：Dimension Evaluation。
/// Golden SemanticText 只与 Runtime SemanticText/语义别名比较；物理绑定同时验证冻结的 Dimension Resolution Contract。
/// Evaluator 不重新进行 Semantic Resolution。
/// </summary>
public sealed class QueryPlanDimensionScoringService
{
    public QueryPlanDimensionEvaluationResult Evaluate(IReadOnlyList<GoldenDimensionExpectation>? expected, IReadOnlyList<QueryDimension>? actual)
    {
        if (expected is null) return new() { Applicable = false, Passed = true, Score = 1d, Reason = "Dimensions are not asserted by this case." };
        if (expected.Count == 0)
        {
            var passed = actual is null || actual.Count == 0;
            return new() { Applicable = true, Passed = passed, Score = passed ? 1d : 0d, Reason = passed ? "No dimensions expected and none were produced." : "No dimensions were expected, but runtime produced dimensions." };
        }
        if (actual is null || actual.Count == 0) return new() { Applicable = true, Passed = false, Score = 0d, Reason = "Expected dimensions are missing at runtime." };
        var items = expected.Select(x => EvaluateDimension(x, actual)).ToList();
        var score = items.Count == 0 ? 1d : items.Average(x => x.Score);
        return new() { Applicable = true, Passed = items.All(x => x.Passed), Score = score, Items = items, Reason = items.All(x => x.Passed) ? "All expected dimensions matched." : "One or more dimension assertions failed." };
    }

    private static QueryPlanDimensionItemScore EvaluateDimension(GoldenDimensionExpectation expected, IReadOnlyList<QueryDimension> actual)
    {
        var match = actual.FirstOrDefault(x => Equal(expected.SemanticText, x.SemanticText))
            ?? actual.FirstOrDefault(x => Equal(expected.SemanticText, x.Alias))
            ?? actual.FirstOrDefault(x => Equal(expected.SemanticText, x.SemanticType));
        if (match is null) return new() { SemanticText = expected.SemanticText, SemanticScore = 0d, BindingScore = 0d, Score = 0d, Passed = false, Reason = "No runtime dimension matched the expected semantic dimension." };

        var semantic = Equal(expected.SemanticText, match.SemanticText) || Equal(expected.SemanticText, match.Alias) || Equal(expected.SemanticText, match.SemanticType) ? 1d : 0d;
        var physicalBinding = match.MetadataColumnId > 0 && !string.IsNullOrWhiteSpace(match.ColumnName);
        var resolutionBinding = string.Equals(match.ResolutionState, "Resolved", StringComparison.OrdinalIgnoreCase)
            && string.Equals(match.ExecutionCapability, "Executable", StringComparison.OrdinalIgnoreCase)
            && (string.Equals(match.ResolutionType, "MasterJoin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(match.ResolutionType, "DirectKey", StringComparison.OrdinalIgnoreCase));

        // DirectKey 的唯一物理真相是事实表 Association ID；不能用 Label/Display Field 冒充 Key。
        var directKeyBinding = !string.Equals(match.ResolutionType, "DirectKey", StringComparison.OrdinalIgnoreCase)
            || (match.DimensionKeyColumnId.HasValue
                && match.DimensionKeyColumnId.Value == match.MetadataColumnId
                && !string.IsNullOrWhiteSpace(match.DimensionKeyColumnName)
                && string.Equals(match.DimensionKeyColumnName, match.ColumnName, StringComparison.OrdinalIgnoreCase));

        var masterJoinBinding = !string.Equals(match.ResolutionType, "MasterJoin", StringComparison.OrdinalIgnoreCase)
            || (match.DimensionKeyColumnId.HasValue && match.DimensionKeyColumnId.Value == match.MetadataColumnId);

        var binding = physicalBinding && resolutionBinding && directKeyBinding && masterJoinBinding;
        var score = semantic * .70d + (binding ? 1d : 0d) * .30d;
        var passed = semantic == 1d && binding;
        var reason = passed
            ? "Dimension SemanticText 与冻结 Resolution Contract / 物理绑定一致。"
            : !physicalBinding
                ? "Dimension semantic match exists, but physical column binding is incomplete."
                : !resolutionBinding
                    ? $"Dimension Resolution Contract 无效：ResolutionType={match.ResolutionType}，ResolutionState={match.ResolutionState}，ExecutionCapability={match.ExecutionCapability}。"
                    : !directKeyBinding
                        ? "DirectKey 的事实表 Association ID 与 Runtime Dimension MetadataColumnId 不一致。"
                        : !masterJoinBinding
                            ? "MasterJoin 缺少事实侧稳定 Dimension Key Binding。"
                            : "Dimension semantic match failed.";

        return new() { SemanticText = expected.SemanticText, RuntimeAlias = match.Alias, RuntimeColumn = match.ColumnName, RuntimeSemanticType = match.SemanticType, SemanticScore = semantic, BindingScore = binding ? 1d : 0d, Score = score, Passed = passed, Reason = reason };
    }

    private static bool Equal(string? left, string? right) => !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
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
