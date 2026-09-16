namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// 查询过滤条件。
/// SemanticText 是 Golden/业务语义，Field 是最终物理字段。
/// </summary>
public class QueryFilter
{
    public string SemanticText { get; set; } = string.Empty;
    public long MetadataTableId { get; set; }
    public string? TableName { get; set; }
    public long MetadataColumnId { get; set; }
    public string Field { get; set; } = string.Empty;
    /// <summary>已解析物理字段类型，供参数化 SQL 做类型转换；旧请求可为空。</summary>
    public string? DataType { get; set; }
    public string Operator { get; set; } = "=";
    public string Value { get; set; } = string.Empty;
}
