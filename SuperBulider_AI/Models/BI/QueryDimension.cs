namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// 查询维度。
/// SemanticText 是业务语义，ColumnName 是物理字段，二者职责分离。
/// </summary>
public class QueryDimension
{
    public long MetadataColumnId { get; set; }
    public string SemanticText { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public string? Alias { get; set; }
    public string? SemanticType { get; set; }
}