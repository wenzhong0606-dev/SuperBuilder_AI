using SuperBuilder_AI.Models.Metadata;


namespace SuperBuilder_AI.Interfaces.BI;

/// <summary>
/// Metadata上下文构建器。
///
/// 将数据库字段知识转换为AI Prompt上下文。
///
/// </summary>
public interface IMetadataContextBuilder
{

	/// <summary>
	/// 创建AI理解上下文
	/// </summary>
	Task<string> BuildAsync(
		string question);

	/// <summary>
	/// 在指定数据源作用域内创建AI理解上下文。
	///
	/// 上下文内容来源于 <see cref="SuperBuilder_AI.Interfaces.IMetadataSemanticSearchService"/>
	/// 的向量召回；该召回是全局 top-K，若不加作用域，理解阶段会把<strong>全部数据源</strong>的
	/// 同名列一并喂给 LLM，造成跨源语义污染（实测：问 WMS 入库单，提示词出现 MES 的
	/// <c>mes_eqp_spare_warehouse_enter.type</c>，维度被解析到别的数据源的表上）。
	///
	/// <paramref name="dataSourceIds"/> 为 <c>null</c> 时行为与 <see cref="BuildAsync(string)"/>
	/// 完全一致（Golden / 内部兼容路径不变）。
	/// 默认实现退化为忽略作用域，既有实现类与测试替身无需改动。
	/// </summary>
	/// <param name="question">用户自然语言问题。</param>
	/// <param name="dataSourceIds">允许的数据源集合；<c>null</c> 表示不限定。</param>
	Task<string> BuildAsync(
		string question,
		IReadOnlyCollection<long>? dataSourceIds)
		=> BuildAsync(question);

}
