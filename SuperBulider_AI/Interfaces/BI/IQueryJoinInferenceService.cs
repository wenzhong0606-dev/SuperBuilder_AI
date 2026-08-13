using SuperBulider_AI.Models.AI;

namespace SuperBulider_AI.Interfaces;

/// <summary>
/// 动态JOIN推断服务。
///
/// 负责根据当前查询涉及的Metadata表和字段，
/// 推断可能存在的业务JOIN关系。
///
/// 注意:
///
/// 不读取数据库Foreign Key。
///
/// JOIN关系来自:
///
/// Metadata
/// +
/// 字段命名
/// +
/// 数据类型
/// +
/// MetadataSemantic
/// +
/// Embedding
///
/// 最终生成QueryJoin。
/// </summary>
public interface IQueryJoinInferenceService
{
	/// <summary>
	/// 推断查询中的动态JOIN关系。
	/// </summary>
	/// <param name="metadataResults">
	/// 当前用户问题召回的Metadata结果。
	/// </param>
	/// <returns>
	/// JOIN候选集合。
	/// </returns>
	Task<List<QueryJoinCandidate>> InferAsync(
		List<MetadataSemanticSearchResult> metadataResults);
}