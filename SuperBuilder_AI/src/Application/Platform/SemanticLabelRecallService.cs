using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Platform;
using SuperBuilder_AI.Models.Localization;
using SuperBuilder_AI.Models.Organization;

namespace SuperBuilder_AI.Services.Platform;

/// <summary>
/// <see cref="ISemanticLabelRecallService"/> 的默认实现（P5 Multi-Language Runtime）。
///
/// 匹配算法（确定性、可离线断言）：
/// <list type="number">
/// <item>门控：默认语言 / Invariant / 空问句 → 直接返回空集合。</item>
/// <item>候选：按回退链取出该语言下全部 <c>DisplayName</c> 与 <c>Synonym</c> 标签。</item>
/// <item>匹配：问句包含标签文本即命中（大小写不敏感）。CJK 无词边界，子串包含是最稳的判据；
///       英文为规避短词误命中（如 "date" 落在 "update" 里），要求标签长度 ≥ 2。</item>
/// <item>打分：<c>Strength = 标签长度 / 本次最长命中长度</c>——长标签更具体，权重更高。</item>
/// <item>归并：同一语义概念只保留最强命中。</item>
/// </list>
/// </summary>
public sealed class SemanticLabelRecallService : ISemanticLabelRecallService
{
	/// <summary>英文等有词边界的语言里，过短标签子串误命中率过高，设下限。</summary>
	private const int MinLabelLength = 2;

	private readonly SuperBIContext _db;
	private readonly ILocalizationService _localization;

	public SemanticLabelRecallService(SuperBIContext db, ILocalizationService localization)
	{
		_db = db;
		_localization = localization;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<SemanticLabelHit>> MatchAsync(
		string question,
		LocaleContext? locale,
		CancellationToken cancellationToken = default)
	{
		// 门控：默认语言与 Invariant 直接短路。
		// 这是 P5 的零回归开关——Golden 与既有中文链路从不进入本方法体。
		if (locale is null || locale.IsDefault || string.IsNullOrEmpty(locale.Culture))
			return Array.Empty<SemanticLabelHit>();

		if (string.IsNullOrWhiteSpace(question))
			return Array.Empty<SemanticLabelHit>();

		var normalizedQuestion = Normalize(question);
		if (normalizedQuestion.Length == 0)
			return Array.Empty<SemanticLabelHit>();

		var chain = _localization.BuildFallbackChain(locale);

		var candidates = await _db.SemanticLabels
			.AsNoTracking()
			.Where(l => l.ConceptType == SemanticConceptTypes.MetadataSemantic
				&& chain.Contains(l.Culture)
				&& (l.LabelKind == SemanticLabelKinds.DisplayName
					|| l.LabelKind == SemanticLabelKinds.Synonym))
			.Select(l => new { l.ConceptId, l.Culture, l.Value })
			.ToListAsync(cancellationToken);

		if (candidates.Count == 0) return Array.Empty<SemanticLabelHit>();

		// 先收集全部命中，再按全局最长命中归一化强度。
		var hits = new List<(long SemanticId, string Label, string Culture, int Length)>();
		foreach (var candidate in candidates)
		{
			var label = Normalize(candidate.Value);
			if (label.Length < MinLabelLength) continue;
			if (!normalizedQuestion.Contains(label, StringComparison.Ordinal)) continue;

			hits.Add((candidate.ConceptId, candidate.Value, candidate.Culture, label.Length));
		}

		if (hits.Count == 0) return Array.Empty<SemanticLabelHit>();

		var maxLength = hits.Max(h => h.Length);

		return hits
			.GroupBy(h => h.SemanticId)
			.Select(g =>
			{
				var best = g.OrderByDescending(h => h.Length).First();
				return new SemanticLabelHit(
					best.SemanticId,
					best.Label,
					best.Culture,
					maxLength <= 0 ? 0 : (double)best.Length / maxLength);
			})
			.OrderByDescending(h => h.Strength)
			.ToList();
	}

	/// <summary>归一化：全角空格统一、去首尾空白、转小写。</summary>
	private static string Normalize(string value)
		=> value.Replace('\u3000', ' ').Trim().ToLowerInvariant();
}
