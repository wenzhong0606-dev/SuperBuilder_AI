using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Services.BI;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M5-01 规范化语义模型：统一 ID 契约 + 默认透传解析器 + 模型查找。
/// 全部为纯单元逻辑，不依赖 LLM / 数据库 / 真实元数据。
/// </summary>
public class QueryPlanCanonicalSemanticModelTests
{
    [Fact]
    public void CanonicalSemanticId_CanonicalString_UsesKindAndKey()
    {
        var id = CanonicalSemanticId.Metric("revenue");
        Assert.Equal("METRIC:revenue", id.Canonical);
        Assert.Equal(SemanticKind.Metric, id.Kind);
        Assert.Equal("revenue", id.Key);
    }

    [Fact]
    public void CanonicalSemanticId_FactoryMethods_SetCorrectKind()
    {
        Assert.Equal(SemanticKind.Entity, CanonicalSemanticId.Entity("e").Kind);
        Assert.Equal(SemanticKind.Dimension, CanonicalSemanticId.Dimension("d").Kind);
        Assert.Equal(SemanticKind.Filter, CanonicalSemanticId.Filter("f").Kind);
        Assert.Equal(SemanticKind.Binding, CanonicalSemanticId.Binding("b").Kind);
    }

    [Fact]
    public void CanonicalSemanticId_Parse_RoundTrips()
    {
        var id = CanonicalSemanticId.Entity("inbound_receipt");
        var parsed = CanonicalSemanticId.Parse(id.Canonical);
        Assert.Equal(id, parsed);
    }

    [Fact]
    public void CanonicalSemanticId_Parse_IsCaseInsensitiveOnKind()
    {
        var parsed = CanonicalSemanticId.Parse("metric:revenue");
        Assert.Equal(SemanticKind.Metric, parsed.Kind);
        Assert.Equal("revenue", parsed.Key);
    }

    [Fact]
    public void CanonicalSemanticId_EmptyKey_Throws()
    {
        Assert.Throws<ArgumentException>(() => new CanonicalSemanticId(SemanticKind.Metric, "   "));
        Assert.Throws<FormatException>(() => CanonicalSemanticId.Parse(""));
        Assert.Throws<FormatException>(() => CanonicalSemanticId.Parse("METRIC"));
    }

    [Fact]
    public void CanonicalSemanticId_ValueEquality()
    {
        Assert.Equal(CanonicalSemanticId.Metric("x"), CanonicalSemanticId.Metric("x"));
        Assert.NotEqual(CanonicalSemanticId.Metric("x"), CanonicalSemanticId.Dimension("x"));
    }

    [Fact]
    public async Task PassThroughResolver_ResolvesNameAsKey_WithCorrectKind()
    {
        ICanonicalSemanticResolver resolver = new PassThroughCanonicalResolver();
        var id = await resolver.ResolveAsync(SemanticKind.Entity, "  入库凭证  ", CancellationToken.None);
        Assert.NotNull(id);
        Assert.Equal(SemanticKind.Entity, id!.Value.Kind);
        Assert.Equal("入库凭证", id.Value.Key);
    }

    [Fact]
    public async Task PassThroughResolver_NullOrWhitespace_ReturnsNull()
    {
        ICanonicalSemanticResolver resolver = new PassThroughCanonicalResolver();
        Assert.Null(await resolver.ResolveAsync(SemanticKind.Metric, "", CancellationToken.None));
        Assert.Null(await resolver.ResolveAsync(SemanticKind.Metric, "  ", CancellationToken.None));
    }

    [Fact]
    public async Task PassThroughResolver_GetModel_ReturnsEmpty()
    {
        ICanonicalSemanticResolver resolver = new PassThroughCanonicalResolver();
        var model = await resolver.GetModelAsync(CancellationToken.None);
        Assert.NotNull(model);
        Assert.Empty(model.Entities);
        Assert.Empty(model.Metrics);
    }

    [Fact]
    public void CanonicalSemanticModel_FindId_MatchesNameAndAliasAcrossKinds()
    {
        var model = new CanonicalSemanticModel
        {
            Entities = new List<CanonicalEntity>
            {
                new() { Id = CanonicalSemanticId.Entity("receipt"), Name = "入库凭证", Aliases = new[] { "入库单" } }
            },
            Metrics = new List<CanonicalMetric>
            {
                new() { Id = CanonicalSemanticId.Metric("amount"), Name = "金额", Aliases = new[] { "总价" } }
            }
        };

        var byName = model.FindId("入库凭证");
        Assert.NotNull(byName);
        Assert.Equal(CanonicalSemanticId.Entity("receipt"), byName!.Value);

        var byAlias = model.FindId("入库单");
        Assert.NotNull(byAlias);
        Assert.Equal(CanonicalSemanticId.Entity("receipt"), byAlias!.Value);

        var byMetricAlias = model.FindId("总价");
        Assert.NotNull(byMetricAlias);
        Assert.Equal(CanonicalSemanticId.Metric("amount"), byMetricAlias!.Value);

        Assert.Null(model.FindId("不存在"));
        Assert.Null(model.FindId("   "));
    }

    [Fact]
    public void CanonicalSemanticModel_Empty_FindId_ReturnsNull()
    {
        Assert.Null(CanonicalSemanticModel.Empty.FindId("anything"));
    }
}
