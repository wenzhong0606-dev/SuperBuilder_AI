using System.Collections.Generic;
using System.Text.Json;
using SuperBuilder_AI.Models.Theme;
using SuperBuilder_AI.Services.Theming;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>P7.4 ThemeDslSerializer 单测（纯函数，无 DB 依赖）。</summary>
public class ThemeDslSerializerTests
{
	private static string SampleDslJson() => ThemeDslSerializer.Serialize(new ThemeDsl
	{
		Brand = new ThemeBrand { Primary = "#123456" },
		Color = new ThemeColor { Primary = "#123456" },
	});

	[Fact]
	public void Serialize_RoundTrips_Through_Deserialize()
	{
		var json = SampleDslJson();
		var ok = ThemeDslSerializer.TryDeserialize(json, out var dsl, out var errors);
		Assert.True(ok);
		Assert.Empty(errors);
		Assert.NotNull(dsl);
		Assert.Equal("#123456", dsl!.Color.Primary);
	}

	[Fact]
	public void TryDeserialize_EmptyJson_ReturnsFalse_WithError()
	{
		var ok = ThemeDslSerializer.TryDeserialize("   ", out _, out var errors);
		Assert.False(ok);
		Assert.NotEmpty(errors);
	}

	[Fact]
	public void TryDeserialize_MalformedJson_ReturnsFalse()
	{
		var ok = ThemeDslSerializer.TryDeserialize("{ not valid json", out _, out var errors);
		Assert.False(ok);
		Assert.NotEmpty(errors);
	}

	[Fact]
	public void TryDeserialize_UnsupportedVersion_ReturnsFalse()
	{
		var json = JsonSerializer.Serialize(new ThemeDsl { Version = "9.9" });
		var ok = ThemeDslSerializer.TryDeserialize(json, out _, out var errors);
		Assert.False(ok);
		Assert.Contains(errors, e => e.Contains("版本"));
	}
}
