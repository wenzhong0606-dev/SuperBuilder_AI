using SuperBuilder_AI.Models.Dashboard;
using SuperBuilder_AI.Models.Theme;

namespace SuperBuilder_AI.Models.Dashboard.Rendering;

/// <summary>渲染结果根模型（P6.3 LowcodeRenderer 输出）。纯结构化，绝不承载 HTML。</summary>
public sealed class DashboardRenderModel
{
	/// <summary>DSL 版本号。</summary>
	public string Version { get; set; } = string.Empty;

	/// <summary>仪表盘标题。</summary>
	public string Title { get; set; } = string.Empty;

	/// <summary>仪表盘描述。</summary>
	public string? Description { get; set; }

	/// <summary>主题键（P7 消费）。</summary>
	public string? ThemeKey { get; set; }

	/// <summary>
	/// 解析后的主题渲染模型（P7.3）。由 <see cref="DashboardLowcodeRenderer"/> 依据
	/// <see cref="SuperBuilder_AI.Models.Organization.PlatformContext.Theme"/> 填充，
	/// 携带具体色值映射，供前端按主题切换风格。绝不承载 CSS 字符串。
	/// </summary>
	public ThemeRenderModel? Theme { get; set; }

	/// <summary>页面渲染模型集合（已按 Order 排序）。</summary>
	public List<PageRenderModel> Pages { get; set; } = new();
}

/// <summary>页面渲染模型（P6.3）。</summary>
public sealed class PageRenderModel
{
	public string Id { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public int Order { get; set; }
	public LayoutRenderModel Layout { get; set; } = new();
	public List<WidgetRenderModel> Widgets { get; set; } = new();
}

/// <summary>布局渲染模型（P6.3）。</summary>
public sealed class LayoutRenderModel
{
	public string Kind { get; set; } = LayoutKinds.Grid;
	public int Columns { get; set; } = 12;
	public int RowHeight { get; set; } = 64;
	public int Gap { get; set; } = 12;
}

/// <summary>组件渲染模型（P6.3）。保留原始组件配置（Source）+ 已解析数据（Data）。</summary>
public sealed class WidgetRenderModel
{
	public string Id { get; set; } = string.Empty;
	public string Type { get; set; } = string.Empty;
	public string? Title { get; set; }
	public GridPositionDsl? Position { get; set; }
	public StyleDsl? Style { get; set; }

	/// <summary>全局与组件级筛选器下推后的完整列表（P6.3 下推语义）。</summary>
	public List<FilterRenderModel> EffectiveFilters { get; set; } = new();

	/// <summary>原始组件配置（保留类型专属字段，前端按 Type 取值）。</summary>
	public WidgetDsl? Source { get; set; }

	/// <summary>已解析的取数结果（无取数组件为 null）。</summary>
	public WidgetDataRenderModel? Data { get; set; }

	/// <summary>AI 洞察占位（运行时由分析链路填充 Insights）。</summary>
	public AiInsightRenderSpec? AiInsight { get; set; }

	/// <summary>
	/// 组件级风格渲染模型（P7.3）。合并主题 Component 默认值与 <see cref="WidgetDsl.Style"/> 的语义键，
	/// 把 "primary"/"surface" 等语义键解析为具体色值/档位。绝不含 CSS 字符串。
	/// </summary>
	public WidgetStyleRenderModel? StyleSpec { get; set; }

	/// <summary>渲染备注（如文本组件净化说明）。</summary>
	public string? Note { get; set; }
}

/// <summary>筛选器渲染模型（P6.3）。</summary>
public sealed class FilterRenderModel
{
	public string Field { get; set; } = string.Empty;
	public string Operator { get; set; } = FilterOperators.Equal;
	public string? Value { get; set; }
	public bool Optional { get; set; } = true;

	/// <summary>来源标记：global（仪表盘级）/ widget（组件级）。</summary>
	public string? Source { get; set; }
}

/// <summary>组件取数结果渲染模型（P6.3）。</summary>
public sealed class WidgetDataRenderModel
{
	public List<string> Columns { get; set; } = new();
	public List<Dictionary<string, object?>> Rows { get; set; } = new();
	public bool Resolved { get; set; }
	public string? Error { get; set; }

	/// <summary>QueryPlanPipeline Decision Gate 结论：Proceed / Blocked / Error / NoQuery。</summary>
	public string Decision { get; set; } = "NoQuery";

	public int RowCount => Rows.Count;

	/// <summary>取首行指定列的值（KPI 取数时用）。</summary>
	public object? FirstValue(string column)
	{
		if (Rows.Count == 0)
		{
			return null;
		}

		return Rows[0].TryGetValue(column, out var v) ? v : null;
	}
}

/// <summary>AI 洞察渲染占位（P6.3）。运行时由分析链路填充 Insights。</summary>
public sealed class AiInsightRenderSpec
{
	public string? Prompt { get; set; }
	public int MaxInsights { get; set; } = 3;
	public string? Perspective { get; set; }
	public List<string> Insights { get; set; } = new();
}

/// <summary>
/// 主题渲染模型（P7.3）。把已解析的 <see cref="ThemeContext"/> 投影为渲染层可直接消费的纯结构化形态：
/// 既保留完整令牌（<see cref="Tokens"/>），又额外给出<strong>语义键 → 具体色值</strong>的解析映射
/// （<see cref="ColorMap"/>），供前端与组件级 <see cref="WidgetStyleRenderModel"/> 直接落地颜色。
/// <para>本模型仅含结构化设计令牌，绝不承载 CSS 字符串或任何标记语言——延续 P6/P7 红线。</para>
/// </summary>
public sealed class ThemeRenderModel
{
	/// <summary>主题键（同 <see cref="ThemeContext.Key"/>）。</summary>
	public string Key { get; set; } = BuiltInThemeKeys.Default;

	/// <summary>解析来源名（BuiltIn / Tenant / Dashboard / Workspace）。</summary>
	public string Source { get; set; } = nameof(ThemeSource.BuiltIn);

	/// <summary>完整已解析的结构化设计令牌（色值/字号/间距等纯数据）。</summary>
	public ThemeDsl Tokens { get; set; } = new();

	/// <summary>
	/// 语义色键 → 具体色值 的解析映射，由 <see cref="ThemeDsl.Color"/>/<see cref="ThemeDsl.Brand"/> 派生。
	/// 例：<c>"primary" → "#2563eb"</c>、<c>"surface" → "#f8fafc"</c>。
	/// 前端与组件级 StyleSpec 据此把语义键落到真实色值；切换主题后此映射随之变化。
	/// </summary>
	public Dictionary<string, string> ColorMap { get; set; } = new();

	/// <summary>组件级默认风格（与 <see cref="WidgetDsl.Style"/> 合并时的兜底）。</summary>
	public ThemeComponent Component { get; set; } = new();
}

/// <summary>
/// 组件级风格渲染模型（P7.3）。合并<strong>主题 Component 默认值</strong>与组件自身
/// <see cref="StyleDsl"/> 的语义键，输出可落地的具体色值与档位。
/// <list type="bullet">
///   <item><see cref="PaletteColor"/> / <see cref="BackgroundColor"/>：组件显式 <see cref="StyleDsl"/> 指定的语义键经 ColorMap 解析后的色值；未指定则为 null（前端回退到主题令牌）。</item>
///   <item><see cref="ShowBorder"/> / <see cref="Padding"/>：组件 <see cref="StyleDsl"/> 优先，否则取主题 Component 默认值。</item>
/// </list>
/// </summary>
public sealed class WidgetStyleRenderModel
{
	/// <summary>组件语义色（Style.Palette 解析后的 hex；未指定为 null）。</summary>
	public string? PaletteColor { get; set; }

	/// <summary>组件背景色（Style.Background 语义键解析后的 hex；未指定为 null）。</summary>
	public string? BackgroundColor { get; set; }

	/// <summary>是否显示边框：Style.ShowBorder 优先，否则取主题 Component.CardShowBorder。</summary>
	public bool ShowBorder { get; set; } = true;

	/// <summary>内边距档位：Style.Padding 优先，否则取主题 Component.CardPadding。</summary>
	public string Padding { get; set; } = "normal";
}
