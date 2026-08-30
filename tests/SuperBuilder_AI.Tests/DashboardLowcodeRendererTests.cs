using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.BI.Dashboard;
using SuperBuilder_AI.Models.Dashboard;
using SuperBuilder_AI.Models.Dashboard.Rendering;
using SuperBuilder_AI.Models.Organization;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// P6.3 LowcodeRenderer 渲染引擎单测（使用 FakeResolver，不触碰任何查询链路 / 不调 LLM）。
/// 覆盖：全组件类型渲染、全局筛选器下推、文本二次净化、AI 洞察占位、无取数组件。
/// </summary>
public class DashboardLowcodeRendererTests
{
	/// <summary>返回固定两行结果（region/amount）的假解析器。</summary>
	private sealed class FakeResolver : IWidgetDataResolver
	{
		public Task<WidgetDataResult> ResolveAsync(
			WidgetQueryDsl? query,
			PlatformContext context,
			IReadOnlyList<FilterDsl> effectiveFilters,
			System.Threading.CancellationToken cancellationToken = default)
		{
			var r = new WidgetDataResult
			{
				Resolved = true,
				Decision = "Proceed",
			};
			r.Columns.Add("region");
			r.Columns.Add("amount");
			r.Rows.Add(new Dictionary<string, object?> { ["region"] = "East", ["amount"] = 100 });
			r.Rows.Add(new Dictionary<string, object?> { ["region"] = "West", ["amount"] = 200 });
			return Task.FromResult(r);
		}
	}

	private static DashboardDsl BuildSampleDsl()
	{
		return new DashboardDsl
		{
			Code = "sales",
			Title = "销售总览",
			GlobalFilters =
			{
				new FilterDsl { Field = "region", Operator = "=", Value = "East" },
			},
			Pages =
			{
				new PageDsl
				{
					Id = "p1",
					Name = "概览",
					Widgets =
					{
						new ChartWidgetDsl
						{
							Id = "c1",
							Title = "区域销售额",
							ChartType = "column",
							Query = new WidgetQueryDsl
							{
								Metrics = { new MetricDsl { Field = "amount", Aggregation = "SUM" } },
								Dimensions = { "region" },
								Filters = { new FilterDsl { Field = "category", Operator = "=", Value = "A" } },
							},
						},
						new TableWidgetDsl
						{
							Id = "t1",
							Title = "明细",
							Query = new WidgetQueryDsl
							{
								Metrics = { new MetricDsl { Field = "amount", Aggregation = "SUM" } },
								Dimensions = { "region" },
							},
						},
						new KpiWidgetDsl
						{
							Id = "k1",
							Title = "总销售额",
							ValueField = "amount",
							Query = new WidgetQueryDsl
							{
								Metrics = { new MetricDsl { Field = "amount", Aggregation = "SUM" } },
							},
						},
						new TextWidgetDsl
						{
							Id = "tx1",
							Title = "说明",
							Content = "<script>alert(1)</script>Hello",
							Markdown = true,
						},
						new FilterWidgetDsl
						{
							Id = "f1",
							Title = "区域筛选",
							Field = "region",
							Control = "select",
						},
						new AiInsightWidgetDsl
						{
							Id = "a1",
							Title = "洞察",
							Prompt = "分析趋势",
							MaxInsights = 2,
							Perspective = "trend",
						},
					},
				},
			},
		};
	}

	[Fact]
	public async Task Render_AllWidgetTypes_ProducesStructuredModel()
	{
		var dsl = BuildSampleDsl();
		var renderer = new SuperBuilder_AI.Services.BI.Dashboard.DashboardLowcodeRenderer(new FakeResolver());

		var model = await renderer.RenderAsync(dsl, PlatformContext.System);

		Assert.Equal("销售总览", model.Title);
		Assert.Single(model.Pages);
		Assert.Equal(6, model.Pages[0].Widgets.Count);
	}

	[Fact]
	public async Task Render_DataWidgets_HaveResolvedRows()
	{
		var dsl = BuildSampleDsl();
		var renderer = new SuperBuilder_AI.Services.BI.Dashboard.DashboardLowcodeRenderer(new FakeResolver());

		var model = await renderer.RenderAsync(dsl, PlatformContext.System);
		var chart = model.Pages[0].Widgets.Single(w => w.Id == "c1");

		Assert.NotNull(chart.Data);
		Assert.True(chart.Data!.Resolved);
		Assert.Equal("Proceed", chart.Data.Decision);
		Assert.Equal(2, chart.Data.RowCount);
		Assert.Contains("region", chart.Data.Columns);
	}

	[Fact]
	public async Task Render_GlobalAndWidgetFilters_ArePushedDown()
	{
		var dsl = BuildSampleDsl();
		var renderer = new SuperBuilder_AI.Services.BI.Dashboard.DashboardLowcodeRenderer(new FakeResolver());

		var model = await renderer.RenderAsync(dsl, PlatformContext.System);
		var chart = model.Pages[0].Widgets.Single(w => w.Id == "c1");

		// 全局 1 条 + 组件 1 条 = 2 条。
		Assert.Equal(2, chart.EffectiveFilters.Count);
		Assert.Contains(chart.EffectiveFilters, f => f.Source == "global" && f.Field == "region");
		Assert.Contains(chart.EffectiveFilters, f => f.Source == "widget" && f.Field == "category");
	}

	[Fact]
	public async Task Render_TextWidget_SanitizedNoteSet()
	{
		var dsl = BuildSampleDsl();
		var renderer = new SuperBuilder_AI.Services.BI.Dashboard.DashboardLowcodeRenderer(new FakeResolver());

		var model = await renderer.RenderAsync(dsl, PlatformContext.System);
		var text = model.Pages[0].Widgets.Single(w => w.Id == "tx1");

		// 文本含 <script>：渲染器二次净化，给出说明（DSL 校验阶段本应已拦截，此处为防御）。
		Assert.Equal("text content sanitized (HTML stripped)", text.Note);
	}

	[Fact]
	public async Task Render_FilterAndTextWidgets_HaveNoData()
	{
		var dsl = BuildSampleDsl();
		var renderer = new SuperBuilder_AI.Services.BI.Dashboard.DashboardLowcodeRenderer(new FakeResolver());

		var model = await renderer.RenderAsync(dsl, PlatformContext.System);
		var filter = model.Pages[0].Widgets.Single(w => w.Id == "f1");
		var text = model.Pages[0].Widgets.Single(w => w.Id == "tx1");

		Assert.Null(filter.Data);
		Assert.Null(text.Data);
	}

	[Fact]
	public async Task Render_AiInsight_Widget_CarriesPlaceholder()
	{
		var dsl = BuildSampleDsl();
		var renderer = new SuperBuilder_AI.Services.BI.Dashboard.DashboardLowcodeRenderer(new FakeResolver());

		var model = await renderer.RenderAsync(dsl, PlatformContext.System);
		var ai = model.Pages[0].Widgets.Single(w => w.Id == "a1");

		Assert.NotNull(ai.AiInsight);
		Assert.Equal("分析趋势", ai.AiInsight!.Prompt);
		Assert.Equal(2, ai.AiInsight.MaxInsights);
		Assert.Empty(ai.AiInsight.Insights);
	}
}
