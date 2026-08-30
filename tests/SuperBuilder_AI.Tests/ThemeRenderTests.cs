using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.BI.Dashboard;
using SuperBuilder_AI.Models.Dashboard;
using SuperBuilder_AI.Models.Dashboard.Rendering;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Models.Theme;
using SuperBuilder_AI.Services.BI.Dashboard;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// P7.3 主题渲染单测（FakeResolver，不触碰查询链路 / 不调 LLM）。
/// 覆盖：主题 → ColorMap 解析、组件 Style 语义键映射具体色值、主题切换产生不同 StyleSpec。
/// </summary>
public class ThemeRenderTests
{
	private sealed class FakeResolver : IWidgetDataResolver
	{
		public Task<WidgetDataResult> ResolveAsync(
			WidgetQueryDsl? query, PlatformContext context,
			IReadOnlyList<FilterDsl> effectiveFilters, CancellationToken ct = default)
		{
			var r = new WidgetDataResult { Resolved = true, Decision = "Proceed" };
			r.Columns.Add("region");
			r.Rows.Add(new Dictionary<string, object?> { ["region"] = "East" });
			return Task.FromResult(r);
		}
	}

	private static ThemeContext DarkTheme()
	{
		var dsl = new ThemeDsl
		{
			Color = new ThemeColor
			{
				Primary = "#f59e0b",
				Background = "#0f172a",
				Surface = "#1e293b",
				Text = "#f8fafc",
			},
			Brand = new ThemeBrand { Primary = "#f59e0b", Accent = "#22d3ee" },
		};
		return new ThemeContext { Key = "dark", Source = ThemeSource.Tenant, Dsl = dsl };
	}

	private static DashboardDsl SampleDsl()
	{
		return new DashboardDsl
		{
			Title = "T",
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
							Title = "图表",
							ChartType = "column",
							Style = new StyleDsl
							{
								Palette = "primary",
								Background = "background",
								ShowBorder = false,
								Padding = "compact",
							},
							Query = new WidgetQueryDsl { Question = "销售额" },
						},
						new KpiWidgetDsl
						{
							Id = "k1",
							Title = "指标",
							ValueField = "amount",
							// 无 Style：应回退到主题 Component 默认值
						},
					},
				},
			},
		};
	}

	[Fact]
	public void ThemeRenderMapper_Build_PopulatesColorMap()
	{
		var model = ThemeRenderMapper.Build(DarkTheme());

		Assert.Equal("dark", model.Key);
		Assert.Equal(nameof(ThemeSource.Tenant), model.Source);
		Assert.Equal("#f59e0b", model.ColorMap["primary"]);
		Assert.Equal("#0f172a", model.ColorMap["background"]);
		Assert.Equal("#22d3ee", model.ColorMap["brand.accent"]);
	}

	[Fact]
	public void ThemeRenderMapper_Build_NullContext_FallsBackToBuiltIn()
	{
		var model = ThemeRenderMapper.Build(null);
		Assert.Equal(BuiltInThemeKeys.Default, model.Key);
		Assert.Equal(BuiltInThemes.DefaultDsl().Color.Primary, model.ColorMap["primary"]);
	}

	[Fact]
	public async Task Render_DarkTheme_ResolvesWidgetStyleToConcreteColors()
	{
		var dsl = SampleDsl();
		var renderer = new DashboardLowcodeRenderer(new FakeResolver());
		var context = PlatformContext.System with { Theme = DarkTheme() };

		var model = await renderer.RenderAsync(dsl, context);

		Assert.NotNull(model.Theme);
		Assert.Equal("#f59e0b", model.Theme!.ColorMap["primary"]);

		var chart = model.Pages[0].Widgets.Single(w => w.Id == "c1");
		Assert.NotNull(chart.StyleSpec);
		Assert.Equal("#f59e0b", chart.StyleSpec!.PaletteColor);
		Assert.Equal("#0f172a", chart.StyleSpec.BackgroundColor);
		Assert.False(chart.StyleSpec.ShowBorder);
		Assert.Equal("compact", chart.StyleSpec.Padding);
	}

	[Fact]
	public async Task Render_NoWidgetStyle_FallsBackToThemeComponentDefaults()
	{
		var dsl = SampleDsl();
		var renderer = new DashboardLowcodeRenderer(new FakeResolver());
		var context = PlatformContext.System with { Theme = DarkTheme() };

		var model = await renderer.RenderAsync(dsl, context);

		var kpi = model.Pages[0].Widgets.Single(w => w.Id == "k1");
		Assert.NotNull(kpi.StyleSpec);
		// 未指定 → 取主题 Component 默认值（内置默认 true / "normal"）。
		Assert.True(kpi.StyleSpec!.ShowBorder);
		Assert.Equal("normal", kpi.StyleSpec.Padding);
		// 未指定 Palette/Background → 为 null（前端回退主题令牌）。
		Assert.Null(kpi.StyleSpec.PaletteColor);
		Assert.Null(kpi.StyleSpec.BackgroundColor);
	}

	[Fact]
	public async Task Render_SameDsl_TwoThemes_ProducesDifferentStyleSpec()
	{
		var dsl = SampleDsl();
		var renderer = new DashboardLowcodeRenderer(new FakeResolver());

		var builtIn = await renderer.RenderAsync(dsl, PlatformContext.System);
		var dark = await renderer.RenderAsync(dsl, PlatformContext.System with { Theme = DarkTheme() });

		var builtInChart = builtIn.Pages[0].Widgets.Single(w => w.Id == "c1");
		var darkChart = dark.Pages[0].Widgets.Single(w => w.Id == "c1");

		// 主题切换 → 根级 ColorMap 与组件级 StyleSpec 的色值均不同。
		Assert.NotEqual(builtIn.Theme!.ColorMap["primary"], dark.Theme!.ColorMap["primary"]);
		Assert.NotEqual(builtInChart.StyleSpec!.PaletteColor, darkChart.StyleSpec!.PaletteColor);
		Assert.Equal(BuiltInThemes.DefaultDsl().Color.Primary, builtIn.Theme.ColorMap["primary"]);
		Assert.Equal("#f59e0b", dark.Theme.ColorMap["primary"]);
	}
}
