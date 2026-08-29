using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Interfaces.Platform;
using SuperBuilder_AI.Models.Organization;

namespace SuperBuilder_AI.Controllers;

/// <summary>
/// 本地化 API（P5 Multi-Language Runtime 生产端点）。
///
/// 暴露平台支持的语言区域、文化名解析与标签回退链。
/// 本控制器纯内存计算、不访问数据库、不触碰任何查询链路与 Golden 契约数据；
/// 属平台级管理面，不影响 Golden 18/18 行为契约。
/// </summary>
[ApiController]
[Route("api/localization")]
public sealed class LocalizationController : ControllerBase
{
	private readonly ILocalizationService _localization;

	public LocalizationController(ILocalizationService localization)
	{
		_localization = localization;
	}

	/// <summary>列举平台支持的语言区域。</summary>
	[HttpGet("locales")]
	public IActionResult ListLocales()
	{
		var locales = _localization.SupportedLocales
			.Select(l => new LocaleSummary(l.Culture, l.Language, l.Region, l.DisplayName, l.TimeZoneId, l.TextDirection, l.IsDefault))
			.ToList();
		return Ok(locales);
	}

	/// <summary>
	/// 解析文化名；为 null/空白或无法识别时回退到平台默认语言（zh-CN）。
	/// </summary>
	[HttpGet("resolve")]
	public IActionResult Resolve([FromQuery] string? culture = null)
	{
		var locale = _localization.Resolve(culture);
		return Ok(new LocaleResolution(
			Requested: culture,
			Culture: locale.Culture,
			Language: locale.Language,
			Region: locale.Region,
			DisplayName: locale.DisplayName,
			IsDefault: locale.IsDefault,
			FallbackChain: _localization.BuildFallbackChain(locale)));
	}

	/// <summary>构造指定语言的标签查找回退链。</summary>
	[HttpGet("fallback-chain")]
	public IActionResult FallbackChain([FromQuery] string? culture = null)
	{
		var locale = _localization.Resolve(culture);
		return Ok(new FallbackChainResult(locale.Culture, _localization.BuildFallbackChain(locale)));
	}
}

/// <summary>语言区域摘要 DTO。</summary>
public sealed record LocaleSummary(
	string Culture,
	string Language,
	string? Region,
	string DisplayName,
	string? TimeZoneId,
	string TextDirection,
	bool IsDefault);

/// <summary>语言区域解析结果 DTO（含回退链）。</summary>
public sealed record LocaleResolution(
	string? Requested,
	string Culture,
	string Language,
	string? Region,
	string DisplayName,
	bool IsDefault,
	IReadOnlyList<string> FallbackChain);

/// <summary>回退链结果 DTO。</summary>
public sealed record FallbackChainResult(string Culture, IReadOnlyList<string> FallbackChain);
