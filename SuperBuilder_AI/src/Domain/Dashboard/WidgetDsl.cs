using System.Text.Json.Serialization;

namespace SuperBuilder_AI.Models.Dashboard;

/// <summary>组件类型常量（P6）。集中定义，避免魔法字符串散落。</summary>
public static class WidgetTypes
{
	public const string Chart = "chart";
	public const string Table = "table";
	public const string Kpi = "kpi";
	public const string Filter = "filter";
	public const string Text = "text";
	public const string AiInsight = "aiInsight";

	/// <summary>受支持的组件类型集合（反序列化白名单）。</summary>
	public static IReadOnlyList<string> Supported { get; } =
		new[] { Chart, Table, Kpi, Filter, Text, AiInsight };
}

/// <summary>图表类型常量（P6）。</summary>
public static class ChartTypes
{
	public const string Line = "line";
	public const string Bar = "bar";
	public const string Column = "column";
	public const string Pie = "pie";
	public const string Area = "area";
	public const string Scatter = "scatter";

	public static IReadOnlyList<string> Supported { get; } =
		new[] { Line, Bar, Column, Pie, Area, Scatter };
}

/// <summary>
/// 组件 DSL 抽象基类（P6）。
///
/// 以 <see cref="Type"/> 作为 JSON 多态判别式，派生出 Chart / Table / KPI / Filter /
/// Text / AI Insight 六类具体组件。共性字段（标识、标题、位置、取数、风格）置于基类，
/// 类型专属配置放在各自派生类，避免出现"万能配置字典"导致校验失效。
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ChartWidgetDsl), WidgetTypes.Chart)]
[JsonDerivedType(typeof(TableWidgetDsl), WidgetTypes.Table)]
[JsonDerivedType(typeof(KpiWidgetDsl), WidgetTypes.Kpi)]
[JsonDerivedType(typeof(FilterWidgetDsl), WidgetTypes.Filter)]
[JsonDerivedType(typeof(TextWidgetDsl), WidgetTypes.Text)]
[JsonDerivedType(typeof(AiInsightWidgetDsl), WidgetTypes.AiInsight)]
public abstract class WidgetDsl
{
	/// <summary>组件类型（JSON 多态判别式），取值见 <see cref="WidgetTypes"/>。</summary>
	[JsonIgnore]
	public abstract string Type { get; }

	/// <summary>组件标识（同一页面内唯一，供筛选器与联动引用）。</summary>
	public string Id { get; set; } = string.Empty;

	/// <summary>组件标题。</summary>
	public string? Title { get; set; }

	/// <summary>展示顺序（小者优先；与 <see cref="Position"/> 同时存在时以 Position 为准）。</summary>
	public int Order { get; set; }

	/// <summary>栅格位置；为 null 时由渲染器按 <see cref="Order"/> 自动排布。</summary>
	public GridPositionDsl? Position { get; set; }

	/// <summary>取数定义。文本类组件（Text）可为 null。</summary>
	public WidgetQueryDsl? Query { get; set; }

	/// <summary>风格配置（P7 Theme 会在此之上叠加主题默认值）。</summary>
	public StyleDsl? Style { get; set; }
}

/// <summary>图表组件（P6）。</summary>
public sealed class ChartWidgetDsl : WidgetDsl
{
	/// <inheritdoc />
	[JsonIgnore]
	public override string Type => WidgetTypes.Chart;

	/// <summary>图表类型，取值见 <see cref="ChartTypes"/>。</summary>
	public string ChartType { get; set; } = ChartTypes.Column;

	/// <summary>分类轴 / X 轴字段（对应结果集列名或语义名）。</summary>
	public string? CategoryField { get; set; }

	/// <summary>数值轴 / Y 轴字段集合（支持多系列）。</summary>
	public List<string> ValueFields { get; set; } = new();

	/// <summary>分系列字段（用于拆分多系列，可为空）。</summary>
	public string? SeriesField { get; set; }

	/// <summary>是否堆叠（柱状/面积图有效）。</summary>
	public bool Stacked { get; set; }

	/// <summary>是否显示图例。</summary>
	public bool ShowLegend { get; set; } = true;

	/// <summary>是否显示数据标签。</summary>
	public bool ShowDataLabel { get; set; }
}

/// <summary>表格组件（P6）。</summary>
public sealed class TableWidgetDsl : WidgetDsl
{
	/// <inheritdoc />
	[JsonIgnore]
	public override string Type => WidgetTypes.Table;

	/// <summary>列配置；为空时展示查询结果全部列。</summary>
	public List<TableColumnDsl> Columns { get; set; } = new();

	/// <summary>每页行数；为 null 时由渲染器决定。</summary>
	public int? PageSize { get; set; }

	/// <summary>是否显示合计行。</summary>
	public bool ShowTotal { get; set; }

	/// <summary>是否启用斑马纹。</summary>
	public bool Striped { get; set; } = true;
}

/// <summary>表格列配置（P6）。</summary>
public sealed class TableColumnDsl
{
	/// <summary>结果集列名或语义名。</summary>
	public string Field { get; set; } = string.Empty;

	/// <summary>列头显示名（多语言场景可由 <c>SemanticLabel</c> 覆盖）。</summary>
	public string? Header { get; set; }

	/// <summary>对齐方式：left / center / right。</summary>
	public string? Align { get; set; }

	/// <summary>数值格式化串（如 "N2"、"0.0%"）。</summary>
	public string? Format { get; set; }

	/// <summary>列宽（像素或权重，由渲染器解释）。</summary>
	public int? Width { get; set; }
}

/// <summary>KPI 指标卡组件（P6）。</summary>
public sealed class KpiWidgetDsl : WidgetDsl
{
	/// <inheritdoc />
	[JsonIgnore]
	public override string Type => WidgetTypes.Kpi;

	/// <summary>指标字段（对应结果集首行取值）。</summary>
	public string ValueField { get; set; } = string.Empty;

	/// <summary>对比字段（用于计算同比/环比，可为空）。</summary>
	public string? CompareField { get; set; }

	/// <summary>数值格式化串。</summary>
	public string? Format { get; set; }

	/// <summary>目标值；提供时渲染器可计算达成率。</summary>
	public decimal? Target { get; set; }

	/// <summary>趋势方向语义：up-good / up-bad / none。</summary>
	public string? TrendSemantics { get; set; }
}

/// <summary>筛选器组件（P6）。其值会下推到关联组件的查询条件。</summary>
public sealed class FilterWidgetDsl : WidgetDsl
{
	/// <inheritdoc />
	[JsonIgnore]
	public override string Type => WidgetTypes.Filter;

	/// <summary>绑定字段（语义名）。</summary>
	public string Field { get; set; } = string.Empty;

	/// <summary>控件形态：select / date-range / number-range / text。</summary>
	public string Control { get; set; } = "select";

	/// <summary>默认值（JSON 标量或数组，由控件形态决定）。</summary>
	public string? DefaultValue { get; set; }

	/// <summary>候选项（静态枚举；为空时由渲染器按字段取值动态生成）。</summary>
	public List<string> Options { get; set; } = new();

	/// <summary>是否多选。</summary>
	public bool Multiple { get; set; }

	/// <summary>作用的组件 Id 集合；为空表示作用于本页全部组件。</summary>
	public List<string> Targets { get; set; } = new();
}

/// <summary>文本组件（P6）。仅承载结构化文本，<strong>不允许裸 HTML</strong>。</summary>
public sealed class TextWidgetDsl : WidgetDsl
{
	/// <inheritdoc />
	[JsonIgnore]
	public override string Type => WidgetTypes.Text;

	/// <summary>文本内容。</summary>
	public string Content { get; set; } = string.Empty;

	/// <summary>是否按 Markdown 解释（渲染器负责转义，杜绝 XSS）。</summary>
	public bool Markdown { get; set; } = true;

	/// <summary>字号级别：title / subtitle / body / caption。</summary>
	public string? Level { get; set; }
}

/// <summary>AI 洞察组件（P6）。运行时调用 BI 分析链路生成结论文本。</summary>
public sealed class AiInsightWidgetDsl : WidgetDsl
{
	/// <inheritdoc />
	[JsonIgnore]
	public override string Type => WidgetTypes.AiInsight;

	/// <summary>洞察提示语（补充 <see cref="WidgetDsl.Query"/> 的取数意图）。</summary>
	public string? Prompt { get; set; }

	/// <summary>最多生成的洞察条数。</summary>
	public int MaxInsights { get; set; } = 3;

	/// <summary>洞察视角：trend / anomaly / contribution / comparison。</summary>
	public string? Perspective { get; set; }
}

/// <summary>
/// 风格 DSL（P6）。只表达"风格意图"，不承载具体像素级样式与任何标记语言。
/// </summary>
public sealed class StyleDsl
{
	/// <summary>语义色板键：primary / success / warning / danger / neutral。</summary>
	public string? Palette { get; set; }

	/// <summary>是否显示边框。</summary>
	public bool? ShowBorder { get; set; }

	/// <summary>内边距档位：compact / normal / loose。</summary>
	public string? Padding { get; set; }

	/// <summary>背景语义键（P7 主题会映射到具体色值）。</summary>
	public string? Background { get; set; }
}
