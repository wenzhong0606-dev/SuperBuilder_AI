namespace SuperBuilder_AI.Models.DTO;

/// <summary>
/// 外键关系信息：本表列指向另一表列。
/// 仅描述同数据源内的物理外键；跨数据源引用由声明/学习规则承载。
/// </summary>
public class ForeignKeyMetadataDto
{
	public string? TableName { get; set; }

	public string? ColumnName { get; set; }

	public string? ReferencedTableName { get; set; }

	public string? ReferencedColumnName { get; set; }
}
