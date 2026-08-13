using SuperBulider_AI.Models.Metadata;


namespace SuperBulider_AI.Models.AI;

/// <summary>
/// Metadata语义检索结果
///
/// 用于承载Qdrant向量召回后的统一结果。
///
/// 支持三类Vector:
///
/// 1. table
///    表级语义向量
///
/// 2. column
///    字段结构向量
///
/// 3. semantic
///    字段业务语义向量
///
/// 最终统一转换为:
///
/// MetadataTable
/// MetadataColumn
/// MetadataSemantic
///
/// 供 MetadataPromptBuilder 生成 Text-To-SQL Prompt。
/// </summary>
public class MetadataSemanticSearchResult
{


	/// <summary>
	/// Qdrant Vector类型
	///
	/// 值:
	///
	/// table
	/// column
	/// semantic
	///
	/// </summary>
	public string VectorType
	{
		get;
		set;
	}
	=
	string.Empty;





	/// <summary>
	/// Qdrant Point Id
	///
	/// 对应Vector数据库中的唯一标识。
	/// </summary>
	public string VectorId
	{
		get;
		set;
	}
	=
	string.Empty;





	/// <summary>
	/// 元数据表
	///
	/// table vector 或 column/semantic关联时填充。
	/// </summary>
	public MetadataTable?
		Table
	{
		get;
		set;
	}





	/// <summary>
	/// 元数据字段
	///
	/// column vector 或 semantic vector关联时填充。
	/// </summary>
	public MetadataColumn?
		Column
	{
		get;
		set;
	}





	/// <summary>
	/// 字段业务语义
	///
	/// semantic vector匹配时填充。
	/// </summary>
	public MetadataSemantic?
		Semantic
	{
		get;
		set;
	}





	/// <summary>
	/// Qdrant相似度评分
	///
	/// Cosine:
	///
	/// 越接近1表示语义越相似。
	/// </summary>
	public double Score
	{
		get;
		set;
	}





	/// <summary>
	/// 是否来自表级向量
	/// </summary>
	public bool IsTableVector
	{
		get
		{
			return VectorType == "table";
		}
	}





	/// <summary>
	/// 是否来自字段向量
	/// </summary>
	public bool IsColumnVector
	{
		get
		{
			return VectorType == "column";
		}
	}





	/// <summary>
	/// 是否来自语义向量
	/// </summary>
	public bool IsSemanticVector
	{
		get
		{
			return VectorType == "semantic";
		}
	}

}