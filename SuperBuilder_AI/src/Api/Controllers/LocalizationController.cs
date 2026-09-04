using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Interfaces.Platform;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models.Localization;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Services.Platform;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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
	private readonly SuperBIContext _db;
	private readonly ILocalizationSeedService _seed;

	public LocalizationController(ILocalizationService localization, SuperBIContext db, ILocalizationSeedService seed)
	{
		_localization = localization;
		_db = db;
		_seed = seed;
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

	/// <summary>数据库维护的可用语言目录；首次使用时从平台内置区域初始化。</summary>
	[HttpGet("languages")]
	public async Task<IActionResult> Languages(CancellationToken ct)
	{
		await EnsureSeedAsync(ct);
		var cultures = await AllowedCulturesAsync(ct);
		return Ok(await _db.UiLanguages.AsNoTracking().Where(x => x.Enabled && cultures.Contains(x.Culture)).OrderBy(x => x.SortOrder)
			.Select(x => new { x.Id, x.Culture, x.DisplayName, x.NativeName, x.Enabled, x.SortOrder }).ToListAsync(ct));
	}

	// AUTH-2：登录前只读公共入口，须显式放行（AuthMiddleware 白名单与控制器层兜底过滤器均据此豁免）。
	[HttpGet("public/languages")]
	[AllowAnonymous]
	public async Task<IActionResult> PublicLanguages(CancellationToken ct)
	{
		await EnsureSeedAsync(ct);
		return Ok(await _db.UiLanguages.AsNoTracking().Where(x => x.Enabled).OrderBy(x => x.SortOrder)
			.Select(x => new { x.Culture, x.DisplayName, x.NativeName }).ToListAsync(ct));
	}

	// AUTH-2：同上，登录前只读公共入口。
	[HttpGet("public/texts")]
	[AllowAnonymous]
	public async Task<IActionResult> PublicTexts([FromQuery] string culture, CancellationToken ct = default)
	{
		await EnsureSeedAsync(ct);
		// M0-08：匿名公共端点只返回平台基线（TenantId==0），禁止按任意 tenantId 枚举租户专属文案
		var rows = await _db.UiTextResources.AsNoTracking().Where(x => x.Culture == culture && x.TenantId == 0).ToListAsync(ct);
		return Ok(rows.OrderBy(x => x.ResourceKey).Select(x => new { x.ResourceKey, PlatformValue = x.Value, Value = x.Value, IsOverridden = false, x.Description }));
	}

	[HttpPost("languages")]
	public async Task<IActionResult> CreateLanguage([FromBody] SaveUiLanguageRequest request, CancellationToken ct)
	{
		if (!User.HasClaim("perm", IdentityPermissions.PlatformTenantManage)) return Forbid();
		await EnsureSeedAsync(ct);
		var culture = (request.Culture ?? string.Empty).Trim();
		if (culture.Length < 2 || await _db.UiLanguages.AnyAsync(x => x.Culture == culture, ct)) return Conflict("语言代码为空或已存在。");
		var language = new UiLanguage { Culture = culture, DisplayName = request.DisplayName?.Trim() ?? culture, NativeName = request.NativeName?.Trim() ?? culture, Enabled = true, SortOrder = await _db.UiLanguages.CountAsync(ct) };
		_db.UiLanguages.Add(language);
		var sourceCulture = string.IsNullOrWhiteSpace(request.CopyFromCulture) ? "en-US" : request.CopyFromCulture;
		var source = await _db.UiTextResources.AsNoTracking().Where(x => x.TenantId == 0 && x.Culture == sourceCulture).ToListAsync(ct);
		_db.UiTextResources.AddRange(source.Select(x => new UiTextResource { TenantId=0, Culture=culture, ResourceKey=x.ResourceKey, Value=x.Value, Description=x.Description }));
		await _db.SaveChangesAsync(ct); return Ok(new { language.Id, language.Culture, language.DisplayName, language.NativeName });
	}

	/// <summary>读取平台基线以及当前租户覆盖后的文本。</summary>
	[HttpGet("texts")]
	public async Task<IActionResult> Texts([FromQuery] string culture, [FromQuery] long? tenantId, CancellationToken ct)
	{
		await EnsureSeedAsync(ct);
		var targetTenant = ResolveTargetTenant(tenantId);
		if (targetTenant < 0) return Forbid();
		var rows = await _db.UiTextResources.AsNoTracking()
			.Where(x => x.Culture == culture && (x.TenantId == 0 || x.TenantId == targetTenant)).ToListAsync(ct);
		var platform = rows.Where(x => x.TenantId == 0).ToDictionary(x => x.ResourceKey, StringComparer.OrdinalIgnoreCase);
		var overrides = rows.Where(x => x.TenantId == targetTenant && targetTenant > 0).ToDictionary(x => x.ResourceKey, StringComparer.OrdinalIgnoreCase);
		return Ok(platform.Values.OrderBy(x => x.ResourceKey).Select(x => new
		{
			x.ResourceKey, PlatformValue = x.Value,
			Value = overrides.TryGetValue(x.ResourceKey, out var own) ? own.Value : x.Value,
			IsOverridden = overrides.ContainsKey(x.ResourceKey), x.Description
		}));
	}

	[HttpPut("texts/{culture}/{key}")]
	public async Task<IActionResult> SaveText(string culture, string key, [FromQuery] long? tenantId, [FromBody] SaveUiTextRequest request, CancellationToken ct)
	{
		var targetTenant = ResolveTargetTenant(tenantId);
		if (targetTenant < 0 || (targetTenant == 0 && !User.HasClaim("perm", IdentityPermissions.PlatformTenantManage))) return Forbid();
		if (targetTenant > 0 && !User.HasClaim("perm", IdentityPermissions.IdentityManage)) return Forbid();
		var value = (request.Value ?? string.Empty).Trim();
		if (value.Length == 0) return BadRequest("文本不能为空。");
		var row = await _db.UiTextResources.FirstOrDefaultAsync(x => x.TenantId == targetTenant && x.Culture == culture && x.ResourceKey == key, ct);
		if (row is null) { row = new UiTextResource { TenantId = targetTenant, Culture = culture, ResourceKey = key }; _db.UiTextResources.Add(row); }
		row.Value = value;
		row.Description = request.Description ?? row.Description;
		await _db.SaveChangesAsync(ct);
		return Ok();
	}

	[HttpDelete("texts/{culture}/{key}/override")]
	public async Task<IActionResult> ResetText(string culture, string key, CancellationToken ct)
	{
		var tenantId = CurrentTenantId();
		if (tenantId <= 0) return BadRequest("平台基线不能使用重置覆盖操作。");
		if (!User.HasClaim("perm", IdentityPermissions.IdentityManage)) return Forbid();
		var row = await _db.UiTextResources.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Culture == culture && x.ResourceKey == key, ct);
		if (row is not null) { _db.UiTextResources.Remove(row); await _db.SaveChangesAsync(ct); }
		return NoContent();
	}

	private long ResolveTargetTenant(long? requested)
	{
		if (User.HasClaim("perm", IdentityPermissions.PlatformTenantView)) return requested ?? 0;
		return CurrentTenantId();
	}

	private async Task<List<string>> AllowedCulturesAsync(CancellationToken ct)
	{
		if (User.HasClaim("perm", IdentityPermissions.PlatformTenantView))
			return await _db.UiLanguages.AsNoTracking().Where(x => x.Enabled).Select(x => x.Culture).ToListAsync(ct);
		var tenantId = CurrentTenantId();
		var json = await _db.TenantSettings.AsNoTracking().Where(x => x.TenantId == tenantId && x.Key == "localization:availableCultures").Select(x => x.Value).FirstOrDefaultAsync(ct);
		try { return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json ?? "[]") ?? new(); }
		catch { return new(); }
	}

	private long CurrentTenantId() => long.TryParse(User.FindFirst("tid")?.Value, out var id) ? id : 0;

	private async Task EnsureSeedAsync(CancellationToken ct) => await _seed.EnsureSeedAsync(ct);
}

public sealed record SaveUiTextRequest(string? Value, string? Description = null);
public sealed record SaveUiLanguageRequest(string? Culture, string? DisplayName, string? NativeName, string? CopyFromCulture = null);

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
