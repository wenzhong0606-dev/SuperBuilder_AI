using SuperBulider_AI.Models;

namespace SuperBulider_AI.Models.Metadata;

/// <summary>
/// 表示数据表的元数据信息。
/// </summary>
public class MetadataTable : BaseEntity
{
	/// <summary>
	/// 所属租户的标识。
	/// </summary>
	public long TenantId { get; set; }

	/// <summary>
	/// 数据源标识。
	/// </summary>
	public long DataSourceId { get; set; }

	/// <summary>
	/// 关联的数据源实体。
	/// </summary>
	public DataSource? DataSource { get; set; }

	/// <summary>
	/// 表名。
	/// </summary>
	public string? TableName { get; set; }

	/// <summary>
	/// 表的注释或描述。
	/// </summary>
	public string? TableComment { get; set; }

	/// <summary>
	/// 业务域，用于分类或分组。
	/// </summary>
	public string? BusinessDomain { get; set; }

	/// <summary>
	/// 用于Embedding的文本内容（搜索文本）。
	/// </summary>
	public string? SearchText { get; set; }

	/// <summary>
	/// 向量存储（如 Qdrant）的 ID。
	/// </summary>
	public string? VectorId { get; set; }

	/// <summary>
	/// Embedding模型
	/// </summary>
	public string? EmbeddingModel { get; set; }


	/// <summary>
	/// 向量维度
	/// </summary>
	public int? VectorDimension { get; set; }

	/// <summary>
	/// 表包含的列集合。
	/// </summary>
	public ICollection<MetadataColumn> Columns { get; set; }

		= new List<MetadataColumn>();

}
