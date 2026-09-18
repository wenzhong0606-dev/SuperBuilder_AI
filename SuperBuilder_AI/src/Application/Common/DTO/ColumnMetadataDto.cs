namespace SuperBuilder_AI.Models.DTO;

/// <summary>
/// 字段信息
/// </summary>
public class ColumnMetadataDto
{

	public string? CatalogName { get; set; }

	public string? SchemaName { get; set; }

	public string? TableName { get; set; }


	public string? ColumnName { get; set; }


	public string? ColumnComment { get; set; }


	public string? DataType { get; set; }


	public long? Length { get; set; }


	public bool IsNullable { get; set; }


	public bool IsPrimaryKey { get; set; }

}