namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// M5-02：字段解析的"统一引用"词汇。
/// Understanding 产出的意图项（Metric / Dimension / Filter）与 Builder 消费的权威绑定、
/// 以及 Validator 校验的运行时计划字段，全部通过该类型表达，
/// 从而让管线三阶段对"同一个字段"使用同一套标识（配合 M5-01 的 CanonicalSemanticId）。
///
/// 关键职责分离：<see cref="SemanticText"/> 是业务语义（语言无关、来自 QueryIntent 归一化），
/// <see cref="PhysicalColumn"/> 是物理列引用（metric.Field / dimension.ColumnName / filter.Field），
/// 二者与 QueryPlan 实体的既有约定一致。
/// </summary>
public sealed class SemanticFieldReference
{
    /// <summary>字段种类（Entity/Metric/Dimension/Filter/Binding）。</summary>
    public SemanticKind Kind { get; init; }

    /// <summary>业务语义文本；可为空（纯物理列引用）。</summary>
    public string? SemanticText { get; init; }

    /// <summary>物理列引用（运行时侧）：metric.Field / dimension.ColumnName / filter.Field；可为空。</summary>
    public string? PhysicalColumn { get; init; }

    /// <summary>已解析的物理列 Id（未解析为 0）。</summary>
    public long ColumnId { get; init; }

    private SemanticFieldReference() { }

    public static SemanticFieldReference Metric(string? semanticText, string? field, long columnId = 0)
        => new() { Kind = SemanticKind.Metric, SemanticText = TrimOrNull(semanticText), PhysicalColumn = TrimOrNull(field), ColumnId = columnId };

    public static SemanticFieldReference Dimension(string? semanticText, string? columnName, long columnId = 0)
        => new() { Kind = SemanticKind.Dimension, SemanticText = TrimOrNull(semanticText), PhysicalColumn = TrimOrNull(columnName), ColumnId = columnId };

    public static SemanticFieldReference Filter(string? semanticText, string? field, long columnId = 0)
        => new() { Kind = SemanticKind.Filter, SemanticText = TrimOrNull(semanticText), PhysicalColumn = TrimOrNull(field), ColumnId = columnId };

    /// <summary>从 Understanding 产出的意图项构建（运行时引用，ColumnId 通常未解析）。</summary>
    public static SemanticFieldReference From(QueryMetric m) => Metric(m?.SemanticText, m?.Field);
    public static SemanticFieldReference From(QueryDimension d) => Dimension(d?.SemanticText, d?.ColumnName, d?.MetadataColumnId ?? 0);
    public static SemanticFieldReference From(QueryFilter f) => Filter(f?.SemanticText, f?.Field);

    /// <summary>从 Builder 产出的计划项构建（Validator 视角，可携带已解析的 ColumnId）。</summary>
    public static SemanticFieldReference FromPlan(QueryMetric m) => Metric(m?.SemanticText, m?.Field);
    public static SemanticFieldReference FromPlan(QueryDimension d) => Dimension(d?.SemanticText, d?.ColumnName, d?.MetadataColumnId ?? 0);
    public static SemanticFieldReference FromPlan(QueryFilter f) => Filter(f?.SemanticText, f?.Field);

    /// <summary>是否有可识别的引用（语义或物理列任一非空）。位置回退仅在 hasToken 时成立。</summary>
    public bool HasToken => !string.IsNullOrWhiteSpace(SemanticText) || !string.IsNullOrWhiteSpace(PhysicalColumn);

    private static string? TrimOrNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
