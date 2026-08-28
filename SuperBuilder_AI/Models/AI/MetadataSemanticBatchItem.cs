namespace SuperBuilder_AI.Models.AI;

/// <summary>
/// Metadata字段语义批量生成结果。
///
/// 用于：
///
/// MetadataColumn
///        ↓
/// Batch发送Qwen
///        ↓
/// Qwen返回JSON
///        ↓
/// MetadataSemantic
///
/// 通过 Id 精确关联 MetadataColumn。
///
/// 注意：
/// 不使用 BusinessKey 进行关联。
/// 因为不同数据库可能存在：
///
/// DataSource A:
/// customer.customer_name
///
/// DataSource B:
/// customer.customer_name
///
/// 但是 MetadataColumn.Id 永远唯一。
/// </summary>
public class MetadataSemanticBatchItem
{


	/// <summary>
	/// MetadataColumn主键Id。
	///
	/// 对应数据库：
	/// MetadataColumns.Id
	///
	/// Qwen必须原样返回。
	/// </summary>
	public long Id { get; set; }




	/// <summary>
	/// AI生成的字段业务含义。
	///
	/// 示例：
	/// "客户名称，用于标识销售订单所属客户"
	/// </summary>
	public string? BusinessMeaning { get; set; }




	/// <summary>
	/// 业务关键词。
	///
	/// 示例：
	/// 客户,客户名称,购买方
	/// </summary>
	public string? Keywords { get; set; }




	/// <summary>
	/// 业务同义词。
	///
	/// 示例：
	/// 客户名,客户名称,单位名称
	/// </summary>
	public string? Synonyms { get; set; }




	/// <summary>
	/// 用户可能提出的问题。
	///
	/// 用于后续：
	/// Semantic Search
	/// Few Shot Learning
	/// Text To SQL
	/// </summary>
	public string? ExampleQuestions { get; set; }




	/// <summary>
	/// 所属业务领域。
	///
	/// 示例：
	/// 销售管理
	/// 库存管理
	/// 生产管理
	/// </summary>
	public string? BusinessDomain { get; set; }




	/// <summary>
	/// AI生成置信度。
	///
	/// 范围：
	/// 0-1
	/// </summary>
	public decimal Confidence { get; set; }



}