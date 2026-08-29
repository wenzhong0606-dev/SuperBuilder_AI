using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Platform;
using SuperBuilder_AI.Models.Localization;

namespace SuperBuilder_AI.Services.Platform;

/// <summary>
/// <see cref="ISemanticLabelService"/> 的默认实现（P5 Multi-Language Runtime）。
///
/// 实现要点：
/// <list type="number">
/// <item>回退链在<strong>内存</strong>中定序：先把链上全部候选一次查回（单次 SQL），
///       再按链序取首个命中。避免 N 次往返。</item>
/// <item>租户隔离复用 P4.3 的 <c>SuperBIContext</c> 全局过滤；全局共享标签（<c>TenantId=0</c>）
///       由该过滤显式放行，无需在此重复处理。</item>
/// <item>解析失败一律软降级返回 null / 空集合，绝不抛异常阻断主链路——与 P3 业务实体
///       语义映射的容错约定保持一致。</item>
/// </list>
/// </summary>
public sealed class SemanticLabelService : ISemanticLabelService
{
	private readonly SuperBIContext _db;
	private readonly ILocalizationService _localization;

	public SemanticLabelService(SuperBIContext db, ILocalizationService localization)
	{
		_db = db;
		_localization = localization;
	}

	/// <inheritdoc />
	public async Task<string?> ResolveAsync(
		string conceptType,
		long conceptId,
		string labelKind,
		string? culture = null,
		long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(conceptType) || string.IsNullOrWhiteSpace(labelKind))
			return null;

		var chain = _localization.BuildFallbackChain(_localization.Resolve(culture));

		var candidates = await _db.SemanticLabels
			.AsNoTracking()
			.Where(l => l.ConceptType == conceptType
				&& l.ConceptId == conceptId
				&& l.LabelKind == labelKind
				&& chain.Contains(l.Culture))
			.ToListAsync(cancellationToken);

		return OrderByChain(candidates, chain)
			.Select(l => l.Value)
			.FirstOrDefault();
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<string>> ResolveSynonymsAsync(
		string conceptType,
		long conceptId,
		string? culture = null,
		long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(conceptType)) return Array.Empty<string>();

		var chain = _localization.BuildFallbackChain(_localization.Resolve(culture));

		var candidates = await _db.SemanticLabels
			.AsNoTracking()
			.Where(l => l.ConceptType == conceptType
				&& l.ConceptId == conceptId
				&& l.LabelKind == SemanticLabelKinds.Synonym
				&& chain.Contains(l.Culture))
			.ToListAsync(cancellationToken);

		if (candidates.Count == 0) return Array.Empty<string>();

		// 同义词多值：先按回退链优先级（精确文化优先），再按 SortOrder。
		return OrderByChain(candidates, chain)
			.Select(l => l.Value)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<SemanticLabel>> ListAsync(
		string conceptType,
		long conceptId,
		string? culture = null,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(conceptType)) return Array.Empty<SemanticLabel>();

		var targetCulture = _localization.Resolve(culture).Culture;

		return await _db.SemanticLabels
			.AsNoTracking()
			.Where(l => l.ConceptType == conceptType
				&& l.ConceptId == conceptId
				&& l.Culture == targetCulture)
			.OrderBy(l => l.LabelKind)
			.ThenBy(l => l.SortOrder)
			.ToListAsync(cancellationToken);
	}

	/// <inheritdoc />
	public async Task<SemanticLabel> UpsertAsync(
		UpsertSemanticLabelRequest request,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);

		var conceptType = (request.ConceptType ?? string.Empty).Trim();
		if (conceptType.Length == 0)
			throw new ArgumentException("ConceptType 不能为空。", nameof(request));

		var value = (request.Value ?? string.Empty).Trim();
		if (value.Length == 0)
			throw new ArgumentException("Value 不能为空。", nameof(request));

		// 文化名统一走归一化，避免 "zh_TW" 与 "zh-TW" 产生两条重复标签。
		var culture = _localization.Resolve(request.Culture).Culture;
		var labelKind = (request.LabelKind ?? string.Empty).Trim();

		// 查找键必须与唯一索引 (TenantId, ConceptType, ConceptId, Culture, LabelKind, SortOrder) 完全对齐：
		// SortOrder 是多值标签（同义词/示例问句）的槽位判别列，漏掉它会把不同槽位的同义词
		// 误判为同一条并反复覆盖，导致一个概念只剩最后一个同义词。
		var existing = await _db.SemanticLabels
			.FirstOrDefaultAsync(
				l => l.TenantId == request.TenantId
					&& l.ConceptType == conceptType
					&& l.ConceptId == request.ConceptId
					&& l.Culture == culture
					&& l.LabelKind == labelKind
					&& l.SortOrder == request.SortOrder,
				cancellationToken);

		if (existing is null)
		{
			existing = new SemanticLabel
			{
				TenantId = request.TenantId,
				ConceptType = conceptType,
				ConceptId = request.ConceptId,
				Culture = culture,
				LabelKind = labelKind,
			};
			_db.SemanticLabels.Add(existing);
		}

		existing.Value = value;
		existing.Source = string.IsNullOrWhiteSpace(request.Source) ? "Manual" : request.Source.Trim();
		existing.SortOrder = request.SortOrder;

		await _db.SaveChangesAsync(cancellationToken);
		return existing;
	}

	/// <summary>把候选标签按回退链顺序排列（链上越靠前优先级越高）。</summary>
	private static IOrderedEnumerable<SemanticLabel> OrderByChain(
		IEnumerable<SemanticLabel> candidates,
		IReadOnlyList<string> chain)
	{
		// IReadOnlyList<T> 无 IndexOf，先固化成"文化 → 链上序号"字典再排序。
		var rank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		for (var i = 0; i < chain.Count; i++) rank[chain[i]] = i;

		return candidates
			.OrderBy(l => rank.TryGetValue(l.Culture, out var index) ? index : int.MaxValue)
			.ThenBy(l => l.SortOrder);
	}
}
