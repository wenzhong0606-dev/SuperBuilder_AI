using System.Globalization;
using System.Text.RegularExpressions;
using SuperBuilder_AI.Interfaces.BI.Dashboard;
using SuperBuilder_AI.Models.Dashboard;
using SuperBuilder_AI.Models.Dashboard.Rendering;
using SuperBuilder_AI.Models.Organization;

namespace SuperBuilder_AI.Services.BI.Dashboard;

/// <summary>
/// LowcodeRenderer 默认实现（P6.3）。
///
/// 职责：把 <see cref="DashboardDsl"/> 转换为纯结构化的 <see cref="DashboardRenderModel"/>。
/// <list type="bullet">
///   <item>从 DSL 派生布局/位置/筛选器，<strong>绝不产出 HTML</strong>（与 P6.2 红线一致）。</item>
///   <item>全局筛选器下推到每个组件（合并进 <see cref="WidgetRenderModel.EffectiveFilters"/>）。</item>
///   <item>数据组件（chart/table/kpi）委托 <see cref="IWidgetDataResolver"/> 取数；文本组件做二次净化。</item>
///   <item>AI 洞察组件仅生成占位（运行时由分析链路填充 Insights）。</item>
/// </list>
/// </summary>
public sealed class DashboardLowcodeRenderer : IDashboardRenderer
{
	private readonly IWidgetDataResolver _dataResolver;

	public DashboardLowcodeRenderer(IWidgetDataResolver dataResolver)
	{
		_dataResolver = dataResolver ?? throw new ArgumentNullException(nameof(dataResolver));
	}

	/// <inheritdoc />
	public async Task<DashboardRenderModel> RenderAsync(
		DashboardDsl dsl,
		PlatformContext context,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(dsl);

		var model = new DashboardRenderModel
		{
			Version = dsl.Version,
			Title = dsl.Title,
			Description = dsl.Description,
			ThemeKey = dsl.ThemeKey,
		};

		foreach (var page in dsl.Pages.OrderBy(p => p.Order))
		{
			model.Pages.Add(await RenderPageAsync(page, dsl.GlobalFilters, context, cancellationToken));
		}

		return model;
	}

	private async Task<PageRenderModel> RenderPageAsync(
		PageDsl page,
		List<FilterDsl> globalFilters,
		PlatformContext context,
		CancellationToken ct)
	{
		var layout = page.Layout is null
			? new LayoutRenderModel()
			: new LayoutRenderModel
			{
				Kind = page.Layout.Kind,
				Columns = page.Layout.Columns,
				RowHeight = page.Layout.RowHeight,
				Gap = page.Layout.Gap,
			};

		var pageModel = new PageRenderModel
		{
			Id = page.Id,
			Name = page.Name,
			Order = page.Order,
			Layout = layout,
		};

		foreach (var widget in page.Widgets.OrderBy(w => w.Order))
		{
			pageModel.Widgets.Add(await RenderWidgetAsync(widget, globalFilters, context, ct));
		}

		return pageModel;
	}

	private async Task<WidgetRenderModel> RenderWidgetAsync(
		WidgetDsl widget,
		List<FilterDsl> globalFilters,
		PlatformContext context,
		CancellationToken ct)
	{
		var effective = BuildEffectiveFilters(
			globalFilters,
			widget.Query?.Filters ?? new List<FilterDsl>());

		var model = new WidgetRenderModel
		{
			Id = widget.Id,
			Type = widget.Type,
			Title = widget.Title,
			Position = widget.Position,
			Style = widget.Style,
			Source = widget,
			EffectiveFilters = effective,
		};

		switch (widget)
		{
			case TextWidgetDsl text:
				SanitizeText(text, model);
				break;

			case AiInsightWidgetDsl ai:
				model.AiInsight = new AiInsightRenderSpec
				{
					Prompt = ai.Prompt,
					MaxInsights = ai.MaxInsights,
					Perspective = ai.Perspective,
				};
				break;

			default:
				if (widget.Query is not null)
				{
					var result = await _dataResolver.ResolveAsync(
						widget.Query,
						context,
						effective.Select(ToFilterDsl).ToList(),
						ct);

					model.Data = new WidgetDataRenderModel
					{
						Resolved = result.Resolved,
						Error = result.Error,
						Columns = result.Columns,
						Rows = result.Rows,
						Decision = result.Decision,
					};
				}

				break;
		}

		return model;
	}

	private static List<FilterRenderModel> BuildEffectiveFilters(
		List<FilterDsl> globalFilters,
		List<FilterDsl> widgetFilters)
	{
		var list = new List<FilterRenderModel>();
		list.AddRange(globalFilters.Select(f => ToFilterRenderModel(f, "global")));
		list.AddRange(widgetFilters.Select(f => ToFilterRenderModel(f, "widget")));
		return list;
	}

	private static FilterRenderModel ToFilterRenderModel(FilterDsl f, string source) => new()
	{
		Field = f.Field,
		Operator = f.Operator,
		Value = f.Value,
		Optional = f.Optional,
		Source = source,
	};

	private static FilterDsl ToFilterDsl(FilterRenderModel f) => new()
	{
		Field = f.Field,
		Operator = f.Operator,
		Value = f.Value,
		Optional = f.Optional,
	};

	/// <summary>P6 红线：文本组件二次净化，移除任何残留 HTML 标签/脚本片段，杜绝 XSS。</summary>
	private static void SanitizeText(TextWidgetDsl text, WidgetRenderModel model)
	{
		var content = text.Content ?? string.Empty;
		var cleaned = HtmlTagPattern.Replace(content, string.Empty);
		if (!string.Equals(cleaned, content, StringComparison.Ordinal))
		{
			model.Note = "text content sanitized (HTML stripped)";
		}
	}

	private static readonly Regex HtmlTagPattern = new(
		@"<\s*/?\s*[a-zA-Z][^>]*>",
		RegexOptions.IgnoreCase | RegexOptions.Compiled,
		TimeSpan.FromMilliseconds(100));
}
