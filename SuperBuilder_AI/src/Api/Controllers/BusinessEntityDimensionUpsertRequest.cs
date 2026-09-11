namespace SuperBuilder_AI.Controllers;

/// <summary>
/// M12 增量：维度编辑请求体（承载维度标量字段，按所属业务域全量合并）。
/// </summary>
public sealed class BusinessEntityDimensionUpsertRequest
{
    /// <summary>维度名（业务键，必填）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>描述（可选）。</summary>
    public string? Description { get; set; }

    /// <summary>维度表达式（可分组字段或时间粒度，如 date_trunc('month', created_at)，可选）。</summary>
    public string? Expression { get; set; }

    /// <summary>维度数据类型 / 时间粒度（string/date/month，可选）。</summary>
    public string? DataType { get; set; }
}
