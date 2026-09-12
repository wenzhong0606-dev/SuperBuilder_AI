using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.Organization;


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
	/// <param name="locale">
	/// 语言区域（P5）。为 <c>null</c> 或平台默认语言（zh-CN）时，行为与 P5 之前完全一致；
	/// 传入其他语言时，对命中多语言标签的语义概念做排序提升（只提升已召回的候选，
	/// 不注入合成候选）。该参数为可选，既有调用方无需改动。
	/// </param>
	/// <returns>
	/// Metadata语义检索结果集合。
	/// </returns>
	Task<List<MetadataSemanticSearchResult>>
		SearchAsync(
			string question,
			int topK = 10,
			LocaleContext? locale = null);

	/// <summary>
	/// 基于语义层关键词/Synonyms 的确定性精确匹配检索，不依赖向量召回深度。
	/// 当向量召回候选缺少词法证据时，按 Keywords/Synonyms 独立关键词精确匹配 query，
	/// 直接返回携带物理绑定的候选（确定性证据，不受向量排序偏序影响）。
	/// 仅用于评估器等需要确定性语义锚定的场景。
	/// </summary>
	Task<List<MetadataSemanticSearchResult>>
		SearchByKeywordAsync(
			string keyword,
			int limit = 30);

	/// <summary>
	/// 基于语义层关键词/Synonyms/BusinessMeaning 的子串确定性匹配检索（用于实体计数等场景）。
	/// 与 <see cref="SearchByKeywordAsync"/> 的精确匹配不同，本方法按“归一化子串包含”匹配，
	/// 以覆盖“入库单ID/入库单号”等将实体词作为前缀的关键词形态，不依赖向量召回深度。
	/// 仅用于评估器等需要确定性语义锚定的场景。
	/// </summary>
	Task<List<MetadataSemanticSearchResult>>
		SearchByKeywordSubstringAsync(
			string keyword,
			int limit = 30);


}