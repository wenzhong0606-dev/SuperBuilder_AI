namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// 查询过滤条件。
/// SemanticText 是 Golden/业务语义，Field 是最终物理字段。
/// </summary>
public class QueryFilter
{
    public string SemanticText { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = "=";
    public string Value { get; set; } = string.Empty;
}