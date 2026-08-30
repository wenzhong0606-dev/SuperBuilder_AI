using System.Collections.Generic;
using System.Linq;
using SuperBuilder_AI.Models.Agent;
using SuperBuilder_AI.Services.Agent;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>P9.1 AgentDslSerializer 单测（往返 / 校验 / HTML 红线 / 版本 / 枚举白名单）。</summary>
public class AgentDslSerializerTests
{
	private static AgentDslSerializer Create() => new();

	private static AgentDsl SampleDsl() => new()
	{
		Name = "销售分析助手",
		UserRequest = "分析销售额下降原因",
		SelectedTools = new List<AgentToolSelection>
		{
			new() { Tool = AgentTools.Query, Order = 1, Reason = "取数" },
			new() { Tool = AgentTools.Dashboard, Order = 2, Reason = "可视化" },
		},
		AnomalyChain = ToolRegistry.BuildAnomalyChain("销售额").ToList(),
	};

	[Fact]
	public void RoundTrip_SerializesAndDeserializes()
	{
		var s = Create();
		var json = s.Serialize(SampleDsl());
		Assert.True(s.TryDeserialize(json, out var dsl, out var errors));
		Assert.Empty(errors);
		Assert.NotNull(dsl);
		Assert.Equal("销售分析助手", dsl!.Name);
		Assert.Equal(2, dsl.SelectedTools.Count);
		Assert.Equal(6, dsl.AnomalyChain.Count);
	}

	[Fact]
	public void Validate_ValidDsl_Passes()
		=> Assert.Empty(Create().Validate(SampleDsl()));

	[Fact]
	public void Validate_EmptyName_Fails()
	{
		var dsl = SampleDsl();
		dsl.Name = "";
		var errors = Create().Validate(dsl);
		Assert.Contains(errors, e => e.Contains("名称"));
	}

	[Fact]
	public void Validate_HtmlInName_Fails()
	{
		var dsl = SampleDsl();
		dsl.Name = "<script>alert(1)</script>";
		var errors = Create().Validate(dsl);
		Assert.Contains(errors, e => e.Contains("HTML"));
	}

	[Fact]
	public void Validate_NoTools_Fails()
	{
		var dsl = SampleDsl();
		dsl.SelectedTools.Clear();
		var errors = Create().Validate(dsl);
		Assert.Contains(errors, e => e.Contains("至少需要选择一个工具"));
	}

	[Fact]
	public void Validate_UnsupportedTool_Fails()
	{
		var dsl = SampleDsl();
		dsl.SelectedTools.Add(new AgentToolSelection { Tool = "hacking", Order = 3 });
		var errors = Create().Validate(dsl);
		Assert.Contains(errors, e => e.Contains("不支持的工具类型") && e.Contains("hacking"));
	}

	[Fact]
	public void Validate_UnsupportedVersion_Fails()
	{
		var dsl = SampleDsl();
		dsl.Version = "9.9";
		var errors = Create().Validate(dsl);
		Assert.Contains(errors, e => e.Contains("版本"));
	}

	[Fact]
	public void Validate_AnomalyChainUnsupportedDimension_Fails()
	{
		var dsl = SampleDsl();
		dsl.AnomalyChain.Add(new AnalysisStep { Order = 7, Dimension = "galaxy", Direction = AnalysisDirections.Drill, Tool = AgentTools.Query });
		var errors = Create().Validate(dsl);
		Assert.Contains(errors, e => e.Contains("分析维度") && e.Contains("galaxy"));
	}

	[Fact]
	public void Validate_DuplicateToolOrder_Fails()
	{
		var dsl = SampleDsl();
		dsl.SelectedTools[1].Order = 1; // 与第一个重复
		var errors = Create().Validate(dsl);
		Assert.Contains(errors, e => e.Contains("工具执行顺序重复"));
	}

	[Fact]
	public void TryDeserialize_MalformedJson_Fails()
	{
		Assert.False(Create().TryDeserialize("{ not valid json ", out _, out var errors));
		Assert.NotEmpty(errors);
	}

	[Fact]
	public void TryDeserialize_HtmlRedLine_Fails()
	{
		var json = "{\"version\":\"1.0\",\"name\":\"<img src=x onerror=alert(1)>\",\"selectedTools\":[{\"tool\":\"query\",\"order\":1}]}";
		Assert.False(Create().TryDeserialize(json, out _, out var errors));
		Assert.Contains(errors, e => e.Contains("HTML"));
	}

	[Fact]
	public void TryDeserialize_EmptyJson_Fails()
		=> Assert.False(Create().TryDeserialize("   ", out _, out _));
}
