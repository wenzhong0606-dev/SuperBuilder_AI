namespace SuperBuilder_AI.Models.Dashboard;

/// <summary>聚合方式常量（P6）。与 QueryIntent 的 Aggregation 取值保持一致。</summary>
public static class AggregateTypes
{
	public const string None = "NONE";
	public const string Sum = "SUM";
	public const string Count = "COUNT";
	public const string Avg = "AVG";
	public const string Max = "MAX";
	public const string Min = "MIN";

	public static IReadOnlyList<string> Supported { get; } =
		new[] { None, Sum, Count, Avg, Max, Min };
}

/// <summary>筛选操作符常量（P6）。</summary>
public static class FilterOperators
{
	public const string Equal = "=";
	public const string NotEqual = "!=";
	public const string GreaterThan = ">";
	public const string LessThan = "<";
	public const string GreaterOrEqual = ">=";
	public const string LessOrEqual = "<=";
	public const string Like = "LIKE";
	public const string In = "IN";
	public const string Between = "BETWEEN";

	public static IReadOnlyList<string> Supported { get; } =
		new[] { Equal, NotEqual, GreaterThan, LessThan, GreaterOrEqual, LessOrEqual, Like, In, Between };
}

/// <summary>
/// 组件取数定义（P6）。
///
/// 支持两种取数方式，渲染器（P6.3）按优先级择一：
/// <list type="number">
/// <item><strong>显式语义查询</strong>（<see cref="Metrics"/> / <see cref="Dimensions"/> 等有值）：
///       确定性最强，直接构造查询，不经过 LLM。</item>
/// <item><strong>自然语言问句</strong>（<see cref="Question"/>）：交由既有 BI 意图理解链路解析，
///       供 AI 生成 DSL（P8）与人工快速搭建使用。</item>
/// </list>
///
/// 两者都为空时，该组件视为无需取数（如静态文本）。
/// </summary>
public sealed class WidgetQueryDsl
{
	/// <summary>自然语言问句（显式语义字段为空时由渲染器使用）。</summary>
	public string? Question { get; set; }

	/// <summary>数据源编码（可为空，为空时由渲染器按租户默认数据源解析）。</summary>
	public string? DataSourceCode { get; set; }

	/// <summary>业务实体编码（P3 BusinessEntity，提供时优先按实体语义取数）。</summary>
	public string? EntityCode { get; set; }

	/// <summary>指标定义集合。</summary>
	public List<MetricDsl> Metrics { get; set; } = new();

	/// <summary>维度字段集合（语义名）。</summary>
	public List<string> Dimensions { get; set; } = new();

	/// <summary>筛选条件集合。</summary>
	public List<FilterDsl> Filters { get; set; } = new();

	/// <summary>排序定义集合。</summary>
	public List<SortDsl> Sorts { get; set; } = new();

	/// <summary>返回行数上限。</summary>
	public int? Limit { get; set; }

	/// <summary>
	/// 是否需要在渲染时重新解析问句。
	/// 默认 true：保证数据实时；设为 false 可使用缓存结果（由渲染器决定）。
	/// </summary>
	public bool RefreshOnRender { get; set; } = true;
}

/// <summary>指标定义（P6）。</summary>
public sealed class MetricDsl
{
	/// <summary>字段语义名。</summary>
	public string Field { get; set; } = string.Empty;

	/// <summary>聚合方式，取值见 <see cref="AggregateTypes"/>。</summary>
	public string Aggregation { get; set; } = AggregateTypes.Sum;

	/// <summary>结果别名（为空时由渲染器生成）。</summary>
	public string? Alias { get; set; }
}

/// <summary>筛选条件（P6）。仪表盘级与组件级共用。</summary>
public sealed class FilterDsl
{
	/// <summary>字段语义名。</summary>
	public string Field { get; set; } = string.Empty;

	/// <summary>操作符，取值见 <see cref="FilterOperators"/>。</summary>
	public string Operator { get; set; } = FilterOperators.Equal;

	/// <summary>比较值（标量或数组序列化后的字符串；BETWEEN / IN 由渲染器拆分）。</summary>
	public string? Value { get; set; }

	/// <summary>
	/// 是否允许为空值覆盖。
	/// 为 true 时运行时若用户未选择，则忽略该条件（而非按空值过滤导致查不到数据）。
	/// </summary>
	public bool Optional { get; set; } = true;
}

/// <summary>排序定义（P6）。</summary>
public sealed class SortDsl
{
	/// <summary>字段语义名。</summary>
	public string Field { get; set; } = string.Empty;

	/// <summary>排序方向：ASC / DESC。</summary>
	public string Direction { get; set; } = "ASC";
}
