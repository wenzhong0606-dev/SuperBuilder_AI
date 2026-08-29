using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Platform;
using SuperBuilder_AI.Models.Localization;
using SuperBuilder_AI.Services.Platform;
using Xunit;

namespace SuperBuilder_AI.Tests.Platform;

/// <summary>
/// P5.2 业务语义多语言标签的解析与隔离单测（EF Core SQLite 内存库，离线可还原）。
///
/// 覆盖：
///   1. 回退链解析：精确文化 → 语言段 → 平台默认语言；
///   2. 同义词多值解析（按链优先级 + SortOrder）；
///   3. 写入归一化（<c>zh_TW</c> → <c>zh-TW</c>）与幂等 upsert；
///   4. 租户隔离：租户私有标签互不可见，但全局共享标签（<c>TenantId=0</c>）对本租户可见。
/// </summary>
public class SemanticLabelServiceTests
{
    private const string Concept = SemanticConceptTypes.MetadataSemantic;
    private const long ConceptId = 1910; // 与生产库中 come_time 语义同 Id，仅作测试取值

    private static (SuperBIContext ctx, SqliteConnection conn, SemanticLabelService service) Create()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SuperBIContext>()
            .UseSqlite(connection)
            .Options;
        var ctx = new SuperBIContext(options);
        ctx.Database.EnsureCreated();

        var localization = new LocalizationService();
        return (ctx, connection, new SemanticLabelService(ctx, localization));
    }

    private static async Task<SemanticLabel> AddLabel(
        SemanticLabelService service,
        string culture,
        string value,
        string kind = SemanticLabelKinds.DisplayName,
        long tenantId = 0,
        int sortOrder = 0)
        => await service.UpsertAsync(new UpsertSemanticLabelRequest(
            tenantId, Concept, ConceptId, culture, kind, value, "Manual", sortOrder));

    [Fact]
    public async Task Resolve_ExactCulture_IsPreferred()
    {
        var (ctx, conn, svc) = Create();
        await using var _ = conn;
        await AddLabel(svc, "zh-CN", "销售额");
        await AddLabel(svc, "en-US", "Sales Amount");

        Assert.Equal("销售额", await svc.ResolveAsync(Concept, ConceptId, SemanticLabelKinds.DisplayName, "zh-CN"));
        Assert.Equal("Sales Amount", await svc.ResolveAsync(Concept, ConceptId, SemanticLabelKinds.DisplayName, "en-US"));

        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task Resolve_FallsBackToLanguageSegment()
    {
        var (ctx, conn, svc) = Create();
        await using var _ = conn;
        // 只有纯语言段 "ja" 的译文，查询 ja-JP 应回退命中。
        await AddLabel(svc, "ja", "売上高");

        Assert.Equal("売上高", await svc.ResolveAsync(Concept, ConceptId, SemanticLabelKinds.DisplayName, "ja-JP"));

        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task Resolve_FallsBackToDefaultLanguage()
    {
        var (ctx, conn, svc) = Create();
        await using var _ = conn;
        // 只有默认语言译文；查询 ko-KR 应回退到 zh-CN 而非返回 null。
        await AddLabel(svc, "zh-CN", "销售额");

        Assert.Equal("销售额", await svc.ResolveAsync(Concept, ConceptId, SemanticLabelKinds.DisplayName, "ko-KR"));

        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task Resolve_NoLabels_ReturnsNull()
    {
        var (ctx, conn, svc) = Create();
        await using var _ = conn;

        // 软降级：标签缺失是常态，返回 null 而非抛异常阻断主链路。
        Assert.Null(await svc.ResolveAsync(Concept, ConceptId, SemanticLabelKinds.DisplayName, "en-US"));

        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task ResolveSynonyms_ReturnsOrderedDistinctValues()
    {
        var (ctx, conn, svc) = Create();
        await using var _ = conn;
        await AddLabel(svc, "zh-CN", "营业额", SemanticLabelKinds.Synonym, sortOrder: 1);
        await AddLabel(svc, "zh-CN", "营收", SemanticLabelKinds.Synonym, sortOrder: 0);
        await AddLabel(svc, "zh-CN", "营收", SemanticLabelKinds.Synonym, sortOrder: 2); // 重复值应被去重

        var synonyms = await svc.ResolveSynonymsAsync(Concept, ConceptId, "zh-CN");

        Assert.Equal(new[] { "营收", "营业额" }, synonyms);

        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task Upsert_NormalizesCulture_AndIsIdempotent()
    {
        var (ctx, conn, svc) = Create();
        await using var _ = conn;
        var first = await AddLabel(svc, "zh_TW", "營業額");
        Assert.Equal("zh-TW", first.Culture);

        // 同键再次 upsert：更新值而非插入新行。
        var second = await AddLabel(svc, "zh-TW", "營業額(修)");
        Assert.Equal(first.Id, second.Id);
        Assert.Equal("營業額(修)", second.Value);
        Assert.Equal(1, await ctx.SemanticLabels.CountAsync());

        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task Upsert_RejectsEmptyConceptTypeOrValue()
    {
        var (ctx, conn, svc) = Create();
        await using var _ = conn;

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.UpsertAsync(new UpsertSemanticLabelRequest(0, "", 1, "zh-CN", SemanticLabelKinds.DisplayName, "x")));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.UpsertAsync(new UpsertSemanticLabelRequest(0, Concept, 1, "zh-CN", SemanticLabelKinds.DisplayName, "  ")));

        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task TenantScope_PrivateLabelOfOtherTenant_IsInvisible()
    {
        var (ctx, conn, svc) = Create();
        await using var _ = conn;
        await AddLabel(svc, "zh-CN", "租户1私有", tenantId: 1);
        await AddLabel(svc, "zh-CN", "租户2私有", tenantId: 2);

        // 开启租户 1 作用域后，租户 2 的私有标签不可见，解析命中租户 1 自己的。
        ctx.ApplyTenantScope(1);
        Assert.Equal("租户1私有", await svc.ResolveAsync(Concept, ConceptId, SemanticLabelKinds.DisplayName, "zh-CN"));

        ctx.ApplyTenantScope(2);
        Assert.Equal("租户2私有", await svc.ResolveAsync(Concept, ConceptId, SemanticLabelKinds.DisplayName, "zh-CN"));

        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task TenantScope_GlobalLabel_StaysVisible()
    {
        var (ctx, conn, svc) = Create();
        await using var _ = conn;
        // 全局共享译文（TenantId=0）必须在任何租户作用域内都可见，
        // 否则开启租户过滤后所有共享译文会集体消失。
        await AddLabel(svc, "zh-CN", "全局译文", tenantId: 0);

        ctx.ApplyTenantScope(7);
        Assert.Equal("全局译文", await svc.ResolveAsync(Concept, ConceptId, SemanticLabelKinds.DisplayName, "zh-CN"));

        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task NoTenantScope_SeesEverything()
    {
        var (ctx, conn, svc) = Create();
        await using var _ = conn;
        await AddLabel(svc, "zh-CN", "T1", tenantId: 1);
        await AddLabel(svc, "zh-CN", "T2", tenantId: 2);

        // 未开启作用域（Golden/系统路径）：过滤为 no-op，全部可见。
        Assert.Equal(2, await ctx.SemanticLabels.CountAsync());

        await ctx.DisposeAsync();
    }
}
