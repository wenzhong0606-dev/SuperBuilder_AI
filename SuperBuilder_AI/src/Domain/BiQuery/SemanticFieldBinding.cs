namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// M5-02：权威绑定侧的统一表示（来自 Semantic Resolution：指标 / 维度 / 过滤的确认绑定）。
/// 与 <see cref="SemanticFieldReference"/> 配对，供 <see cref="SemanticFieldBindingMatcher"/> 统一匹配。
/// 语义解析阶段产出的绑定是"权威"的，下游（Builder）只消费、不再二次猜测。
/// </summary>
public sealed class SemanticFieldBinding
{
    public SemanticKind Kind { get; init; }

    /// <summary>绑定的业务语义文本（来自 Semantic Resolution）。</summary>
    public string? SemanticText { get; init; }

    /// <summary>绑定的权威物理列（resolution.Column）。</summary>
    public string? PhysicalColumn { get; init; }

    /// <summary>绑定的权威物理列 Id。</summary>
    public long ColumnId { get; init; }

    private SemanticFieldBinding() { }

    public static SemanticFieldBinding From(QueryPlanMetricResolution b)
        => new() { Kind = SemanticKind.Metric, SemanticText = TrimOrNull(b?.SemanticText), PhysicalColumn = TrimOrNull(b?.Column), ColumnId = b?.ColumnId ?? 0 };

    public static SemanticFieldBinding From(QueryPlanFilterResolution b)
        => new() { Kind = SemanticKind.Filter, SemanticText = TrimOrNull(b?.SemanticText), PhysicalColumn = TrimOrNull(b?.Column), ColumnId = b?.ColumnId ?? 0 };

    public static SemanticFieldBinding From(QueryPlanDimensionResolution b)
        => new() { Kind = SemanticKind.Dimension, SemanticText = TrimOrNull(b?.SemanticText), PhysicalColumn = TrimOrNull(b?.Column), ColumnId = b?.ColumnId ?? 0 };

    private static string? TrimOrNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
