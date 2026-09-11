namespace SuperBuilder_AI.Controllers;

/// <summary>
/// M12 增量：指标编辑请求体（承载指标标量字段，按所属业务实体全量合并）。
/// 物理绑定不在本请求内，由合并逻辑按指标 Name 保留匹配项的既有绑定。
/// </summary>
public sealed class BusinessEntityMetricUpsertRequest
{
    /// <summary>指标名（业务键，必填）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>展示名（可选）。</summary>
    public string? DisplayName { get; set; }

    /// <summary>描述（可选）。</summary>
    public string? Description { get; set; }

    /// <summary>语义类型（可选）。</summary>
    public string? SemanticType { get; set; }

    /// <summary>聚合方式（sum/count/avg…，可选）。</summary>
    public string? Aggregation { get; set; }

    /// <summary>是否为计算（派生）指标。</summary>
    public bool IsCalculated { get; set; }

    /// <summary>计算口径表达式（仅 IsCalculated 为 true 时有意义）。</summary>
    public string? Expression { get; set; }

    /// <summary>结果数据类型（decimal/int/string/date，可选）。</summary>
    public string? DataType { get; set; }
}
