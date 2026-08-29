using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Platform;
using SuperBuilder_AI.Models.Localization;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.Platform;
using Xunit;

namespace SuperBuilder_AI.Tests.Platform;

/// <summary>
/// P5.3 多语言标签召回单测（EF Core SQLite 内存库，离线可还原）。
///
/// 最关键的是<strong>门控不变量</strong>：默认语言 / Invariant / null 一律返回空集合。
/// 这是 P5 零回归的保证——Golden 与既有中文链路从不执行标签匹配逻辑。
/// </summary>
public class SemanticLabelRecallServiceTests
{
    private static (SuperBIContext ctx, SqliteConnection conn, SemanticLabelRecallService svc) Create()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SuperBIContext>()
            .UseSqlite(connection)
            .Options;
        var ctx = new SuperBIContext(options);
        ctx.Database.EnsureCreated();

        return (ctx, connection, new SemanticLabelRecallService(ctx, new LocalizationService()));
    }

    private static async Task AddLabel(
        SuperBIContext ctx,
        long semanticId,
        string culture,
        string value,
        string kind = SemanticLabelKinds.DisplayName)
    {
        ctx.SemanticLabels.Add(new SemanticLabel
        {
            TenantId = 0,
            ConceptType = SemanticConceptTypes.MetadataSemantic,
            ConceptId = semanticId,
            Culture = culture,
            LabelKind = kind,
            Value = value,
            Source = "Manual",
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Match_DefaultLocale_ReturnsEmpty_GatingGuarantee()
    {
        var (ctx, conn, svc) = Create();
        await using var _ = conn;
        await AddLabel(ctx, 1, "zh-CN", "销售额");

        // 零回归核心断言：默认语言即使有完全命中的标签也必须返回空。
        Assert.Empty(await svc.MatchAsync("销售额是多少", LocaleContext.Default));
        Assert.Empty(await svc.MatchAsync("销售额是多少", null));

        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task Match_InvariantLocale_ReturnsEmpty()
    {
        var (ctx, conn, svc) = Create();
        await using var _ = conn;
        await AddLabel(ctx, 1, "zh-CN", "销售额");

        Assert.Empty(await svc.MatchAsync("销售额", LocaleContext.Invariant));

        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task Match_ResolvedFromNullCulture_ReturnsEmpty()
    {
        // 模拟 Golden/未指定语言的真实调用方式：Resolve(null) -> Default
        var (ctx, conn, svc) = Create();
        await using var _ = conn;
        await AddLabel(ctx, 1, "zh-CN", "销售额");

        var locale = new LocalizationService().Resolve(null);
        Assert.Empty(await svc.MatchAsync("销售额", locale));

        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task Match_NonDefaultLocale_HitsRegisteredLabel()
    {
        var (ctx, conn, svc) = Create();
        await using var _ = conn;
        await AddLabel(ctx, 100, "en-US", "Sales Amount");

        var hits = await svc.MatchAsync("What is the total Sales Amount?", new LocalizationService().Resolve("en-US"));

        var hit = Assert.Single(hits);
        Assert.Equal(100, hit.SemanticId);
        Assert.Equal("Sales Amount", hit.MatchedLabel);
        Assert.Equal("en-US", hit.Culture);

        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task Match_IsCaseInsensitive()
    {
        var (ctx, conn, svc) = Create();
        await using var _ = conn;
        await AddLabel(ctx, 100, "en-US", "Sales Amount");

        var hits = await svc.MatchAsync("SALES AMOUNT total", new LocalizationService().Resolve("en-US"));

        Assert.Single(hits);

        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task Match_PrefersLongerLabel_AndNormalizesStrength()
    {
        var (ctx, conn, svc) = Create();
        await using var _ = conn;
        // 两个标签都能命中同一问句，长标签更具体 -> 强度 1.0
        await AddLabel(ctx, 7, "en-US", "Inbound");
        await AddLabel(ctx, 8, "en-US", "Inbound Date");

        var hits = await svc.MatchAsync("Show inbound date by month", new LocalizationService().Resolve("en-US"));

        // 同一语义概念只保留一条（此处为不同概念，各一条）
        Assert.Equal(2, hits.Count);
        var top = hits[0];
        Assert.Equal(8, top.SemanticId);
        Assert.Equal("Inbound Date", top.MatchedLabel);
        Assert.Equal(1.0, top.Strength, 6);
        // 短标签按最长命中归一化："Inbound"(7) / "Inbound Date"(12)
        Assert.Equal(7d / 12d, hits[1].Strength, 6);

        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task Match_SameConceptKeepsStrongestHitOnly()
    {
        var (ctx, conn, svc) = Create();
        await using var _ = conn;
        await AddLabel(ctx, 7, "en-US", "Inbound", SemanticLabelKinds.DisplayName);
        await AddLabel(ctx, 7, "en-US", "Inbound Date", SemanticLabelKinds.Synonym);

        var hits = await svc.MatchAsync("inbound date count", new LocalizationService().Resolve("en-US"));

        var hit = Assert.Single(hits);
        Assert.Equal("Inbound Date", hit.MatchedLabel);

        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task Match_IgnoresTooShortLabels()
    {
        var (ctx, conn, svc) = Create();
        await using var _ = conn;
        // 单字符标签在英文里子串误命中率极高，应被过滤
        await AddLabel(ctx, 9, "en-US", "a");

        Assert.Empty(await svc.MatchAsync("a very long question", new LocalizationService().Resolve("en-US")));

        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task Match_FallsBackToDefaultCultureLabels()
    {
        var (ctx, conn, svc) = Create();
        await using var _ = conn;
        // 目标语言无标签，但有默认语言标签 -> 回退链应当生效
        await AddLabel(ctx, 55, "zh-CN", "入库日期");

        var hits = await svc.MatchAsync("入库日期 分布", new LocalizationService().Resolve("ja-JP"));

        var hit = Assert.Single(hits);
        Assert.Equal(55, hit.SemanticId);
        Assert.Equal("zh-CN", hit.Culture);

        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task Match_NoHitOrEmptyQuestion_ReturnsEmpty()
    {
        var (ctx, conn, svc) = Create();
        await using var _ = conn;
        await AddLabel(ctx, 100, "en-US", "Sales Amount");

        Assert.Empty(await svc.MatchAsync("totally unrelated question", new LocalizationService().Resolve("en-US")));
        Assert.Empty(await svc.MatchAsync("   ", new LocalizationService().Resolve("en-US")));
        Assert.Empty(await svc.MatchAsync(string.Empty, new LocalizationService().Resolve("en-US")));

        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task Match_OnlyConsidersMetadataSemanticConcepts()
    {
        var (ctx, conn, svc) = Create();
        await using var _ = conn;
        // 业务实体标签不应参与字段语义召回
        ctx.SemanticLabels.Add(new SemanticLabel
        {
            TenantId = 0,
            ConceptType = SemanticConceptTypes.BusinessEntity,
            ConceptId = 100,
            Culture = "en-US",
            LabelKind = SemanticLabelKinds.DisplayName,
            Value = "Sales Amount",
        });
        await ctx.SaveChangesAsync();

        Assert.Empty(await svc.MatchAsync("Sales Amount", new LocalizationService().Resolve("en-US")));

        await ctx.DisposeAsync();
    }
}
