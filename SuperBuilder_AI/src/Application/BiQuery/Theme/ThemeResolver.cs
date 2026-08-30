using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Theme;
using SuperBuilder_AI.Models.Theme;

namespace SuperBuilder_AI.Services.Theming;

/// <summary>
/// 主题级联解析服务（P7.2）。实现 <see cref="IThemeResolver"/> 的
/// 仪表盘显式键 → 租户默认 → 内置默认 三级兜底解析。
/// <para>不承载 CSS/HTML——仅解析出结构化 <see cref="ThemeDsl"/>。</para>
/// </summary>
public sealed class ThemeResolver : IThemeResolver
{
	private const string TenantDefaultThemeKey = "theme:defaultKey";

	private readonly SuperBIContext _db;

	public ThemeResolver(SuperBIContext db) => _db = db;

	/// <inheritdoc/>
	public async Task<ThemeContext> ResolveAsync(long tenantId, string? dashboardThemeKey = null, CancellationToken ct = default)
	{
		// 1) 仪表盘显式主题键：优先命中 租户自有 或 内置(TenantId=0)
		if (!string.IsNullOrWhiteSpace(dashboardThemeKey))
		{
			var explicitTheme = await FindByKeyAsync(tenantId, dashboardThemeKey, ct);
			if (explicitTheme is not null)
				return ToContext(explicitTheme, ThemeSource.Dashboard);
		}

		// 2) 租户默认主题（TenantSetting "theme:defaultKey"）
		if (tenantId > 0)
		{
			var defaultKey = await _db.TenantSettings
				.Where(s => s.TenantId == tenantId && s.Key == TenantDefaultThemeKey)
				.Select(s => s.Value)
				.FirstOrDefaultAsync(ct);
			if (!string.IsNullOrWhiteSpace(defaultKey))
			{
				var tenantTheme = await FindByKeyAsync(tenantId, defaultKey, ct);
				if (tenantTheme is not null)
					return ToContext(tenantTheme, ThemeSource.Tenant);
			}
		}

		// 3) 内置默认兜底
		return ThemeContext.Default;
	}

	/// <summary>
	/// 在「租户自有 ∪ 内置全局」范围内按 key 查找主题；租户自有（TenantId&gt;0）优先于内置。
	/// 使用 <see cref="EntityFrameworkQueryableExtensions.IgnoreQueryFilters"/> 以避开当前作用域的租户过滤，
	/// 由本方法显式约束检索范围，避免跨租户泄漏（仅允许当前租户或内置）。
	/// </summary>
	private async Task<Theme?> FindByKeyAsync(long tenantId, string key, CancellationToken ct)
	{
		return await _db.Themes
			.IgnoreQueryFilters()
			.Where(t => t.Key == key && (t.TenantId == tenantId || t.TenantId == 0))
			.OrderByDescending(t => t.TenantId) // 租户(>0) 排在前，内置(0) 兜底
			.FirstOrDefaultAsync(ct);
	}

	private static ThemeContext ToContext(Theme theme, ThemeSource source) => new()
	{
		Key = theme.Key,
		Source = source,
		Dsl = Deserialize(theme.DslJson),
	};

	private static ThemeDsl Deserialize(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
			return BuiltInThemes.DefaultDsl();
		try
		{
			var dsl = JsonSerializer.Deserialize<ThemeDsl>(json);
			return dsl ?? BuiltInThemes.DefaultDsl();
		}
		catch (JsonException)
		{
			return BuiltInThemes.DefaultDsl();
		}
	}
}
