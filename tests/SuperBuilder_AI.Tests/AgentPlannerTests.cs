using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.Agent;
using SuperBuilder_AI.Models.Agent;
using SuperBuilder_AI.Services.Agent;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>P9.2 AgentPlanner 单测：默认路径（确定性、无 LLM）与非默认 LLM 路径均覆盖。</summary>
public class AgentPlannerTests
{
	/// <summary>可配置返回内容的假 Qwen 服务，用于隔离测试 LLM 编排路径。</summary>
	private sealed class FakeQwen : IQwenService
	{
		private readonly string _response;
		public FakeQwen(string response) => _response = response;
		public Task<string> GenerateSqlAsync(string prompt) => Task.FromResult(_response);
	}

	private static readonly AgentDslSerializer Serializer = new();

	private static AgentPlanner CreateDefault() => new(Serializer, new FakeQwen("ignored"));

	/// <summary>把编排结果中的 DSL JSON 反序列化为强类型 DSL 供断言。</summary>
	private static AgentDsl DslOf(AgentResult result)
	{
		Assert.True(Serializer.TryDeserialize(result.Plan!.DslJson, out var dsl, out _));
		return dsl!;
	}

	[Fact]
	public async Task PlanFromIntent_QueryIntent_SelectsQueryTool_NoAi()
	{
		var agent = CreateDefault();
		var result = await agent.PlanFromIntentAsync(tenantId: 7, "帮我查询本月销售额排名");

		Assert.True(result.Success);
		Assert.False(result.UsedAi);
		Assert.NotNull(result.Plan);
		Assert.Equal(7, result.Plan!.TenantId);
		var dsl = DslOf(result);
		Assert.Contains(dsl.SelectedTools, t => t.Tool == AgentTools.Query);
		Assert.Empty(dsl.AnomalyChain);
	}

	[Fact]
	public async Task PlanFromIntent_AnomalyIntent_IncludesAnomalyChain_NoAi()
	{
		var agent = CreateDefault();
		var result = await agent.PlanFromIntentAsync(tenantId: 7, "分析销售额为什么下降");

		Assert.True(result.Success);
		Assert.False(result.UsedAi);
		var dsl = DslOf(result);
		Assert.NotEmpty(dsl.AnomalyChain);
		// 链路顺序应为 1..6 且维度覆盖 time→region→customer→product→channel
		Assert.Equal(1, dsl.AnomalyChain.Min(s => s.Order));
		Assert.Contains(dsl.AnomalyChain, s => s.Dimension == AnalysisDimensions.Region && s.Direction == AnalysisDirections.Drill);
		Assert.Contains(dsl.AnomalyChain, s => s.Dimension == AnalysisDimensions.Time && s.Direction == AnalysisDirections.YoY);
	}

	[Fact]
	public async Task PlanFromIntent_EmptyIntent_Fails()
	{
		var agent = CreateDefault();
		var result = await agent.PlanFromIntentAsync(tenantId: 7, intent: "   ");

		Assert.False(result.Success);
		Assert.False(result.UsedAi);
	}

	[Fact]
	public async Task PlanFromIntent_NoToolMatched_Fails()
	{
		var agent = CreateDefault();
		var result = await agent.PlanFromIntentAsync(tenantId: 7, "今天天气不错");

		Assert.False(result.Success);
		Assert.Contains(result.Errors, e => e.Contains("无法从意图中识别任何可用工具"));
	}

	[Fact]
	public async Task PlanFromIntent_ExplicitCode_Used()
	{
		var agent = CreateDefault();
		var result = await agent.PlanFromIntentAsync(tenantId: 0, "做一个预测看板", code: "forecast-board");

		Assert.True(result.Success);
		Assert.Equal("forecast-board", result.Plan!.Code);
		Assert.Equal(0, result.Plan.TenantId);
	}

	[Fact]
	public async Task GenerateFromDescription_ValidLlmJson_Succeeds_UsedAi()
	{
		var dslJson = """
			{
			  "version": "1.0",
			  "code": "sales-agent",
			  "name": "销售分析 Agent",
			  "userRequest": "分析销售额为什么下降",
			  "selectedTools": [
				{ "tool": "query", "order": 1, "reason": "计算指标" },
				{ "tool": "dashboard", "order": 2, "reason": "可视化" }
			  ],
			  "anomalyChain": [
				{ "order": 1, "dimension": "time", "direction": "yoy", "tool": "query", "description": "同比" }
			  ]
			}
			""";
		var agent = new AgentPlanner(Serializer, new FakeQwen(dslJson));
		var result = await agent.GenerateFromDescriptionAsync(tenantId: 5, "分析销售额为什么下降");

		Assert.True(result.Success);
		Assert.True(result.UsedAi);
		Assert.Equal("sales-agent", result.Plan!.Code);
		Assert.Equal(5, result.Plan.TenantId);
	}

	[Fact]
	public async Task GenerateFromDescription_EmptyDescription_Fails()
	{
		var agent = CreateDefault();
		var result = await agent.GenerateFromDescriptionAsync(tenantId: 5, description: "");

		Assert.False(result.Success);
		Assert.False(result.UsedAi);
	}

	[Fact]
	public async Task GenerateFromDescription_MalformedLlmJson_Fails_UsedAi()
	{
		var agent = new AgentPlanner(Serializer, new FakeQwen("{ not valid json "));
		var result = await agent.GenerateFromDescriptionAsync(tenantId: 5, "随便来一个");

		Assert.False(result.Success);
		Assert.True(result.UsedAi);
	}

	[Fact]
	public async Task GenerateFromDescription_UnsupportedTool_Fails_UsedAi()
	{
		var dslJson = """
			{
			  "version": "1.0",
			  "name": "坏 Agent",
			  "selectedTools": [ { "tool": "hack", "order": 1 } ]
			}
			""";
		var agent = new AgentPlanner(Serializer, new FakeQwen(dslJson));
		var result = await agent.GenerateFromDescriptionAsync(tenantId: 5, "生成一个 agent");

		Assert.False(result.Success);
		Assert.True(result.UsedAi);
		Assert.Contains(result.Errors, e => e.Contains("不支持的工具类型"));
	}

	[Fact]
	public async Task GenerateFromDescription_HtmlInName_Fails_UsedAi()
	{
		var dslJson = """
			{
			  "version": "1.0",
			  "name": "<script>alert(1)</script>",
			  "selectedTools": [ { "tool": "query", "order": 1 } ]
			}
			""";
		var agent = new AgentPlanner(Serializer, new FakeQwen(dslJson));
		var result = await agent.GenerateFromDescriptionAsync(tenantId: 5, "生成带脚本的 agent");

		Assert.False(result.Success);
		Assert.True(result.UsedAi);
		Assert.Contains(result.Errors, e => e.Contains("HTML"));
	}

	[Fact]
	public async Task PlanFromIntent_Deterministic_SameIntentSameTools()
	{
		var agent = CreateDefault();
		var a = await agent.PlanFromIntentAsync(tenantId: 3, "做个销售预测趋势分析");
		var b = await agent.PlanFromIntentAsync(tenantId: 3, "做个销售预测趋势分析");

		Assert.True(a.Success && b.Success);
		var aTools = string.Join(",", DslOf(a).SelectedTools.OrderBy(t => t.Order).Select(t => t.Tool));
		var bTools = string.Join(",", DslOf(b).SelectedTools.OrderBy(t => t.Order).Select(t => t.Tool));
		Assert.Equal(aTools, bTools);
	}
}
