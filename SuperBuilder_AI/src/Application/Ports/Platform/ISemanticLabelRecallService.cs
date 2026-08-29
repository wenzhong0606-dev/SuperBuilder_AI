using SuperBuilder_AI.Models.Organization;

namespace SuperBuilder_AI.Interfaces.Platform;

/// <summary>
/// 语义标签召回服务端口（P5 Multi-Language Runtime）。
///
/// 解决跨语言检索的"低召回/低排序"问题：跨语言 Embedding 常能把非默认语言的问句
/// 映射到正确语义概念，但排序偏后。本服务按<strong>已登记的多语言标签</strong>做确定性
/// 文本匹配，把命中的语义概念及其匹配强度返回，供检索层提升其排序。
///
/// <para>
/// 设计约束（P5 零回归要求）：
/// <list type="bullet">
/// <item>仅当传入<strong>非默认</strong>语言区域时执行；默认语言（zh-CN）与
///       <see cref="LocaleContext.Invariant"/> 一律返回空结果——Golden 与既有中文链路
///       因此完全不受影响。</item>
/// <item>纯确定性文本匹配，不调用 LLM、不访问向量库。</item>
/// <item>只返回<strong>已存在</strong>的语义概念 Id，绝不合成新候选。</item>
/// </list>
/// </para>
/// </summary>
public interface ISemanticLabelRecallService
{
	/// <summary>
	/// 按问句文本在指定语言下匹配语义标签。
	/// </summary>
	/// <param name="question">用户问句（原始文本）。</param>
	/// <param name="locale">语言区域；为 null / 默认语言 / Invariant 时返回空集合。</param>
	/// <returns>命中结果（按匹配强度降序，同一语义概念只保留最强命中）。</returns>
	Task<IReadOnlyList<SemanticLabelHit>> MatchAsync(
		string question,
		LocaleContext? locale,
		CancellationToken cancellationToken = default);
}

/// <summary>
/// 标签命中结果。
/// </summary>
/// <param name="SemanticId">命中的语义概念 Id（<c>MetadataSemantic.Id</c>）。</param>
/// <param name="MatchedLabel">命中的标签文本。</param>
/// <param name="Culture">命中标签所属语言。</param>
/// <param name="Strength">匹配强度 0–1，由命中标签长度归一化得到（越长越具体）。</param>
public sealed record SemanticLabelHit(
	long SemanticId,
	string MatchedLabel,
	string Culture,
	double Strength);
