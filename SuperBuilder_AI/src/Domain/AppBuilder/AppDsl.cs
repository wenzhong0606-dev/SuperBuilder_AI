namespace SuperBuilder_AI.Models.AppBuilder;

/// <summary>应用组件类型常量（P8）。集中定义，避免魔法字符串散落。</summary>
public static class AppComponentTypes
{
	public const string Chart = "chart";
	public const string Table = "table";
	public const string Kpi = "kpi";
	public const string Text = "text";
	public const string Filter = "filter";
	public const string Form = "form";

	/// <summary>受支持的组件类型集合（反序列化白名单）。</summary>
	public static IReadOnlyList<string> Supported { get; } =
		new[] { Chart, Table, Kpi, Text, Filter, Form };
}

/// <summary>聚合方式常量（P8）。</summary>
public static class AppAggregateTypes
{
	public const string Sum = "sum";
	public const string Avg = "avg";
	public const string Count = "count";
	public const string Min = "min";
	public const string Max = "max";

	public static IReadOnlyList<string> Supported { get; } =
		new[] { Sum, Avg, Count, Min, Max };
}

/// <summary>筛选操作符常量（P8）。</summary>
public static class AppFilterOperators
{
	public const string Eq = "eq";
	public const string Neq = "neq";
	public const string Gt = "gt";
	public const string Lt = "lt";
	public const string In = "in";
	public const string Like = "like";

	public static IReadOnlyList<string> Supported { get; } =
		new[] { Eq, Neq, Gt, Lt, In, Like };
}

/// <summary>应用布局方式（P8）。</summary>
public static class AppLayoutKinds
{
	/// <summary>12 栅格自由布局（默认）。</summary>
	public const string Grid = "grid";

	/// <summary>纵向流式布局。</summary>
	public const string Flow = "flow";

	public static IReadOnlyList<string> Supported { get; } = new[] { Grid, Flow };
}

/// <summary>
/// 应用 DSL 根对象（P8 AI App Builder）。
///
/// 一个 <see cref="AppDsl"/> 即一份完整的、可序列化的应用定义：
/// 描述<strong>有哪些页面</strong>、每页<strong>放哪些组件</strong>、组件<strong>取什么数</strong>、
/// <strong>什么风格</strong>（P7 主题语义键）。
///
/// <para>设计红线：底层不存裸 HTML。所有呈现意图由结构化字段表达，由后续渲染器解释；
/// 同一份 DSL 可换主题（P7）、可被 AI 生成与修改（P8）。</para>
/// </summary>
public sealed class AppDsl
{
	/// <summary>DSL 版本号，默认 <see cref="AppDslVersions.Current"/>。</summary>
	public string Version { get; set; } = AppDslVersions.Current;

	/// <summary>业务编码（同租户内唯一，便于 API 定位与 AI 引用）。</summary>
	public string? Code { get; set; }

	/// <summary>应用名称。</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>应用描述。</summary>
	public string? Description { get; set; }

	/// <summary>主题键（P7 Theme 消费；P8.1 仅透传不解释）。</summary>
	public string? ThemeKey { get; set; }

	/// <summary>页面集合（至少一个页面）。</summary>
	public List<PagePlan> Pages { get; set; } = new();
}

/// <summary>页面 DSL（P8）。一个应用可含多个页面，每个页面承载一组组件。</summary>
public sealed class PagePlan
{
	/// <summary>页面标识（同一应用内唯一）。</summary>
	public string Id { get; set; } = string.Empty;

	/// <summary>页面名称。</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>展示顺序（小者优先）。</summary>
	public int Order { get; set; }

	/// <summary>页面布局配置；为 null 时采用 <see cref="AppLayoutKinds.Grid"/> 默认值。</summary>
	public AppLayoutDsl? Layout { get; set; }

	/// <summary>页面内组件集合。</summary>
	public List<ComponentPlan> Components { get; set; } = new();
}

/// <summary>布局 DSL（P8）。描述页面的栅格系统，渲染器据此换算组件位置。</summary>
public sealed class AppLayoutDsl
{
	/// <summary>布局方式，取值见 <see cref="AppLayoutKinds"/>。</summary>
	public string Kind { get; set; } = AppLayoutKinds.Grid;

	/// <summary>栅格列数（Grid 布局下有效，默认 12）。</summary>
	public int Columns { get; set; } = 12;

	/// <summary>单行高度（像素）。</summary>
	public int RowHeight { get; set; } = 64;

	/// <summary>组件间距（像素）。</summary>
	public int Gap { get; set; } = 12;
}

/// <summary>组件栅格位置（P8）。</summary>
public sealed class AppGridPosition
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

/// <summary>取数绑定（P8 / M7-11）。以业务语义名表达"取什么数"，由运行时语义解析映射到实体/字段。</summary>
public sealed class AppDataSourceBinding
{
	/// <summary>业务实体语义名（如 sales_order）。</summary>
	public string? Entity { get; set; }

	/// <summary>
	/// M7-11：运行时硬约束的数据源 Id（来自 Ask 查询快照解析，非语义名）。
	/// 应用运行必须在该数据源范围内执行；单源约束在选表前生效。为 null 时由实体语义回退解析。
	/// </summary>
	public long? DataSourceId { get; set; }

	/// <summary>指标集合（字段 + 聚合方式）。</summary>
	public List<AppMetricBinding> Metrics { get; set; } = new();

	/// <summary>维度字段集合（用于分组/分类轴）。</summary>
	public List<string> Dimensions { get; set; } = new();

	/// <summary>筛选条件集合。</summary>
	public List<AppFilterBinding> Filters { get; set; } = new();

	/// <summary>
	/// M7-11：排序集合。首批仅支持单字段排序；多字段一律在导出/运行时阶段以 422 拒绝。
	/// </summary>
	public List<AppSortBinding> Sort { get; set; } = new();

	/// <summary>返回行数上限；为 null 表示不限制。</summary>
	public int? Limit { get; set; }
}

/// <summary>M7-11 排序绑定（确定性查询分支使用，替代 QueryIntent 的单值 OrderBy）。</summary>
public sealed class AppSortBinding
{
	/// <summary>排序字段（语义名）。</summary>
	public string Field { get; set; } = string.Empty;

	/// <summary>排序方向：ASC / DESC。</summary>
	public string Direction { get; set; } = "ASC";
}

/// <summary>指标绑定（P8）。</summary>
public sealed class AppMetricBinding
{
	/// <summary>指标字段（语义名）。</summary>
	public string Field { get; set; } = string.Empty;

	/// <summary>聚合方式，取值见 <see cref="AppAggregateTypes"/>。</summary>
	public string Aggregation { get; set; } = AppAggregateTypes.Sum;
}

/// <summary>筛选绑定（P8 / M7-11）。</summary>
public sealed class AppFilterBinding
{
	/// <summary>筛选字段（语义名）。</summary>
	public string Field { get; set; } = string.Empty;

	/// <summary>操作符，取值见 <see cref="AppFilterOperators"/>。</summary>
	public string Operator { get; set; } = AppFilterOperators.Eq;

	/// <summary>单值比较值（eq/neq/gt/lt/like 使用；可为空表示占位/由运行时注入）。</summary>
	public string? Value { get; set; }

	/// <summary>
	/// M7-11：类型明确的多值集合（<c>in</c> 操作符使用）。
	/// 数组中每个元素已是独立值，<strong>禁止按逗号拆分单个字符串</strong>；
	/// 含逗号的字符串（如 "上海,浦东"）应作为数组的一项原样保留，由运行时按列类型参数化。
	/// </summary>
	public List<string> Values { get; set; } = new();
}

/// <summary>
/// 组件风格（P8）。只表达"风格意图"的语义键，不承载具体像素级样式与任何标记语言；
/// 与 P7 主题令牌（Palette/Background/ShowBorder/Padding）保持一致，渲染时由主题解析为具体色值。
/// </summary>
public sealed class AppComponentStyle
{
	/// <summary>语义色板键：primary / success / warning / danger / neutral。</summary>
	public string? Palette { get; set; }

	/// <summary>背景语义键（P7 主题会映射到具体色值）。</summary>
	public string? Background { get; set; }

	/// <summary>是否显示边框。</summary>
	public bool? ShowBorder { get; set; }

	/// <summary>内边距档位：compact / normal / loose。</summary>
	public string? Padding { get; set; }
}

/// <summary>
/// 组件 DSL（P8）。单个强类型组件：核心取数（<see cref="Binding"/>）与风格（<see cref="Style"/>）
/// 均为强类型，仅类型专属呈现参数（如 chartType、categoryField）以 <see cref="Properties"/> 承载，
/// 避免退化为"万能配置字典"导致校验失效。
/// </summary>
public sealed class ComponentPlan
{
	/// <summary>组件类型，取值见 <see cref="AppComponentTypes"/>。</summary>
	public string Type { get; set; } = AppComponentTypes.Chart;

	/// <summary>组件标识（同一页面内唯一，供联动引用）。</summary>
	public string Id { get; set; } = string.Empty;

	/// <summary>组件标题。</summary>
	public string? Title { get; set; }

	/// <summary>展示顺序（小者优先；与 <see cref="Position"/> 同时存在时以 Position 为准）。</summary>
	public int Order { get; set; }

	/// <summary>栅格位置；为 null 时由渲染器按 <see cref="Order"/> 自动排布。</summary>
	public AppGridPosition? Position { get; set; }

	/// <summary>取数定义。文本类组件（Text）可为 null。</summary>
	public AppDataSourceBinding? Binding { get; set; }

	/// <summary>类型专属呈现参数（如 chartType=bar、categoryField=region、pageSize=20）。</summary>
	public Dictionary<string, string> Properties { get; set; } = new();

	/// <summary>风格配置（P7 Theme 会在此之上叠加主题默认值）。</summary>
	public AppComponentStyle? Style { get; set; }
}
