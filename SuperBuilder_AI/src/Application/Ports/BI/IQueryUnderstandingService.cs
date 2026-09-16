using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Organization;


namespace SuperBuilder_AI.Interfaces.BI;

/// <summary>
/// 用户问题理解服务。
///
/// 负责:
///
/// 用户语言
/// ↓
/// AI分析
/// ↓
/// QueryIntent
///
/// </summary>
public interface IQueryUnderstandingService
{


	/// <summary>
	/// 理解用户查询
	/// </summary>
	Task<QueryIntent> UnderstandAsync(
		string question);

	/// <summary>
	/// 理解用户查询并注入业务实体感知（P3）。
	/// 提供平台运行时上下文（PlatformContext，P4）后，归一化阶段会识别候选业务实体并写入 QueryIntent.BusinessEntityHints。
	/// </summary>
	Task<QueryIntent> UnderstandAsync(
		string question,
		PlatformContext platformContext);

	/// <summary>
	/// 在指定数据源作用域内理解用户查询（P0：理解阶段的跨源语义污染修复）。
	///
	/// 理解阶段的提示词由 <see cref="IMetadataContextBuilder"/> 的 Metadata 召回填充，
	/// 该召回是全局 top-K。传入 <paramref name="dataSourceIds"/> 可把提示词收敛到
	/// 当前用户「已授权 / 已显式请求」的数据源，避免跨源同名列（如多套系统里都叫
	/// <c>type</c> / <c>status</c> 的列）把 Metric / Dimension 解析带偏。
	///
	/// <paramref name="dataSourceIds"/> 为 <c>null</c> 时行为与
	/// <see cref="UnderstandAsync(string,PlatformContext)"/> 完全一致
	/// （Golden / 评估器 / 内部兼容路径不变）。
	/// 默认实现退化为忽略作用域，既有实现类与测试替身无需改动。
	/// </summary>
	/// <param name="question">用户自然语言问题。</param>
	/// <param name="platformContext">平台运行时上下文（租户 / 语言 / 主题）。</param>
	/// <param name="dataSourceIds">允许的数据源集合；<c>null</c> 表示不限定。</param>
	Task<QueryIntent> UnderstandAsync(
		string question,
		PlatformContext platformContext,
		IReadOnlyCollection<long>? dataSourceIds)
		=> UnderstandAsync(question, platformContext);


}
