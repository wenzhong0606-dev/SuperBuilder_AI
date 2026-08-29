using Microsoft.AspNetCore.Mvc;
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
	private readonly ILocalizationService _localization;

	public SemanticLabelController(ISemanticLabelService labels, ILocalizationService localization)
	{
		_labels = labels;
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
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		var value = await _labels.ResolveAsync(
			conceptType, conceptId, labelKind, culture, tenantId, cancellationToken);

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
		[FromQuery] long tenantId = 0,
		CancellationToken cancellationToken = default)
	{
		var synonyms = await _labels.ResolveSynonymsAsync(
			conceptType, conceptId, culture, tenantId, cancellationToken);

		return Ok(new SynonymResolution(conceptType, conceptId, _localization.Resolve(culture).Culture, synonyms));
	}

	/// <summary>新增或更新一条标签（按 TenantId+ConceptType+ConceptId+Culture+LabelKind 唯一）。</summary>
	[HttpPost]
	public async Task<IActionResult> Upsert(
		[FromBody] UpsertSemanticLabelRequest request,
		CancellationToken cancellationToken = default)
	{
		if (request is null) return BadRequest("请求体不能为空。");

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
