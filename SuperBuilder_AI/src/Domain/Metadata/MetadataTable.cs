using SuperBuilder_AI.Models;

namespace SuperBuilder_AI.Models.Metadata;

/// <summary>
/// 表示数据表的元数据信息。
/// </summary>
public class MetadataTable : BaseEntity
{
	/// <summary>
	/// 所属租户的标识。
	/// </summary>
	public long TenantId { get; set; }

	/// <summary>所属租户。</summary>
	public SuperBuilder_AI.Models.Organization.Tenant? Tenant { get; set; }

	/// <summary>
	/// 数据源标识。
	/// </summary>
	public long DataSourceId { get; set; }

	/// <summary>
	/// 关联的数据源实体。
	/// </summary>
	public DataSource? DataSource { get; set; }

	/// <summary>
	/// 表名（必填，建议按数据源原始大小写保留）。
	/// </summary>
	public string TableName { get; set; } = string.Empty;

	/// <summary>
	/// 目录名（如 MySQL 的 database / PostgreSQL 的 catalog）。
	/// 与 SchemaName、TableName 共同构成租户内唯一键。
	/// </summary>
	public string? CatalogName { get; set; }

	/// <summary>
	/// 模式名（如 dbo / public）。
	/// 与 CatalogName、TableName 共同构成租户内唯一键。
	/// </summary>
	public string? SchemaName { get; set; }

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
	/// 向量最近同步时间（UTC）。
	/// </summary>
	public DateTime? VectorSyncTime { get; set; }

	/// <summary>
	/// 向量状态：待同步(Pending) / 已同步(Synced) / 同步失败(Failed) / 过期(Stale)。
	/// </summary>
	public string? VectorStatus { get; set; }

	/// <summary>
	/// 向量同步错误码（同步失败时记录脱敏后的异常类型名）。
	/// </summary>
	public string? VectorErrorCode { get; set; }

	/// <summary>
	/// 表包含的列集合。
	/// </summary>
	public ICollection<MetadataColumn> Columns { get; set; }

		= new List<MetadataColumn>();

}
