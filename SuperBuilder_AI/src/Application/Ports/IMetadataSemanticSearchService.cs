using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.Metadata;
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
	/// 数据源作用域内的 Metadata 语义搜索。
	///
	/// 与 <see cref="SearchAsync(string,int,LocaleContext)"/> 的唯一差别：
	/// 召回结果按 <c>MetadataTable.DataSourceId</c> 收敛到 <paramref name="dataSourceIds"/>。
	///
	/// 为什么需要它：本服务是<strong>全局 top-K</strong> 召回（Qdrant 无数据源过滤条件），
	/// 若在「查询理解」阶段把全部数据源的表/列塞进 LLM 提示词，跨源同名列会污染
	/// 指标 / 维度解析（例如问 WMS 入库单，提示词里却出现 MES 的 <c>type</c> 列），
	/// 导致意图落到别的数据源的表上、置信度被压低而错走澄清。
	///
	/// 语义约定（与 <c>IQueryPlanDataSourceScope</c> 保持一致）：
	/// <list type="bullet">
	/// <item><c>null</c> —— 不限定数据源，行为与新增本重载之前<strong>逐字节一致</strong>
	/// （Golden / 评估器 / 内部兼容路径走此分支）。</item>
	/// <item>非空集合 —— 仅返回归属该集合的表/列/语义候选；集合为空则返回空结果
	/// （不抛异常：本方法是检索原语，「无候选」的处置由调用方决定）。</item>
	/// </list>
	///
	/// 默认实现退化为忽略作用域（委托三参重载），因此既有实现类与测试替身无需改动。
	/// </summary>
	/// <param name="question">用户自然语言问题。</param>
	/// <param name="topK">作用域过滤<strong>之后</strong>返回的最大数量（实现内部按倍数过采样）。</param>
	/// <param name="locale">语言区域（P5），语义同三参重载。</param>
	/// <param name="dataSourceIds">允许的数据源集合；<c>null</c> 表示不限定。</param>
	Task<List<MetadataSemanticSearchResult>>
		SearchAsync(
			string question,
			int topK,
			LocaleContext? locale,
			IReadOnlyCollection<long>? dataSourceIds)
		=> SearchAsync(question, topK, locale);

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

	/// <summary>
	/// 按物理表名在（已授权）元数据库目录中确定性解析目标表。
	///
	/// 用于多轮表纠正：当用户显式纠正到某张表时，即便该表因数据源范围收敛
	/// （<see cref="IQueryPlanDataSourceScope"/>）未进入语义检索候选，也能从
	/// 全部“已授权”数据源中按表名找回，强制作为主表，避免纠正失效。
	///
	/// 不依赖向量召回深度，纯目录精确匹配（表名大小写不敏感）。
	/// 调用方须保证 <paramref name="authorizedDataSourceIds"/> 传的是“已授权”集合，
	/// 以守住跨租户 / 跨数据源安全。
	/// </summary>
	/// <param name="tableName">物理表名（大小写不敏感精确匹配）。</param>
	/// <param name="authorizedDataSourceIds">
	/// 已授权数据源集合；为 <c>null</c> 或空时匹配范围内的全部表。
	/// </param>
	Task<MetadataTable?> ResolveTableByNameAsync(
		string tableName,
		IReadOnlyCollection<long>? authorizedDataSourceIds = null)
		=> Task.FromResult<MetadataTable?>(null);


}