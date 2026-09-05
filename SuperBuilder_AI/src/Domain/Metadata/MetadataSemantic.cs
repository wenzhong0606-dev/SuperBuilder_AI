using SuperBuilder_AI.Models;
using SuperBuilder_AI.Models.BI.Entity;

namespace SuperBuilder_AI.Models.Metadata;

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
	/// 所属业务领域（字符串，兼容历史数据）。
	///
	/// 自 M1-06 起，新增写入应以 <see cref="BusinessDomainId"/>（外键）为权威，
	/// 本字符串仅作展示兼容，不再作为唯一来源。
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
	/// 所属业务领域 Id（外键，M1-06 新增，作为业务域归属的权威来源）。
	/// 为 null 时表示尚未收敛到 <see cref="BusinessDomain"/> 实体。
	/// </summary>
	public long? BusinessDomainId { get; set; }

	/// <summary>
	/// 关联的业务领域实体（FK 导航，对应 <see cref="BusinessDomainId"/>）。
	/// </summary>
	public BusinessDomain? BusinessDomainRef { get; set; }



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
	/// 语义来源（受控词表）。默认 Manual。
	/// </summary>
	public SemanticSource Source { get; set; } = SemanticSource.Manual;




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


	#endregion


	#region 结构化辅助

	/// <summary>
	/// 将逗号/分号/换行分隔的自由文本解析为去空白、去空项的有序列表。
	/// 用于 Keywords / Synonyms / ExampleQuestions 的归一化。
	/// </summary>
	public static IReadOnlyList<string> ParseList(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			return Array.Empty<string>();
		}

		return raw
			.Split([',', ';', '\n', '\r', '\t'],
				StringSplitOptions.RemoveEmptyEntries)
			.Select(x => x.Trim())
			.Where(x => x.Length > 0)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	/// <summary>
	/// 将字符串列表规范化为逗号分隔的单一文本（保持确定性顺序）。
	/// </summary>
	public static string FormatList(IEnumerable<string> items)
		=> string.Join(", ", items
			.Select(x => x.Trim())
			.Where(x => x.Length > 0));

	#endregion

}