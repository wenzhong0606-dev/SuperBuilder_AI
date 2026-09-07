using SuperBuilder_AI.Models.AppBuilder;

namespace SuperBuilder_AI.Models.Components;

public static class CustomComponentDslVersions
{
    public const string V1 = "1.0";
    public const string Current = V1;
    public static IReadOnlyList<string> Supported { get; } = new[] { V1 };
}

/// <summary>
/// 可复用组件 DSL。Root 只能使用 AppComponentTypes 白名单中的结构化 ComponentPlan，
/// 不允许 HTML/CSS/脚本；运行时只消费经过校验的强类型对象。
/// </summary>
public sealed class CustomComponentDsl
{
    public string Version { get; set; } = CustomComponentDslVersions.Current;
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ComponentPlan Root { get; set; } = new();
}

/// <summary>租户级自定义组件定义；草稿与发布态物理隔离。</summary>
public sealed class CustomComponentDefinition : BaseEntity
{
    public long TenantId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ComponentType { get; set; } = AppComponentTypes.Chart;
    public string DslVersion { get; set; } = CustomComponentDslVersions.Current;
    public string DslJson { get; set; } = string.Empty;
    public string? PublishedDslJson { get; set; }
    public int PublishedVersion { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? PublishedBy { get; set; }
    public ICollection<CustomComponentVersion> Versions { get; set; } = new List<CustomComponentVersion>();
}

/// <summary>不可变的组件发布版本；回滚生成新版本，不改写历史。</summary>
public sealed class CustomComponentVersion : BaseEntity
{
    public long ComponentId { get; set; }
    public long TenantId { get; set; }
    public int Version { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ComponentType { get; set; } = AppComponentTypes.Chart;
    public string DslVersion { get; set; } = CustomComponentDslVersions.Current;
    public string DslJson { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    public string? PublishedBy { get; set; }
    public int? RolledBackFromVersion { get; set; }
    public CustomComponentDefinition? Component { get; set; }
}
