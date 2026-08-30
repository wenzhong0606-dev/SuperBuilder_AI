using SuperBuilder_AI.Models.Dashboard;

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
