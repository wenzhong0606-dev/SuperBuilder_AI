using SuperBuilder_AI.Models.Metadata;

namespace SuperBuilder_AI.Models.DTO;

/// <summary>
/// 数据库表信息
/// </summary>
public class TableMetadataDto
{

	/// <summary>
	/// 目录名（如 MySQL 的 database / PostgreSQL 的 catalog）。MySQL 下与 SchemaName 同义。
	/// </summary>
	public string? CatalogName { get; set; }

	/// <summary>
	/// 模式名（如 dbo / public）。MySQL 下为空（与 CatalogName 同义）。
	/// </summary>
	public string? SchemaName { get; set; }

	/// <summary>
	/// 表名
	/// </summary>
	public string? TableName { get; set; }

	/// <summary>
	/// 对象类型（表 / 视图），支撑 ScanViews 开关。
	/// </summary>
	public MetadataObjectKind ObjectKind { get; set; } = MetadataObjectKind.Table;

	/// <summary>
	/// 表注释
	/// </summary>
	public string? TableComment { get; set; }

}