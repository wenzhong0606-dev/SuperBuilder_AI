namespace SuperBulider_AI.Models.BI;

/// <summary>
/// Phase 2.4：Semantic Applicability 到 QueryPlan 的最小绑定契约。
/// 当前第一版只承载 Metric Binding，后续再扩展 Dimension / Filter / Order。
/// </summary>
public sealed class QueryPlanSemanticResolution
{
    public QueryPlanMetricResolution? Metric { get; init; }
}

public sealed class QueryPlanMetricResolution
{
    public string SemanticText { get; init; } = string.Empty;

    public string Table { get; init; } = string.Empty;

    public string Column { get; init; } = string.Empty;

    public string? BusinessMeaning { get; init; }

    public double? Score { get; init; }
}
