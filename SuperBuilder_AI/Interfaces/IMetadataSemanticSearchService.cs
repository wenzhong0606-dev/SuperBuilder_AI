using SuperBuilder_AI.Models.AI;


namespace SuperBuilder_AI.Interfaces;

/// <summary>
/// Metadata语义检索服务接口
///
/// 负责:
///
/// 用户问题
///     ↓
/// Embedding
///     ↓
/// Qdrant Vector Search
///     ↓
/// MetadataSemanticSearchResult
///
/// 支持:
///
/// 1. table vector
/// 2. column vector
/// 3. semantic vector
///
/// 最终统一返回:
///
/// MetadataTable
/// MetadataColumn
/// MetadataSemantic
///
/// 供 MetadataPromptBuilder 使用。
/// </summary>
public interface IMetadataSemanticSearchService
{


	/// <summary>
	/// 根据用户问题进行Metadata语义搜索。
	///
	/// 搜索范围:
	///
	/// table
	/// column
	/// semantic
	///
	/// </summary>
	/// <param name="question">
	/// 用户自然语言问题。
	/// </param>
	/// <param name="topK">
	/// 返回最大数量。
	/// </param>
	/// <returns>
	/// Metadata语义检索结果集合。
	/// </returns>
	Task<List<MetadataSemanticSearchResult>>
		SearchAsync(
			string question,
			int topK = 10);


}