using SuperBulider_AI.Models;

namespace SuperBulider_AI.Models.Metadata;

/// <summary>
/// 表示元数据字段的业务语义信息。
///
/// 用于:
///
/// 1. AI理解数据库字段业务含义
/// 2. 用户自然语言检索
/// 3. Text-To-SQL Prompt增强
/// 4. Semantic Embedding向量搜索
///
/// </summary>
public class MetadataSemantic
	: BaseEntity
{


	/// <summary>
	/// 关联的元数据字段Id。
	///
	/// 一个字段对应一个Semantic。
	/// </summary>
	public long? MetadataColumnId { get; set; }



	/// <summary>
	/// 关联字段。
	/// </summary>
	public MetadataColumn? MetadataColumn { get; set; }



	/// <summary>
	/// AI生成的业务含义。
	///
	/// 示例:
	///
	/// "表示订单交易产生的销售金额"
	///
	/// </summary>
	public string? BusinessMeaning { get; set; }



	/// <summary>
	/// 关键词。
	///
	/// 保存JSON字符串或逗号分隔文本。
	///
	/// 示例:
	///
	/// 销售金额,收入,营业额
	///
	/// </summary>
	public string? Keywords { get; set; }



	/// <summary>
	/// 业务同义词。
	///
	/// 示例:
	///
	/// Revenue,Sales Amount
	///
	/// </summary>
	public string? Synonyms { get; set; }



	/// <summary>
	/// 用户可能提出的问题。
	///
	/// 用于增强语义检索。
	///
	/// </summary>
	public string? ExampleQuestions { get; set; }



	/// <summary>
	/// 所属业务领域。
	///
	/// 示例:
	///
	/// 销售
	/// 财务
	/// 库存
	///
	/// </summary>
	public string? BusinessDomain { get; set; }



	/// <summary>
	/// AI生成置信度。
	///
	/// 范围:
	///
	/// 0-1
	///
	/// </summary>
	public decimal? Confidence { get; set; }



	/// <summary>
	/// 语义来源。
	///
	/// 示例:
	///
	/// AI
	/// Manual
	///
	/// </summary>
	public string? Source { get; set; }




	#region Semantic Vector



	/// <summary>
	/// Semantic Embedding文本。
	///
	/// 内容:
	///
	/// BusinessMeaning
	/// Keywords
	/// Synonyms
	/// ExampleQuestions
	///
	/// 用于Qdrant向量检索。
	///
	/// </summary>
	public string? SearchText { get; set; }




	/// <summary>
	/// Qdrant Semantic Vector ID。
	///
	/// 对应:
	///
	/// type = semantic
	///
	/// </summary>
	public string? VectorId { get; set; }




	/// <summary>
	/// 使用的Embedding模型。
	///
	/// 示例:
	///
	/// text-embedding-v3
	///
	/// </summary>
	public string? EmbeddingModel { get; set; }




	/// <summary>
	/// 向量维度。
	///
	/// 示例:
	///
	/// 1536
	///
	/// </summary>
	public int? VectorDimension { get; set; }



	#endregion

}