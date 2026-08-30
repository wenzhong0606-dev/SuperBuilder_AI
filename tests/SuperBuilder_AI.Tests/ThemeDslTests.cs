using System.Text.Json;
using SuperBuilder_AI.Models.Theme;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>P7.1 主题 DSL 与实体的结构化契约测试（不承载 CSS/HTML）。</summary>
public class ThemeDslTests
{
	[Fact]
	public void DefaultTheme_HasExpectedTokens()
	{
		var theme = BuiltInThemes.DefaultDsl();

		// 语义色板默认值
		Assert.Equal("#2563eb", theme.Color.Primary);
		Assert.Equal("#ffffff", theme.Color.Background);
		Assert.Equal("#0f172a", theme.Color.Text);

		// 图表色板 8 色序列
		Assert.Equal(8, theme.ChartPalette.Series.Count);
		Assert.Equal("#16a34a", theme.ChartPalette.Positive);

		// 布局 / 边框 / 圆角默认值
		Assert.Equal(12, theme.Layout.Columns);
		Assert.Equal("solid", theme.Border.Style);
		Assert.Equal(8, theme.Radius.Md);

		// 组件默认风格
		Assert.True(theme.Component.CardShowBorder);
		Assert.Equal("primary", theme.Component.ButtonPrimaryBg);
	}

	[Fact]
	public void ThemeDsl_JsonRoundTrip_PreservesValues()
	{
		var original = BuiltInThemes.DefaultDsl();
		var json = JsonSerializer.Serialize(original);
		var restored = JsonSerializer.Deserialize<ThemeDsl>(json);

		Assert.NotNull(restored);
		Assert.Equal(original.Color.Primary, restored!.Color.Primary);
		Assert.Equal(original.Color.Background, restored.Color.Background);
		Assert.Equal(original.ChartPalette.Series.Count, restored.ChartPalette.Series.Count);
		Assert.Equal(original.Typography.FontFamily, restored.Typography.FontFamily);
		Assert.Equal(original.Layout.Columns, restored.Layout.Columns);
		Assert.Equal(original.Radius.Pill, restored.Radius.Pill);
	}

	[Fact]
	public void ThemeEntity_Defaults()
	{
		var theme = new Theme { Key = "dark", Name = "Dark", TenantId = 1, DslJson = "{}" };

		Assert.Equal(ThemeDslVersions.Current, theme.DslVersion);
		Assert.False(theme.IsBuiltIn);
		Assert.Equal(0, theme.Id); // 未持久化
	}
}
