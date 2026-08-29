using System.Linq;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.Platform;
using Xunit;

namespace SuperBuilder_AI.Tests.Platform;

/// <summary>
/// P5.1 本地化上下文与服务的单测（纯内存、离线可断言）。
///
/// 覆盖三类关键行为：
///   1. 文化名归一化与无效输入回退（zh_CN → zh-CN；null/胡乱输入 → 默认 zh-CN）；
///   2. 回退链构造（精确文化 → 语言段 → 平台默认）；
///   3. 零回归约束：默认语言恒为 zh-CN，未指定语言的路径行为与 P5 之前一致。
/// </summary>
public class LocalizationServiceTests
{
    private readonly LocalizationService _service = new();

    [Theory]
    // 归一化：下划线 → 连字符、语言段小写、地区段大写
    [InlineData("zh-CN", "zh-CN")]
    [InlineData("zh_CN", "zh-CN")]
    [InlineData("ZH-cn", "zh-CN")]
    [InlineData("en-us", "en-US")]
    [InlineData("ja-JP", "ja-JP")]
    // 纯语言段（无地区）保持原样小写
    [InlineData("zh", "zh")]
    [InlineData("EN", "en")]
    public void Resolve_NormalizesCulture(string? input, string expected)
    {
        var locale = _service.Resolve(input);

        Assert.Equal(expected, locale.Culture);
    }

    [Theory]
    // 无效/缺失输入一律回退平台默认，保证调用方永不拿到空文化名
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a culture")]
    [InlineData("12345")]
    [InlineData("zh-CN-XX")]
    public void Resolve_InvalidInput_FallsBackToDefault(string? input)
    {
        var locale = _service.Resolve(input);

        Assert.Same(LocaleContext.Default, locale);
        Assert.True(locale.IsDefault);
        Assert.Equal("zh-CN", locale.Culture);
    }

    [Fact]
    public void Default_IsChineseSimplified_ZeroRegressionBaseline()
    {
        // 零回归硬约束：平台默认必须是 zh-CN，
        // 否则未显式指定语言的路径（含 Golden 运行时）行为将发生偏移。
        Assert.Equal("zh-CN", LocaleContext.Default.Culture);
        Assert.Equal("zh", LocaleContext.Default.Language);
        Assert.Equal("CN", LocaleContext.Default.Region);
        Assert.True(LocaleContext.Default.IsDefault);
        Assert.Same(LocaleContext.Default, LocaleContext.FromCulture(null));
    }

    [Fact]
    public void PlatformContext_WithoutCulture_UsesDefaultLocale()
    {
        var context = PlatformContext.FromTenant(42, "T42");

        Assert.Equal(42, context.Tenant.TenantId);
        Assert.Same(LocaleContext.Default, context.Locale);
    }

    [Fact]
    public void PlatformContext_WithCulture_ResolvesLocale()
    {
        var context = PlatformContext.FromTenant(42, "T42", "ja-JP");

        Assert.Equal("ja-JP", context.Locale.Culture);
        Assert.Equal("ja", context.Locale.Language);
        Assert.False(context.Locale.IsDefault);
    }

    [Fact]
    public void BuildFallbackChain_ForNonDefault_IsCultureThenLanguageThenDefault()
    {
        var chain = _service.BuildFallbackChain(_service.Resolve("zh-TW"));

        Assert.Equal(new[] { "zh-TW", "zh", "zh-CN" }, chain);
    }

    [Fact]
    public void BuildFallbackChain_ForDefault_IsSingleElement()
    {
        // 默认语言已是回退终点，链中不应出现重复项。
        var chain = _service.BuildFallbackChain(LocaleContext.Default);

        Assert.Single(chain);
        Assert.Equal("zh-CN", chain[0]);
    }

    [Fact]
    public void BuildFallbackChain_NullLocale_TreatsAsDefault()
    {
        var chain = _service.BuildFallbackChain(null);

        Assert.Single(chain);
        Assert.Equal("zh-CN", chain[0]);
    }

    [Fact]
    public void BuildFallbackChain_AlwaysTerminatesAtDefault()
    {
        // 不变量：任何语言的回退链终点都必须是平台默认语言，避免标签查找返回空。
        foreach (var locale in _service.SupportedLocales)
        {
            var chain = _service.BuildFallbackChain(locale);
            Assert.Equal("zh-CN", chain[^1]);
        }
    }

    [Fact]
    public void GetString_BeforeResourceWiring_ReturnsKey()
    {
        // P5.1 阶段资源尚未接入：返回键名本身，保证调用方无需判空（P5.3 后替换为真实文案）。
        Assert.Equal("Common.Save", _service.GetString("Common.Save"));
        Assert.Equal(string.Empty, _service.GetString("   "));
    }

    [Fact]
    public void SupportedLocales_ContainsP5FirstBatch()
    {
        var cultures = _service.SupportedLocales.Select(l => l.Culture).ToList();

        Assert.Contains("zh-CN", cultures);
        Assert.Contains("zh-TW", cultures);
        Assert.Contains("en-US", cultures);
        Assert.Contains("ja-JP", cultures);
        Assert.Contains("ko-KR", cultures);
    }
}
