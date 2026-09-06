namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// M5-01：语义对象的种类。统一标识的第一步是给每个语义对象一个稳定的"种类 + 键"。
/// 取值与计划验收的 Entity / Metric / Dimension / Filter / Binding 一一对应。
/// </summary>
public enum SemanticKind
{
    Entity = 0,
    Metric = 1,
    Dimension = 2,
    Filter = 3,
    Binding = 4
}

/// <summary>
/// M5-01：规范化语义 ID。所有管线阶段（Understanding / Builder / Validator / Security / Governance）
/// 都应通过本类型引用语义对象，而非散落的字符串或物理列名，从而保证"统一 ID"。
/// 字符串形式为 "&lt;KIND&gt;:&lt;key&gt;"，例如 "ENTITY:inbound_receipt"、"METRIC:revenue"。
/// </summary>
public readonly record struct CanonicalSemanticId
{
    public SemanticKind Kind { get; }
    public string Key { get; }

    public CanonicalSemanticId(SemanticKind kind, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("CanonicalSemanticId 的 key 不能为空。", nameof(key));
        Kind = kind;
        Key = key.Trim();
    }

    /// <summary>稳定字符串形式，适合作为缓存键 / 审计键 / 去重键。</summary>
    public string Canonical => $"{Kind.ToString().ToUpperInvariant()}:{Key}";

    public static CanonicalSemanticId Entity(string key) => new(SemanticKind.Entity, key);
    public static CanonicalSemanticId Metric(string key) => new(SemanticKind.Metric, key);
    public static CanonicalSemanticId Dimension(string key) => new(SemanticKind.Dimension, key);
    public static CanonicalSemanticId Filter(string key) => new(SemanticKind.Filter, key);
    public static CanonicalSemanticId Binding(string key) => new(SemanticKind.Binding, key);

    public static CanonicalSemanticId Parse(string canonical)
    {
        if (string.IsNullOrWhiteSpace(canonical))
            throw new FormatException("CanonicalSemanticId 字符串不能为空。");
        var idx = canonical.IndexOf(':');
        if (idx <= 0 || idx >= canonical.Length - 1)
            throw new FormatException($"非法的 CanonicalSemanticId：{canonical}");
        var kind = Enum.Parse<SemanticKind>(canonical[..idx], ignoreCase: true);
        return new CanonicalSemanticId(kind, canonical[(idx + 1)..]);
    }
}

/// <summary>M5-01：规范化实体定义（业务实体，如"入库凭证"）。</summary>
public sealed record CanonicalEntity
{
    public CanonicalSemanticId Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();
    public long? TableId { get; init; }
    public string? Table { get; init; }
    public string? BusinessDomain { get; init; }
}

/// <summary>M5-01：规范化指标定义。</summary>
public sealed record CanonicalMetric
{
    public CanonicalSemanticId Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();
    public string? PhysicalColumn { get; init; }
    public long? ColumnId { get; init; }
    public string Aggregation { get; init; } = "NONE";
    public string? SemanticType { get; init; }
    /// <summary>GQ-002：EntityCount / ColumnMetric，决定运行时是否强制 COUNT。</summary>
    public string MetricType { get; init; } = "ColumnMetric";
}

/// <summary>M5-01：规范化维度定义。</summary>
public sealed record CanonicalDimension
{
    public CanonicalSemanticId Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();
    public string? PhysicalColumn { get; init; }
    public long? ColumnId { get; init; }
    public long? KeyColumnId { get; init; }
    public long? LabelColumnId { get; init; }
}

/// <summary>M5-01：规范化过滤条件定义。</summary>
public sealed record CanonicalFilter
{
    public CanonicalSemanticId Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? PhysicalColumn { get; init; }
    public long? ColumnId { get; init; }
    public string? Operator { get; init; }
}

/// <summary>M5-01：规范化绑定定义（物理绑定：语义对象 → 表/列）。</summary>
public sealed record CanonicalBinding
{
    public CanonicalSemanticId Id { get; init; }
    public CanonicalSemanticId Target { get; init; }
    public long DataSourceId { get; init; }
    public long TableId { get; init; }
    public string Table { get; init; } = string.Empty;
    public long? ColumnId { get; init; }
    public string? Column { get; init; }
}

/// <summary>M5-01：规范化语义模型全集，由解析器构建，供管线各阶段统一引用。</summary>
public sealed class CanonicalSemanticModel
{
    public IReadOnlyList<CanonicalEntity> Entities { get; init; } = Array.Empty<CanonicalEntity>();
    public IReadOnlyList<CanonicalMetric> Metrics { get; init; } = Array.Empty<CanonicalMetric>();
    public IReadOnlyList<CanonicalDimension> Dimensions { get; init; } = Array.Empty<CanonicalDimension>();
    public IReadOnlyList<CanonicalFilter> Filters { get; init; } = Array.Empty<CanonicalFilter>();
    public IReadOnlyList<CanonicalBinding> Bindings { get; init; } = Array.Empty<CanonicalBinding>();

    private static readonly CanonicalSemanticModel EmptyModel = new();
    public static CanonicalSemanticModel Empty => EmptyModel;

    /// <summary>按名称或别名（跨种类）查找统一 ID；找不到返回 null。</summary>
    public CanonicalSemanticId? FindId(string nameOrAlias)
    {
        var n = (nameOrAlias ?? string.Empty).Trim();
        if (n.Length == 0) return null;
        foreach (var e in Entities) if (Matches(e.Name, e.Aliases, n)) return e.Id;
        foreach (var m in Metrics) if (Matches(m.Name, m.Aliases, n)) return m.Id;
        foreach (var d in Dimensions) if (Matches(d.Name, d.Aliases, n)) return d.Id;
        foreach (var f in Filters)
            if (string.Equals(f.Name, n, StringComparison.OrdinalIgnoreCase)) return f.Id;
        return null;
    }

    private static bool Matches(string name, IReadOnlyList<string> aliases, string n)
        => string.Equals(name, n, StringComparison.OrdinalIgnoreCase)
           || aliases.Any(a => string.Equals(a, n, StringComparison.OrdinalIgnoreCase));
}
