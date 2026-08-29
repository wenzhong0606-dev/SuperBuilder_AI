using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models.Localization;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.BI.Planning;
using SuperBuilder_AI.Services.Platform;
using Xunit;

namespace SuperBuilder_AI.Tests.BiQuery;

/// <summary>
/// P5.4 AI 意图语言无关化单测。
///
/// 两类断言：
///   1. <strong>门控不变量</strong>：默认语言 / Invariant / null 一律返回空指令 —— 提示词
///      因此与 P5 之前逐字节一致，这是 Golden 不受影响的根本保证；
///   2. <strong>跨语言语义一致性</strong>：同一概念的不同语言问句解析到同一个语义概念 Id，
///      即 <c>Intent → Semantic Concept</c> 与提问语言无关。
/// </summary>
public class QueryIntentLocaleDirectiveTests
{
    [Fact]
    public void Build_DefaultLocale_ReturnsEmpty_GatingGuarantee()
    {
        // 零回归核心断言：默认语言不得追加任何指令，否则提示词变化会波及 Golden。
        Assert.Equal(string.Empty, QueryIntentLocaleDirective.Build(LocaleContext.Default));
        Assert.Equal(string.Empty, QueryIntentLocaleDirective.Build(null));
    }

    [Fact]
    public void Build_InvariantLocale_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, QueryIntentLocaleDirective.Build(LocaleContext.Invariant));
    }

    [Theory]
    [InlineData("zh-TW", "中文")]
    [InlineData("en-US", "英文")]
    [InlineData("ja-JP", "日文")]
    [InlineData("ko-KR", "韩文")]
    public void Build_NonDefaultLocale_ContainsLanguageAndNormalizationRule(string culture, string languageName)
    {
        var directive = QueryIntentLocaleDirective.Build(LocaleContext.FromCulture(culture));

        Assert.NotEqual(string.Empty, directive);
        Assert.Contains(languageName, directive);
        Assert.Contains(culture, directive);
        // 归一化目标必须是平台默认语言（简体中文），否则下游无法匹配 Metadata 语义
        Assert.Contains("简体中文", directive);
    }

    [Fact]
    public void Build_NonDefaultLocale_ForbidsRetainingSourceVocabulary()
    {
        // 关键约束：禁止把原始语言词汇当作字段语义名输出。
        var directive = QueryIntentLocaleDirective.Build(LocaleContext.FromCulture("en-US"));

        Assert.Contains("禁止", directive);
        Assert.Contains("字段语义名", directive);
    }

    [Fact]
    public void Build_UnregisteredLanguage_FallsBackToCode()
    {
        // 未登记语言（如 de-DE）也应生成指令，语言名回退为代码本身，不至于退化为空。
        var directive = QueryIntentLocaleDirective.Build(LocaleContext.FromCulture("de-DE"));

        Assert.NotEqual(string.Empty, directive);
        Assert.Contains("de", directive);
    }

    [Fact]
    public async Task CrossLanguage_QuestionsResolveToSameSemanticConcept()
    {
        // P5 目标验证：Intent → Semantic Concept 与提问语言无关。
        // 同一概念（入库日期 / Inbound Date / 入荷日）登记三种语言标签后，
        // 三种语言的问句都应命中同一个 semanticId。
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var _ = connection;

        var options = new DbContextOptionsBuilder<SuperBIContext>()
            .UseSqlite(connection)
            .Options;
        await using var ctx = new SuperBIContext(options);
        ctx.Database.EnsureCreated();

        const long semanticId = 1910;
        var labels = new[]
        {
            ("zh-CN", "入库日期"),
            ("en-US", "Inbound Date"),
            ("ja-JP", "入荷日"),
            ("ko-KR", "입고일자"),
        };
        var order = 0;
        foreach (var (culture, value) in labels)
        {
            ctx.SemanticLabels.Add(new SemanticLabel
            {
                TenantId = 0,
                ConceptType = SemanticConceptTypes.MetadataSemantic,
                ConceptId = semanticId,
                Culture = culture,
                LabelKind = SemanticLabelKinds.DisplayName,
                Value = value,
                Source = "Manual",
                SortOrder = order++,
            });
        }
        await ctx.SaveChangesAsync();

        var localization = new LocalizationService();
        var recall = new SemanticLabelRecallService(ctx, localization);

        var questions = new Dictionary<string, string>
        {
            ["zh-CN"] = "按月份统计入库日期分布",
            ["en-US"] = "Show Inbound Date distribution by month",
            ["ja-JP"] = "月別の入荷日の分布を表示",
            ["ko-KR"] = "월별 입고일자 분포 조회",
        };

        foreach (var (culture, question) in questions)
        {
            // 注意：默认语言（zh-CN）会被门控短路，故此处用非默认语言验证跨语言一致性；
            // zh-CN 单独走默认路径（见下一条测试）。
            var locale = localization.Resolve(culture);
            if (locale.IsDefault) continue;

            var hits = await recall.MatchAsync(question, locale);

            Assert.True(hits.Count > 0, $"{culture} 问句未命中任何标签：{question}");
            Assert.All(hits, h => Assert.Equal(semanticId, h.SemanticId));
        }
    }

    [Fact]
    public async Task DefaultLanguageQuestion_UsesGatedPath_UnaffectedByLabels()
    {
        // 默认语言走门控路径：即使存在完全命中的中文标签，召回也不参与（由向量检索独立完成）。
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var _ = connection;

        var options = new DbContextOptionsBuilder<SuperBIContext>()
            .UseSqlite(connection)
            .Options;
        await using var ctx = new SuperBIContext(options);
        ctx.Database.EnsureCreated();

        ctx.SemanticLabels.Add(new SemanticLabel
        {
            TenantId = 0,
            ConceptType = SemanticConceptTypes.MetadataSemantic,
            ConceptId = 1910,
            Culture = "zh-CN",
            LabelKind = SemanticLabelKinds.DisplayName,
            Value = "入库日期",
        });
        await ctx.SaveChangesAsync();

        var recall = new SemanticLabelRecallService(ctx, new LocalizationService());

        Assert.Empty(await recall.MatchAsync("按月份统计入库日期分布", LocaleContext.Default));
    }
}
