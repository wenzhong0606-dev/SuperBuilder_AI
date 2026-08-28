namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// 查询表计划。
/// SemanticText 保存 Golden 业务语义，TableName 保存物理表名。
/// </summary>
public class QueryTable
{
    public long MetadataTableId { get; set; }
    public long DataSourceId { get; set; }
    public string? SemanticText { get; set; }
    public string? TableName { get; set; }
    public string? TableComment { get; set; }
}