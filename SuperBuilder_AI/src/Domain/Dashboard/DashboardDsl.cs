namespace SuperBuilder_AI.Models.Dashboard;

/// <summary>
/// 仪表盘 DSL 版本号常量（P6 Low-code BI Engine）。
///
/// 序列化时写入 <see cref="DashboardDsl.Version"/>，反序列化时用于选择兼容读取策略——
/// 保证 DSL 演进时旧数据仍可被解析（P6.2 的校验器据此分流）。
/// </summary>
public static class DslVersions
{
	/// <summary>首个正式版本。</summary>
	public const string V1 = "1.0";

	/// <summary>当前版本（新增 DSL 时以此为默认）。</summary>
	public const string Current = V1;

	/// <summary>受支持的版本集合（反序列化白名单）。</summary>
	public static IReadOnlyList<string> Supported { get; } = new[] { V1 };
}

/// <summary>仪表盘布局方式。</summary>
public static class LayoutKinds
{
	/// <summary>12 栅格自由布局（默认）。</summary>
	public const string Grid = "grid";

	/// <summary>纵向流式布局。</summary>
	public const string Flow = "flow";
}

/// <summary>
/// 仪表盘 DSL 根对象（P6）。
///
/// 一个 <see cref="DashboardDsl"/> 即一份完整的、可序列化的仪表盘定义：
/// 描述<strong>取什么数</strong>（Query）、<strong>怎么画</strong>（Visualization）、
/// <strong>放哪里</strong>（Layout / Position）、<strong>什么风格</strong>（Style）。
///
/// <para>
/// 设计红线：<strong>底层不存裸 HTML</strong>。所有呈现意图都由结构化字段表达，
/// 由 <c>LowcodeRenderer</c>（P6.3）在运行时解释。这样同一份 DSL 可换主题（P7）、
/// 可被 AI 生成与修改（P8），且永远不会把样式硬编码进数据里。
/// </para>
/// </summary>
public sealed class DashboardDsl
{
	/// <summary>DSL 版本号，默认 <see cref="DslVersions.Current"/>。</summary>
	public string Version { get; set; } = DslVersions.Current;

	/// <summary>业务编码（同租户内唯一，便于 API 定位与 AI 引用）。</summary>
	public string? Code { get; set; }

	/// <summary>仪表盘标题。</summary>
	public string Title { get; set; } = string.Empty;

	/// <summary>仪表盘描述。</summary>
	public string? Description { get; set; }

	/// <summary>主题键（P7 Theme 消费；P6 阶段仅透传不解释）。</summary>
	public string? ThemeKey { get; set; }

	/// <summary>页面集合（至少一个页面）。</summary>
	public List<PageDsl> Pages { get; set; } = new();

	/// <summary>仪表盘级筛选器（作用于全部页面，P6.3 渲染时下推到各 Widget 查询）。</summary>
	public List<FilterDsl> GlobalFilters { get; set; } = new();
}

/// <summary>
/// 页面 DSL（P6）。一个仪表盘可含多个页面，每个页面承载一组组件。
/// </summary>
public sealed class PageDsl
{
	/// <summary>页面标识（同一仪表盘内唯一）。</summary>
	public string Id { get; set; } = string.Empty;

	/// <summary>页面名称。</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>展示顺序（小者优先）。</summary>
	public int Order { get; set; }

	/// <summary>页面布局配置；为 null 时采用 <see cref="LayoutKinds.Grid"/> 默认值。</summary>
	public LayoutDsl? Layout { get; set; }

	/// <summary>页面内组件集合。</summary>
	public List<WidgetDsl> Widgets { get; set; } = new();
}

/// <summary>
/// 布局 DSL（P6）。描述页面的栅格系统，渲染器据此换算组件位置。
/// </summary>
public sealed class LayoutDsl
{
	/// <summary>布局方式，取值见 <see cref="LayoutKinds"/>。</summary>
	public string Kind { get; set; } = LayoutKinds.Grid;

	/// <summary>栅格列数（Grid 布局下有效，默认 12）。</summary>
	public int Columns { get; set; } = 12;

	/// <summary>单行高度（像素）。</summary>
	public int RowHeight { get; set; } = 64;

	/// <summary>组件间距（像素）。</summary>
	public int Gap { get; set; } = 12;
}

/// <summary>
/// 组件栅格位置（P6）。
/// </summary>
public sealed class GridPositionDsl
{
	/// <summary>起始列（0 基）。</summary>
	public int X { get; set; }

	/// <summary>起始行（0 基）。</summary>
	public int Y { get; set; }

	/// <summary>横向占用的列数。</summary>
	public int W { get; set; } = 4;

	/// <summary>纵向占用的行数。</summary>
	public int H { get; set; } = 3;
}
