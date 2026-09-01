using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Api.Security;
using SuperBuilder_AI.Interfaces.Platform;
using SuperBuilder_AI.Models.Localization;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// 业务语义多语言标签 API（P5 Multi-Language Runtime 生产端点）。
///
/// 提供同一语义概念在不同语言下标签的读写与解析（含回退链）。
/// 本控制器只读写 <c>SemanticLabels</c> 表，不触碰查询链路与 Golden 契约数据；
/// 属平台级管理面，不影响 Golden 18/18 行为契约。
/// </summary>
[ApiController]
[Route("api/semantic-labels")]
public sealed class SemanticLabelController : ControllerBase
{
	private readonly ISemanticLabelService _labels;
	private readonly ISemanticLabelRecallService _labelRecall;
	private readonly ILocalizationService _localization;

	public SemanticLabelController(
		ISemanticLabelService labels,
		ISemanticLabelRecallService labelRecall,
		ILocalizationService localization)
	{
		_labels = labels;
		_labelRecall = labelRecall;
		_localization = localization;
	}

	/// <summary>列出某概念在指定语言下的全部标签（不回退，仅精确文化）。</summary>
	[HttpGet]
	public async Task<IActionResult> List(
		[FromQuery] string conceptType,
		[FromQuery] long conceptId,
		[FromQuery] string? culture = null,
		CancellationToken cancellationToken = default)
	{
		var labels = await _labels.ListAsync(conceptType, conceptId, culture, cancellationToken);
		return Ok(labels.Select(ToSummary).ToList());
	}

	/// <summary>
	/// 解析单个标签（按回退链：精确文化 → 语言段 → 平台默认语言）。
	/// 全部未命中时返回 <c>null</c> 的 <c>value</c>，而非 404——标签缺失是常态而非错误。
	/// </summary>
	[HttpGet("resolve")]
	public async Task<IActionResult> Resolve(
		[FromQuery] string conceptType,
		[FromQuery] long conceptId,
		[FromQuery] string labelKind,
		[FromQuery] string? culture = null,
		[FromQuery] long? tenantId = null,
		CancellationToken cancellationToken = default)
	{
		var tenant = TenantDataPlanePolicy.Resolve(User, tenantId);
		if (!tenant.Authorized) return TenantMismatch();
		var value = await _labels.ResolveAsync(
			conceptType, conceptId, labelKind, culture, tenant.EffectiveTenantId, cancellationToken);

		return Ok(new SemanticLabelResolution(
			ConceptType: conceptType,
			ConceptId: conceptId,
			LabelKind: labelKind,
			RequestedCulture: culture,
			ResolvedCulture: _localization.Resolve(culture).Culture,
			FallbackChain: _localization.BuildFallbackChain(_localization.Resolve(culture)),
			Value: value));
	}

	/// <summary>解析某概念在指定语言下的同义词列表。</summary>
	[HttpGet("synonyms")]
	public async Task<IActionResult> Synonyms(
		[FromQuery] string conceptType,
		[FromQuery] long conceptId,
		[FromQuery] string? culture = null,
		[FromQuery] long? tenantId = null,
		CancellationToken cancellationToken = default)
	{
		var tenant = TenantDataPlanePolicy.Resolve(User, tenantId);
		if (!tenant.Authorized) return TenantMismatch();
		var synonyms = await _labels.ResolveSynonymsAsync(
			conceptType, conceptId, culture, tenant.EffectiveTenantId, cancellationToken);

		return Ok(new SynonymResolution(conceptType, conceptId, _localization.Resolve(culture).Culture, synonyms));
	}

	/// <summary>
	/// 多语言标签召回（P5.3）：按问句文本匹配已登记的语义标签。
	/// 传入平台默认语言（zh-CN）或不传 culture 时返回空集合——该门控是 P5 零回归的保证。
	/// </summary>
	[HttpGet("recall")]
	public async Task<IActionResult> Recall(
		[FromQuery] string question,
		[FromQuery] string? culture = null,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(question)) return BadRequest("question 不能为空。");

		var locale = _localization.Resolve(culture);
		var hits = await _labelRecall.MatchAsync(question, locale, cancellationToken);

		return Ok(new LabelRecallResult(
			Question: question,
			RequestedCulture: culture,
			ResolvedCulture: locale.Culture,
			Gated: locale.IsDefault,
			FallbackChain: _localization.BuildFallbackChain(locale),
			Hits: hits.Select(h => new LabelRecallHit(h.SemanticId, h.MatchedLabel, h.Culture, h.Strength)).ToList()));
	}

	/// <summary>新增或更新一条标签（按 TenantId+ConceptType+ConceptId+Culture+LabelKind 唯一）。</summary>
	[HttpPost]
	public async Task<IActionResult> Upsert(
		[FromBody] UpsertSemanticLabelRequest request,
		CancellationToken cancellationToken = default)
	{
		if (request is null) return BadRequest("请求体不能为空。");
		var tenant = TenantDataPlanePolicy.Resolve(User, request.TenantId);
		if (!tenant.Authorized) return TenantMismatch();

		try
		{
			var saved = await _labels.UpsertAsync(request, cancellationToken);
			return Ok(ToSummary(saved));
		}
		catch (ArgumentException ex)
		{
			return BadRequest(ex.Message);
		}
	}

	private ObjectResult TenantMismatch() => StatusCode(403,
		new ApiError { Code = ErrorCodes.TenantIsolated, Message = "禁止：数据面请求租户必须与认证租户一致。" });

	private static SemanticLabelSummary ToSummary(SemanticLabel l)
		=> new(l.Id, l.TenantId, l.ConceptType, l.ConceptId, l.Culture, l.LabelKind, l.Value, l.Source, l.SortOrder);
}

/// <summary>语义标签摘要 DTO。</summary>
public sealed record SemanticLabelSummary(
	long Id,
	long TenantId,
	string ConceptType,
	long ConceptId,
	string Culture,
	string LabelKind,
	string Value,
	string? Source,
	int SortOrder);

/// <summary>标签解析结果 DTO（含回退链，便于排查命中来源）。</summary>
public sealed record SemanticLabelResolution(
	string ConceptType,
	long ConceptId,
	string LabelKind,
	string? RequestedCulture,
	string ResolvedCulture,
	IReadOnlyList<string> FallbackChain,
	string? Value);

/// <summary>同义词解析结果 DTO。</summary>
public sealed record SynonymResolution(
	string ConceptType,
	long ConceptId,
	string Culture,
	IReadOnlyList<string> Synonyms);

/// <summary>标签召回结果 DTO（<c>Gated=true</c> 表示因默认语言被门控短路）。</summary>
public sealed record LabelRecallResult(
	string Question,
	string? RequestedCulture,
	string ResolvedCulture,
	bool Gated,
	IReadOnlyList<string> FallbackChain,
	IReadOnlyList<LabelRecallHit> Hits);

/// <summary>标签召回命中项 DTO。</summary>
public sealed record LabelRecallHit(long SemanticId, string MatchedLabel, string Culture, double Strength);
