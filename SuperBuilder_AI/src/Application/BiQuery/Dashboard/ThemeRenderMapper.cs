using System.Collections.Generic;
using SuperBuilder_AI.Models.Dashboard;
using SuperBuilder_AI.Models.Dashboard.Rendering;
using SuperBuilder_AI.Models.Theme;

namespace SuperBuilder_AI.Services.BI.Dashboard;

/// <summary>
/// 主题 → 渲染模型映射器（P7.3）。纯函数式、无状态、可单测。
///
/// <list type="bullet">
///   <item>把 <see cref="ThemeContext"/> 投影为渲染层友好的 <see cref="ThemeRenderModel"/>，并派生语义色键 → hex 的 <see cref="ThemeRenderModel.ColorMap"/>。</item>
///   <item>把组件级 <see cref="StyleDsl"/> 与主题 <see cref="ThemeDsl.Component"/> 默认值合并为 <see cref="WidgetStyleRenderModel"/>，语义键经 ColorMap 解析为具体色值。</item>
/// </list>
///
/// <para>仅产出结构化令牌，绝不承载 CSS 字符串或任何标记语言——延续 P6/P7 红线。</para>
/// </summary>
public static class ThemeRenderMapper
{
	/// <summary>把主题上下文投影为渲染模型；解析失败或空主题时回退内置默认。</summary>
	public static ThemeRenderModel Build(ThemeContext? context)
	{
		var ctx = context ?? ThemeContext.Default;
		var dsl = ctx.Dsl ?? BuiltInThemes.DefaultDsl();

		var colorMap = new Dictionary<string, string>
		{
			["primary"] = dsl.Color.Primary,
			["success"] = dsl.Color.Success,
			["warning"] = dsl.Color.Warning,
			["danger"] = dsl.Color.Danger,
			["neutral"] = dsl.Color.Neutral,
			["background"] = dsl.Color.Background,
			["surface"] = dsl.Color.Surface,
			["text"] = dsl.Color.Text,
			["textMuted"] = dsl.Color.TextMuted,
			["border"] = dsl.Color.Border,
			["brand.primary"] = dsl.Brand.Primary,
			["brand.accent"] = dsl.Brand.Accent,
		};

		return new ThemeRenderModel
		{
			Key = ctx.Key,
			Source = ctx.Source.ToString(),
			Tokens = dsl,
			ColorMap = colorMap,
			Component = dsl.Component,
		};
	}

	/// <summary>
	/// 合并组件 <see cref="StyleDsl"/> 与主题 Component 默认值，产出可落地的组件风格。
	/// 组件的语义色键经 <paramref name="colorMap"/> 解析为具体色值；未指定时返回 null（前端回退主题令牌）。
	/// </summary>
	public static WidgetStyleRenderModel BuildWidgetStyle(
		StyleDsl? style,
		ThemeDsl theme,
		IReadOnlyDictionary<string, string> colorMap)
	{
		theme ??= BuiltInThemes.DefaultDsl();

		string? ResolveColor(string? semanticKey) =>
			semanticKey is not null && colorMap.TryGetValue(semanticKey, out var hex)
				? hex
				: null;

		return new WidgetStyleRenderModel
		{
			PaletteColor = ResolveColor(style?.Palette),
			BackgroundColor = ResolveColor(style?.Background),
			ShowBorder = style?.ShowBorder ?? theme.Component.CardShowBorder,
			Padding = style?.Padding ?? theme.Component.CardPadding,
		};
	}
}
