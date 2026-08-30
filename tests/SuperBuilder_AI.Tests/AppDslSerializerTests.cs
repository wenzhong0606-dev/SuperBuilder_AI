using System.Collections.Generic;
using System.Text.Json;
using SuperBuilder_AI.Models.AppBuilder;
using SuperBuilder_AI.Services.AppBuilder;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>P8.1 AppDslSerializer 单测（纯函数，无 DB 依赖）。</summary>
public class AppDslSerializerTests
{
	private static AppDslSerializer Create() => new();

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
						Properties = new Dictionary<string, string> { ["format"] = "N2" },
					},
					new()
					{
						Type = AppComponentTypes.Chart,
						Id = "chart1",
						Title = "趋势",
					},
				},
			},
		},
	};

	[Fact]
	public void Serialize_RoundTrips_Through_Deserialize()
	{
		var s = Create();
		var json = s.Serialize(SampleDsl());
		var ok = s.TryDeserialize(json, out var dsl, out var errors);
		Assert.True(ok);
		Assert.Empty(errors);
		Assert.NotNull(dsl);
		Assert.Equal("销售看板", dsl!.Name);
		Assert.Single(dsl.Pages);
		Assert.Equal(2, dsl.Pages[0].Components.Count);
		Assert.Equal(AppComponentTypes.Kpi, dsl.Pages[0].Components[0].Type);
		Assert.Equal("N2", dsl.Pages[0].Components[0].Properties["format"]);
		Assert.Equal("sales_order", dsl.Pages[0].Components[0].Binding!.Entity);
	}

	[Fact]
	public void TryDeserialize_EmptyJson_ReturnsFalse()
	{
		var ok = Create().TryDeserialize("   ", out _, out var errors);
		Assert.False(ok);
		Assert.NotEmpty(errors);
	}

	[Fact]
	public void TryDeserialize_MalformedJson_ReturnsFalse()
	{
		var ok = Create().TryDeserialize("{ bad json", out _, out var errors);
		Assert.False(ok);
		Assert.NotEmpty(errors);
	}

	[Fact]
	public void TryDeserialize_UnsupportedVersion_ReturnsFalse()
	{
		var json = JsonSerializer.Serialize(new AppDsl { Version = "9.9", Name = "x" });
		var ok = Create().TryDeserialize(json, out _, out var errors);
		Assert.False(ok);
		Assert.Contains(errors, e => e.Contains("版本"));
	}

	[Fact]
	public void Validate_DuplicatePageId_ReturnsFalse()
	{
		var dsl = SampleDsl();
		dsl.Pages.Add(new PagePlan { Id = "home", Name = "dup", Order = 2 });
		var ok = Create().TryDeserialize(Create().Serialize(dsl), out _, out var errors);
		Assert.False(ok);
		Assert.Contains(errors, e => e.Contains("重复"));
	}

	[Fact]
	public void Validate_UnsupportedComponentType_ReturnsFalse()
	{
		var dsl = SampleDsl();
		dsl.Pages[0].Components.Add(new ComponentPlan { Type = "video", Id = "c2" });
		var ok = Create().TryDeserialize(Create().Serialize(dsl), out _, out var errors);
		Assert.False(ok);
		Assert.Contains(errors, e => e.Contains("类型不受支持"));
	}

	[Fact]
	public void Validate_HtmlInName_ReturnsFalse()
	{
		var dsl = SampleDsl();
		dsl.Name = "<script>alert(1)</script>";
		var ok = Create().TryDeserialize(Create().Serialize(dsl), out _, out var errors);
		Assert.False(ok);
		Assert.Contains(errors, e => e.Contains("HTML"));
	}

	[Fact]
	public void Validate_EmptyPages_ReturnsFalse()
	{
		var dsl = new AppDsl { Name = "x" };
		var ok = Create().TryDeserialize(Create().Serialize(dsl), out _, out var errors);
		Assert.False(ok);
		Assert.Contains(errors, e => e.Contains("至少需要一个页面"));
	}

	[Fact]
	public void Validate_UnsupportedAggregation_ReturnsFalse()
	{
		var dsl = SampleDsl();
		dsl.Pages[0].Components[0].Binding!.Metrics[0].Aggregation = "median";
		var ok = Create().TryDeserialize(Create().Serialize(dsl), out _, out var errors);
		Assert.False(ok);
		Assert.Contains(errors, e => e.Contains("聚合方式不受支持"));
	}

	[Fact]
	public void DefaultAppPlan_HasDraftStatus_AndCurrentDslVersion()
	{
		var plan = new AppPlan { Code = "demo", Name = "Demo" };
		Assert.Equal(AppStatuses.Draft, plan.Status);
		Assert.Equal(AppDslVersions.Current, plan.DslVersion);
		Assert.Equal(0, plan.TenantId);
	}
}
