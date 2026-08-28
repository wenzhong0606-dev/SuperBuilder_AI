using System.Collections.Generic;


namespace SuperBuilder_AI.Models.AI;

/// <summary>
/// Metadata向量索引结果
///
/// 保存:
///
/// 1. 表向量ID
/// 2. 字段向量ID
/// 3. 语义向量ID
///
/// 用于回写SQL Server Metadata表
/// </summary>
public class MetadataVectorIndexResult
{


	/// <summary>
	/// MetadataTable对应Qdrant VectorId
	/// </summary>
	public string TableVectorId
	{
		get;
		set;
	}
	=
	string.Empty;





	/// <summary>
	/// MetadataColumn对应Qdrant VectorId
	///
	/// Key:
	/// MetadataColumn.Id
	///
	/// Value:
	/// Qdrant Point Id
	/// </summary>
	public Dictionary<long, string> ColumnVectors
	{
		get;
		set;
	}
	=
	new();





	/// <summary>
	/// MetadataSemantic对应Qdrant VectorId
	///
	/// Key:
	/// MetadataSemantic.Id
	///
	/// Value:
	/// Qdrant Point Id
	///
	/// 用于语义检索
	/// </summary>
	public Dictionary<long, string> SemanticVectors
	{
		get;
		set;
	}
	=
	new();


}