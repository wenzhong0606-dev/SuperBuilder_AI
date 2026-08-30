using System.Collections.Generic;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.AppBuilder;
using SuperBuilder_AI.Models.AppBuilder;
using SuperBuilder_AI.Services.AppBuilder;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>P8.2 AppBuilderAgent 单测：默认路径（确定性、无 LLM）与非默认 LLM 路径均覆盖。</summary>
public class AppBuilderAgentTests
{
	/// <summary>可配置返回内容的假 Qwen 服务，用于隔离测试 LLM 编排路径。</summary>
	private sealed class FakeQwen : IQwenService
	{
		private readonly string _response;
		public FakeQwen(string response) => _response = response;
		public Task<string> GenerateSqlAsync(string prompt) => Task.FromResult(_response);
	}

	private static AppDsl SampleDsl() => new()
	{
		Name = "销售看板",
		Code = "sales-board",
		ThemeKey = "acme-dark",
		Pages = new List<PagePlan>
		{
			new()
			{
				Id = "home",
				Name = "首页",
				Order = 1,
				Components = new List<ComponentPlan>
				{
					new()
					{
						Type = AppComponentTypes.Kpi,
						Id = "kpi1",
						Title = "总销售额",
						Binding = new AppDataSourceBinding
						{
							Entity = "sales_order",
							Metrics = new List<AppMetricBinding> { new() { Field = "amount", Aggregation = AppAggregateTypes.Sum } },
							Dimensions = new List<string> { "region" },
						},
					},
				},
			},
		},
	};

	private static AppBuilderAgent CreateDefault() => new(new AppDslSerializer(), new FakeQwen("ignored"));

	[Fact]
	public async Task BuildFromDsl_ValidDsl_Succeeds_NoAi()
	{
		var agent = CreateDefault();
		var result = await agent.BuildFromDslAsync(tenantId: 7, SampleDsl());

		Assert.True(result.Success);
		Assert.False(result.UsedAi);
		Assert.NotNull(result.Plan);
		Assert.Equal(7, result.Plan!.TenantId);
		Assert.Equal("sales-board", result.Plan.Code);
		Assert.Equal("销售看板", result.Plan.Name);
		Assert.Equal("acme-dark", result.Plan.ThemeKey);
		Assert.Equal(AppStatuses.Draft, result.Plan.Status);
		Assert.Equal(AppDslVersions.Current, result.Plan.DslVersion);
		Assert.False(string.IsNullOrEmpty(result.DslJson));
	}

	[Fact]
	public async Task BuildFromDsl_EmptyPages_Fails_WithErrors()
	{
		var agent = CreateDefault();
		var dsl = new AppDsl { Name = "x" };
		var result = await agent.BuildFromDslAsync(1, dsl);

		Assert.False(result.Success);
		Assert.False(result.UsedAi);
		Assert.Null(result.Plan);
		Assert.Contains(result.Errors, e => e.Contains("至少需要一个页面"));
	}

	[Fact]
	public async Task BuildFromDsl_HtmlInName_Fails_RedLine()
	{
		var agent = CreateDefault();
		var dsl = SampleDsl();
		dsl.Name = "<script>alert(1)</script>";
		var result = await agent.BuildFromDslAsync(1, dsl);

		Assert.False(result.Success);
		Assert.Contains(result.Errors, e => e.Contains("HTML"));
	}

	[Fact]
	public async Task BuildFromDsl_CodeResolution_ExplicitWins()
	{
		var agent = CreateDefault();
		var dsl = SampleDsl();
		var result = await agent.BuildFromDslAsync(1, dsl, code: "explicit-code");

		Assert.True(result.Success);
		Assert.Equal("explicit-code", result.Plan!.Code);
	}

	[Fact]
	public async Task BuildFromDsl_CodeResolution_FallsBackToSlugFromName()
	{
		var agent = CreateDefault();
		var dsl = SampleDsl();
		dsl.Code = null; // 让出位置，验证 slug 兜底
		var result = await agent.BuildFromDslAsync(1, dsl);

		Assert.True(result.Success);
		Assert.Equal("销售看板".Length > 0 ? "销售看板" : "app", result.Plan!.Code);
	}

	[Fact]
	public async Task GenerateFromDescription_ValidLlmJson_Succeeds_WithAi()
	{
		var llmJson = """
			{
			  "version": "1.0",
			  "name": "库存看板",
			  "pages": [
			    {
			      "id": "p1",
			      "name": "库存",
			      "order": 1,
			      "components": [
			        {
			          "type": "kpi",
			          "id": "k1",
			          "title": "总库存",
			          "binding": {
			            "entity": "stock",
			            "metrics": [ { "field": "qty", "aggregation": "sum" } ],
			            "dimensions": [],
			            "filters": [],
			            "limit": null
			          }
			        }
			      ]
			    }
			  ]
			}
			""";
		var agent = new AppBuilderAgent(new AppDslSerializer(), new FakeQwen(llmJson));
		var result = await agent.GenerateFromDescriptionAsync(tenantId: 3, "做一个库存看板");

		Assert.True(result.Success);
		Assert.True(result.UsedAi);
		Assert.NotNull(result.Plan);
		Assert.Equal(3, result.Plan!.TenantId);
		Assert.Equal("库存看板", result.Plan.Name);
		Assert.Equal(AppStatuses.Draft, result.Plan.Status);
	}

	[Fact]
	public async Task GenerateFromDescription_EmptyDescription_Fails()
	{
		var agent = new AppBuilderAgent(new AppDslSerializer(), new FakeQwen("{}"));
		var result = await agent.GenerateFromDescriptionAsync(1, "   ");

		Assert.False(result.Success);
		Assert.Contains(result.Errors, e => e.Contains("描述"));
	}

	[Fact]
	public async Task GenerateFromDescription_MalformedLlmJson_Fails()
	{
		var agent = new AppBuilderAgent(new AppDslSerializer(), new FakeQwen("{ not json"));
		var result = await agent.GenerateFromDescriptionAsync(1, "做一个看板");

		Assert.False(result.Success);
		Assert.True(result.UsedAi); // 仍标记为 AI 路径
		Assert.NotEmpty(result.Errors);
	}

	[Fact]
	public async Task GenerateFromDescription_UnsupportedComponentFromLlm_Fails_RedLine()
	{
		var llmJson = """
			{
			  "version": "1.0",
			  "name": "坏看板",
			  "pages": [
			    { "id": "p1", "name": "p", "order": 1,
			      "components": [ { "type": "video", "id": "v1" } ] }
			  ]
			}
			""";
		var agent = new AppBuilderAgent(new AppDslSerializer(), new FakeQwen(llmJson));
		var result = await agent.GenerateFromDescriptionAsync(1, "做一个带视频的看板");

		Assert.False(result.Success);
		Assert.Contains(result.Errors, e => e.Contains("类型不受支持"));
	}
}
