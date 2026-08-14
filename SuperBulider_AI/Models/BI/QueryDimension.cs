namespace SuperBulider_AI.Models.BI;

/// <summary>
/// 查询维度。
///
/// 例如：
///
/// 按物料
/// 按仓库
/// 按供应商
/// 按月份
/// </summary>
public class QueryDimension
{
	/// <summary>
	/// MetadataColumn Id。
	/// </summary>
	public long MetadataColumnId { get; set; }

	/// <summary>
	/// 实际数据库字段名。
	/// </summary>
	public string ColumnName { get; set; } = string.Empty;

	/// <summary>
	/// 展示别名。
	/// </summary>
	public string? Alias { get; set; }

	/// <summary>
	/// 语义类型。
	///
	/// Dimension
	/// Time
	/// Category
	/// Entity
	/// </summary>
	public string? SemanticType { get; set; }
}