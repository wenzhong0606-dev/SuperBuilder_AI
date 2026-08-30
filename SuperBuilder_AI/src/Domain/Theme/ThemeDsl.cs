namespace SuperBuilder_AI.Models.Theme;

/// <summary>主题 DSL 版本号常量（P7 Multi-Theme / Style Engine）。</summary>
public static class ThemeDslVersions
{
	/// <summary>首个正式版本。</summary>
	public const string V1 = "1.0";

	/// <summary>当前版本（新增主题令牌时以此为默认）。</summary>
	public const string Current = V1;

	/// <summary>受支持的版本集合（反序列化白名单）。</summary>
	public static IReadOnlyList<string> Supported { get; } = new[] { V1 };
}

/// <summary>内置主题键（P7）。</summary>
public static class BuiltInThemeKeys
{
	/// <summary>平台默认浅色主题。</summary>
	public const string Default = "default";
}

/// <summary>
/// 主题 DSL 根对象（P7 Multi-Theme / Style Engine）。
///
/// <para>
/// 描述一组<strong>结构化设计令牌</strong>（design tokens）：品牌 / 色彩 / 字体 / 布局 / 边框 /
/// 圆角 / 阴影 / 图表色板 / 组件 / 仪表盘模板。所有令牌都是纯数据（色值、字号、像素、圆角），
/// <strong>绝不承载 CSS 字符串或任何标记语言</strong>——这与 P6 Low-code 的红线一致。
/// </para>
///
/// <para>
/// 渲染层（P7.3 <c>LowcodeRenderer</c>）在运行时把语义键（如 <c>Palette=primary</c>、
/// <c>Background=surface</c>）映射到 <see cref="ThemeColor"/> 的具体色值；前端再据此生成样式。
/// 这样同一份 Dashboard DSL 在切换主题后会产生不同风格，而主题数据本身永远保持结构化、可版本化、可 AI 生成。
/// </para>
/// </summary>
public sealed class ThemeDsl
{
	/// <summary>DSL 版本号，默认 <see cref="ThemeDslVersions.Current"/>。</summary>
	public string Version { get; set; } = ThemeDslVersions.Current;

	/// <summary>品牌令牌（Logo 主色、强调色等）。</summary>
	public ThemeBrand Brand { get; set; } = new();

	/// <summary>色彩令牌（语义色板 + 背景/文字/边框）。</summary>
	public ThemeColor Color { get; set; } = new();

	/// <summary>字体令牌（字体族、字号阶梯、行高）。</summary>
	public ThemeTypography Typography { get; set; } = new();

	/// <summary>布局令牌（栅格、间距、最大宽度）。</summary>
	public ThemeLayout Layout { get; set; } = new();

	/// <summary>边框令牌（默认宽度与样式）。</summary>
	public ThemeBorder Border { get; set; } = new();

	/// <summary>圆角令牌（不同层级组件的半径）。</summary>
	public ThemeRadius Radius { get; set; } = new();

	/// <summary>阴影令牌（卡片/浮层等的阴影档位）。</summary>
	public ThemeShadow Shadow { get; set; } = new();

	/// <summary>图表色板（多系列取色序列 + 语义系列色）。</summary>
	public ThemeChartPalette ChartPalette { get; set; } = new();

	/// <summary>组件级默认风格（卡片/按钮/表格/KPI 等）。</summary>
	public ThemeComponent Component { get; set; } = new();

	/// <summary>仪表盘模板（整体背景、容器、页头风格）。</summary>
	public ThemeDashboardTemplate DashboardTemplate { get; set; } = new();
}

/// <summary>品牌令牌（P7）。纯数据，不含任何标记语言。</summary>
public sealed class ThemeBrand
{
	/// <summary>品牌主色（用于 Logo 背景、强调态）。</summary>
	public string Primary { get; set; } = "#2563eb";

	/// <summary>品牌辅助色。</summary>
	public string Accent { get; set; } = "#7c3aed";
}

/// <summary>色彩令牌（P7）。语义色板映射到具体色值；渲染层按语义键取用。</summary>
public sealed class ThemeColor
{
	/// <summary>主色（primary 语义）。</summary>
	public string Primary { get; set; } = "#2563eb";

	/// <summary>成功色（success 语义）。</summary>
	public string Success { get; set; } = "#16a34a";

	/// <summary>警告色（warning 语义）。</summary>
	public string Warning { get; set; } = "#d97706";

	/// <summary>危险色（danger 语义）。</summary>
	public string Danger { get; set; } = "#dc2626";

	/// <summary>中性色（neutral 语义）。</summary>
	public string Neutral { get; set; } = "#64748b";

	/// <summary>页面背景（语义键 background/page）。</summary>
	public string Background { get; set; } = "#ffffff";

	/// <summary>卡片/浮层表面色（语义键 surface）。</summary>
	public string Surface { get; set; } = "#f8fafc";

	/// <summary>正文文字色。</summary>
	public string Text { get; set; } = "#0f172a";

	/// <summary>次要/弱化文字色。</summary>
	public string TextMuted { get; set; } = "#64748b";

	/// <summary>边框/分隔线色。</summary>
	public string Border { get; set; } = "#e2e8f0";
}

/// <summary>字体令牌（P7）。仅字体族名与字号阶梯（纯数据）。</summary>
public sealed class ThemeTypography
{
	/// <summary>正文字体族（回退链以逗号分隔的字符串）。</summary>
	public string FontFamily { get; set; } = "-apple-system, \"Segoe UI\", Roboto, \"PingFang SC\", \"Microsoft YaHei\", sans-serif";

	/// <summary>等宽字体族（用于数字/代码）。</summary>
	public string MonoFontFamily { get; set; } = "\"SFMono-Regular\", Consolas, \"Liberation Mono\", monospace";

	/// <summary>标题字号（px）。</summary>
	public int HeadingSize { get; set; } = 22;

	/// <summary>正文字号（px）。</summary>
	public int BodySize { get; set; } = 14;

	/// <summary>小字号（px，用于标注/脚注）。</summary>
	public int CaptionSize { get; set; } = 12;

	/// <summary>行高倍数（无单位，如 1.5）。</summary>
	public double LineHeight { get; set; } = 1.5;
}

/// <summary>布局令牌（P7）。</summary>
public sealed class ThemeLayout
{
	/// <summary>栅格列数（与 <see cref="LayoutKinds.Grid"/> 对齐，默认 12）。</summary>
	public int Columns { get; set; } = 12;

	/// <summary>卡片间距（px）。</summary>
	public int Gap { get; set; } = 12;

	/// <summary>页面最大宽度（px；0 表示不限制）。</summary>
	public int MaxWidth { get; set; } = 0;

	/// <summary>页内边距（px）。</summary>
	public int PagePadding { get; set; } = 16;
}

/// <summary>边框令牌（P7）。</summary>
public sealed class ThemeBorder
{
	/// <summary>默认边框宽度（px）。</summary>
	public int Width { get; set; } = 1;

	/// <summary>边框样式（solid / dashed / none）。</summary>
	public string Style { get; set; } = "solid";
}

/// <summary>圆角令牌（P7）。不同层级的半径档位（px）。</summary>
public sealed class ThemeRadius
{
	/// <summary>小组件（标签/徽章）。</summary>
	public int Sm { get; set; } = 4;

	/// <summary>卡片/面板。</summary>
	public int Md { get; set; } = 8;

	/// <summary>大容器/弹窗。</summary>
	public int Lg { get; set; } = 12;

	/// <summary>圆形（头像/进度环）。</summary>
	public int Pill { get; set; } = 999;
}

/// <summary>阴影令牌（P7）。阴影档位（CSS box-shadow 值字符串，属样式意图数据，非标记语言）。</summary>
public sealed class ThemeShadow
{
	/// <summary>无阴影。</summary>
	public string None { get; set; } = "none";

	/// <summary>卡片默认阴影。</summary>
	public string Card { get; set; } = "0 1px 2px rgba(15,23,42,0.06), 0 1px 3px rgba(15,23,42,0.10)";

	/// <summary>浮层/弹窗阴影。</summary>
	public string Popover { get; set; } = "0 4px 12px rgba(15,23,42,0.12), 0 8px 24px rgba(15,23,42,0.16)";
}

/// <summary>图表色板（P7）。多系列取色序列 + 语义系列色。</summary>
public sealed class ThemeChartPalette
{
	/// <summary>多系列取色序列（按顺序循环分配）。</summary>
	public List<string> Series { get; set; } = new()
	{
		"#2563eb", "#16a34a", "#d97706", "#dc2626", "#7c3aed", "#0891b2", "#db2777", "#65a30d",
	};

	/// <summary>正向/增长系列色（如同比上升）。</summary>
	public string Positive { get; set; } = "#16a34a";

	/// <summary>负向/下降系列色。</summary>
	public string Negative { get; set; } = "#dc2626";

	/// <summary>中性/持平系列色。</summary>
	public string Neutral { get; set; } = "#64748b";
}

/// <summary>组件级默认风格（P7）。作用于卡片/按钮/表格/KPI 等；WidgetDsl.Style 可逐组件覆盖。</summary>
public sealed class ThemeComponent
{
	/// <summary>卡片：是否显示边框。</summary>
	public bool CardShowBorder { get; set; } = true;

	/// <summary>卡片：内边距档位（compact / normal / loose）。</summary>
	public string CardPadding { get; set; } = "normal";

	/// <summary>按钮：主按钮背景语义键（映射到 <see cref="ThemeColor"/>）。</summary>
	public string ButtonPrimaryBg { get; set; } = "primary";

	/// <summary>表格：是否显示斑马纹。</summary>
	public bool TableStriped { get; set; } = true;

	/// <summary>KPI：数值强调色语义键。</summary>
	public string KpiValueColor { get; set; } = "primary";

	/// <summary>KPI：达成率良好阈值色语义键。</summary>
	public string KpiGoodColor { get; set; } = "success";
}

/// <summary>仪表盘模板（P7）。整体外观意图。</summary>
public sealed class ThemeDashboardTemplate
{
	/// <summary>仪表盘背景语义键（映射到 <see cref="ThemeColor"/> 的 Background/Surface）。</summary>
	public string Background { get; set; } = "background";

	/// <summary>容器表面语义键（卡片承载面）。</summary>
	public string Surface { get; set; } = "surface";

	/// <summary>页头展示风格：standard / minimal / banded。</summary>
	public string HeaderStyle { get; set; } = "standard";
}

/// <summary>
/// 内置主题提供器（P7）。返回平台默认主题（浅色），供 <see cref="IThemeResolver"/> 在级联解析失败时兜底，
/// 避免任何仪表盘因找不到主题而渲染失败。
/// </summary>
public static class BuiltInThemes
{
	/// <summary>默认浅色主题的 <see cref="ThemeDsl"/>（与 <see cref="BuiltInThemeKeys.Default"/> 对应）。</summary>
	public static ThemeDsl DefaultDsl() => new();
}
