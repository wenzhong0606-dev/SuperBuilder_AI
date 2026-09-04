using SuperBuilder_AI.Models.Localization;

namespace SuperBuilder_AI.Interfaces.Platform;

/// <summary>
/// 业务语义多语言标签服务端口（P5 Multi-Language Runtime）。
///
/// 负责"同一语义概念在不同语言下的表述"的读写与解析：
/// <c>销售额</c> / <c>Sales Amount</c> / <c>売上高</c> 映射到同一 Concept。
///
/// 解析采用 <see cref="ILocalizationService"/> 提供的回退链：
/// 精确文化 → 语言段 → 平台默认语言，保证任何语言下都至少能取到默认译文。
/// </summary>
public interface ISemanticLabelService
{
	/// <summary>
	/// 解析单个标签文本。
	/// 按回退链依次查找，返回首个命中；全部未命中时返回 null。
	/// </summary>
	/// <param name="conceptType">概念类型，取值见 <see cref="SemanticConceptTypes"/>。</param>
	/// <param name="conceptId">概念实体 Id。</param>
	/// <param name="labelKind">标签种类，取值见 <see cref="SemanticLabelKinds"/>。</param>
	/// <param name="culture">目标语言；null/无效时回退平台默认语言。</param>
	/// <param name="tenantId">租户 Id，用于隔离私有标签（0 表示仅取全局共享标签）。</param>
	Task<string?> ResolveAsync(
		string conceptType,
		long conceptId,
		string labelKind,
		string? culture = null,
		long tenantId = 0,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// 解析某概念在指定语言下的同义词列表（按 <see cref="SemanticLabel.SortOrder"/> 排序）。
	/// 用于扩展向量检索的召回面：多语言问句也能命中同一语义概念。
	/// </summary>
	Task<IReadOnlyList<string>> ResolveSynonymsAsync(
		string conceptType,
		long conceptId,
		string? culture = null,
		long tenantId = 0,
		CancellationToken cancellationToken = default);

	/// <summary>列出某概念在指定语言下的全部标签（不回退，仅精确文化）。</summary>
	Task<IReadOnlyList<SemanticLabel>> ListAsync(
		string conceptType,
		long conceptId,
		string? culture = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// 新增或更新一条标签（按 TenantId + ConceptType + ConceptId + Culture + LabelKind 唯一）。
	/// </summary>
	Task<SemanticLabel> UpsertAsync(
		UpsertSemanticLabelRequest request,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// 按主键获取单条标签（M0-02 详情端点用）。仅返回属于指定租户或全局共享(TenantId=0)的标签。
	/// </summary>
	Task<SemanticLabel?> GetByIdAsync(
		long id,
		long tenantId,
		CancellationToken cancellationToken = default);
}

/// <summary>写入/更新语义标签的请求。</summary>
public sealed record UpsertSemanticLabelRequest(
	long TenantId,
	string ConceptType,
	long ConceptId,
	string Culture,
	string LabelKind,
	string Value,
	string? Source = null,
	int SortOrder = 0);
