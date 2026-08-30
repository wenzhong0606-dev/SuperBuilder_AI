using System.Linq;
using SuperBuilder_AI.Models.Agent;
using SuperBuilder_AI.Services.Agent;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>P9.1 ToolRegistry 单测（工具目录 + 确定性意图解析 + 异常分析链路）。</summary>
public class ToolRegistryTests
{
	[Fact]
	public void GetAll_ReturnsEightTools()
		=> Assert.Equal(8, ToolRegistry.GetAll().Count);

	[Fact]
	public void Get_KnownTool_ReturnsDescriptor()
	{
		var desc = ToolRegistry.Get(AgentTools.Query);
		Assert.NotNull(desc);
		Assert.Equal("指标查询", desc!.Name);
	}

	[Fact]
	public void Get_UnknownTool_ReturnsNull()
		=> Assert.Null(ToolRegistry.Get("nonexistent"));

	[Theory]
	[InlineData("销售额为什么下降了")]
	[InlineData("分析一下异常波动的原因")]
	[InlineData("why did revenue drop")]
	public void ResolveFromIntent_AnomalyKeywords_SelectsQueryDashboardForecast(string request)
	{
		var tools = ToolRegistry.ResolveFromIntent(request);
		var names = tools.Select(t => t.Tool).ToHashSet();
		Assert.Contains(AgentTools.Query, names);
		Assert.Contains(AgentTools.Dashboard, names);
		Assert.Contains(AgentTools.Forecast, names);
	}

	[Fact]
	public void ResolveFromIntent_DashboardKeyword_SelectsOnlyDashboard()
	{
		var tools = ToolRegistry.ResolveFromIntent("给我做个销售看板");
		Assert.Single(tools);
		Assert.Equal(AgentTools.Dashboard, tools[0].Tool);
	}

	[Fact]
	public void ResolveFromIntent_Empty_ReturnsEmpty()
		=> Assert.Empty(ToolRegistry.ResolveFromIntent(""));

	[Fact]
	public void ResolveFromIntent_Unknown_ReturnsEmpty()
		=> Assert.Empty(ToolRegistry.ResolveFromIntent("今天天气真好"));

	[Fact]
	public void ResolveFromIntent_OrderIsSequentialWithoutDuplicates()
	{
		var tools = ToolRegistry.ResolveFromIntent("查询指标并生成看板和报表，再加个预测");
		var orders = tools.Select(t => t.Order).ToList();
		Assert.Equal(orders.OrderBy(o => o).ToList(), orders);
		Assert.Equal(orders.Distinct().Count(), orders.Count);
		Assert.All(tools, t => Assert.False(string.IsNullOrWhiteSpace(t.Reason)));
	}

	[Fact]
	public void BuildAnomalyChain_ReturnsSixSteps_WithCorrectDimensions()
	{
		var steps = ToolRegistry.BuildAnomalyChain("销售额", "sales_order");
		Assert.Equal(6, steps.Count);

		var dims = steps.Select(s => s.Dimension).ToArray();
		Assert.Equal(new[] { AnalysisDimensions.Time, AnalysisDimensions.Time, AnalysisDimensions.Region, AnalysisDimensions.Customer, AnalysisDimensions.Product, AnalysisDimensions.Channel }, dims);

		var dirs = steps.Select(s => s.Direction).ToArray();
		Assert.Equal(AnalysisDirections.YoY, dirs[0]);
		Assert.Equal(AnalysisDirections.MoM, dirs[1]);
		Assert.All(dirs.Skip(2), d => Assert.Equal(AnalysisDirections.Drill, d));
	}

	[Fact]
	public void BuildAnomalyChain_SequentialOrders()
	{
		var steps = ToolRegistry.BuildAnomalyChain("利润");
		Assert.Equal(Enumerable.Range(1, 6).ToList(), steps.Select(s => s.Order).ToList());
		Assert.All(steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Description)));
	}
}
