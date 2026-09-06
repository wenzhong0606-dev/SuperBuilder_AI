using System;
using System.Collections.Generic;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Services.BI;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M5-02：统一字段解析规则单元测试。
/// 验证 Understanding/Builder/Validator 三阶段共享的字段引用词汇与单一匹配算法行为一致。
/// </summary>
public class QueryPlanUnifiedFieldResolutionTests
{
    #region 词汇工厂

    [Fact]
    public void SemanticFieldReference_FromMetric_捕获种类与物理列()
    {
        var m = new QueryMetric { SemanticText = "入库数量", Field = "inbound_qty" };
        var r = SemanticFieldReference.From(m);

        Assert.Equal(SemanticKind.Metric, r.Kind);
        Assert.Equal("入库数量", r.SemanticText);
        Assert.Equal("inbound_qty", r.PhysicalColumn);
        Assert.True(r.HasToken);
    }

    [Fact]
    public void SemanticFieldReference_FromDimension_物理列取ColumnName且携带ColumnId()
    {
        var d = new QueryDimension { SemanticText = "供应商", ColumnName = "es_supplier_code", MetadataColumnId = 42 };
        var r = SemanticFieldReference.From(d);

        Assert.Equal(SemanticKind.Dimension, r.Kind);
        Assert.Equal("供应商", r.SemanticText);
        Assert.Equal("es_supplier_code", r.PhysicalColumn);
        Assert.Equal(42L, r.ColumnId);
    }

    [Fact]
    public void SemanticFieldReference_FromFilter_捕获种类()
    {
        var f = new QueryFilter { SemanticText = "入库日期", Field = "inbound_date" };
        var r = SemanticFieldReference.From(f);

        Assert.Equal(SemanticKind.Filter, r.Kind);
        Assert.Equal("入库日期", r.SemanticText);
        Assert.Equal("inbound_date", r.PhysicalColumn);
    }

    [Fact]
    public void SemanticFieldReference_FromPlan_与From保持一致()
    {
        var m = new QueryMetric { SemanticText = "金额", Field = "amount" };
        Assert.Equal(SemanticFieldReference.From(m).PhysicalColumn, SemanticFieldReference.FromPlan(m).PhysicalColumn);
    }

    [Fact]
    public void SemanticFieldReference_空白引用_HasToken为false()
    {
        var r = SemanticFieldReference.Metric("", "   ");
        Assert.False(r.HasToken);
    }

    #endregion

    #region 统一匹配算法

    [Fact]
    public void Match_P1_SemanticText精确匹配_返回对应索引()
    {
        var bindings = new List<SemanticFieldBinding>
        {
            SemanticFieldBinding.From(new QueryPlanMetricResolution { SemanticText = "数量", Column = "qty", ColumnId = 1 }),
            SemanticFieldBinding.From(new QueryPlanMetricResolution { SemanticText = "金额", Column = "amt", ColumnId = 2 }),
        };
        var runtime = SemanticFieldReference.Metric("金额", "amt");

        var idx = SemanticFieldBindingMatcher.Match(runtime, bindings, new HashSet<int>(), 0);

        Assert.Equal(1, idx);
    }

    [Fact]
    public void Match_P2_物理列匹配绑定SemanticText()
    {
        var bindings = new List<SemanticFieldBinding>
        {
            SemanticFieldBinding.From(new QueryPlanMetricResolution { SemanticText = "数量", Column = "qty", ColumnId = 1 }),
        };
        // 运行时仅有物理列，且该物理列恰好等于某绑定的 SemanticText。
        var runtime = SemanticFieldReference.Metric(null, "数量");

        var idx = SemanticFieldBindingMatcher.Match(runtime, bindings, new HashSet<int>(), 0);

        Assert.Equal(0, idx);
    }

    [Fact]
    public void Match_P2_物理列匹配绑定物理列()
    {
        var bindings = new List<SemanticFieldBinding>
        {
            SemanticFieldBinding.From(new QueryPlanMetricResolution { SemanticText = "数量", Column = "qty", ColumnId = 1 }),
        };
        var runtime = SemanticFieldReference.Metric(null, "qty");

        var idx = SemanticFieldBindingMatcher.Match(runtime, bindings, new HashSet<int>(), 0);

        Assert.Equal(0, idx);
    }

    [Fact]
    public void Match_P3_维度SemanticText匹配绑定物理列_互换情形()
    {
        var bindings = new List<SemanticFieldBinding>
        {
            SemanticFieldBinding.From(new QueryPlanDimensionResolution { SemanticText = "供应商", Column = "es_supplier_code", ColumnId = 7 }),
        };
        // 运行时语义文本等于绑定的物理列（语义↔物理互换），且物理列非空。
        var runtime = SemanticFieldReference.Dimension("es_supplier_code", "es_supplier_code");

        var idx = SemanticFieldBindingMatcher.Match(runtime, bindings, new HashSet<int>(), 0);

        Assert.Equal(0, idx);
    }

    [Fact]
    public void Match_位置回退_当SemanticText无匹配但HasToken()
    {
        var bindings = new List<SemanticFieldBinding>
        {
            SemanticFieldBinding.From(new QueryPlanMetricResolution { SemanticText = "数量", Column = "qty", ColumnId = 1 }),
            SemanticFieldBinding.From(new QueryPlanMetricResolution { SemanticText = "金额", Column = "amt", ColumnId = 2 }),
        };
        // 运行时语义与所有绑定均不匹配，但确有可识别引用（物理列），位置 1 可用。
        var runtime = SemanticFieldReference.Metric("其它", "other");

        var idx = SemanticFieldBindingMatcher.Match(runtime, bindings, new HashSet<int>(), 1);

        Assert.Equal(1, idx);
    }

    [Fact]
    public void Match_空引用_位置回退被HasToken守卫拦截_返回负一()
    {
        var bindings = new List<SemanticFieldBinding>
        {
            SemanticFieldBinding.From(new QueryPlanMetricResolution { SemanticText = "数量", Column = "qty", ColumnId = 1 }),
        };
        // 无任何可识别引用的运行时项（旧的"幽灵维度"来源）不得位置回退绑定到无关项。
        var runtime = SemanticFieldReference.Metric("", "  ");

        var idx = SemanticFieldBindingMatcher.Match(runtime, bindings, new HashSet<int>(), 0);

        Assert.Equal(-1, idx);
    }

    [Fact]
    public void Match_已消费绑定_跳过并匹配下一条()
    {
        var bindings = new List<SemanticFieldBinding>
        {
            SemanticFieldBinding.From(new QueryPlanMetricResolution { SemanticText = "数量", Column = "qty", ColumnId = 1 }),
            SemanticFieldBinding.From(new QueryPlanMetricResolution { SemanticText = "数量", Column = "qty2", ColumnId = 2 }),
        };
        var runtime = SemanticFieldReference.Metric("数量", "qty2");
        var used = new HashSet<int> { 0 };

        var idx = SemanticFieldBindingMatcher.Match(runtime, bindings, used, 1);

        // 索引 0 已被消费，应匹配到索引 1。
        Assert.Equal(1, idx);
    }

    [Fact]
    public void Match_1比1不同语义_各自匹配自身不串扰()
    {
        var bindings = new List<SemanticFieldBinding>
        {
            SemanticFieldBinding.From(new QueryPlanFilterResolution { SemanticText = "入库日期", Column = "inbound_date", ColumnId = 1 }),
            SemanticFieldBinding.From(new QueryPlanFilterResolution { SemanticText = "供应商", Column = "supplier", ColumnId = 2 }),
        };
        var r0 = SemanticFieldReference.Filter("入库日期", "inbound_date");
        var r1 = SemanticFieldReference.Filter("供应商", "supplier");

        Assert.Equal(0, SemanticFieldBindingMatcher.Match(r0, bindings, new HashSet<int>(), 0));
        Assert.Equal(1, SemanticFieldBindingMatcher.Match(r1, bindings, new HashSet<int>(), 1));
    }

    [Fact]
    public void CanResolve_与Match结论一致()
    {
        var bindings = new List<SemanticFieldBinding>
        {
            SemanticFieldBinding.From(new QueryPlanMetricResolution { SemanticText = "数量", Column = "qty", ColumnId = 1 }),
        };
        Assert.True(SemanticFieldBindingMatcher.CanResolve(SemanticFieldReference.Metric("数量", "qty"), bindings));
        Assert.False(SemanticFieldBindingMatcher.CanResolve(SemanticFieldReference.Metric("", "  "), bindings));
    }

    #endregion

    #region Validator 物理解析一致性

    [Fact]
    public void IsPhysicallyResolved_列名匹配_返回true()
    {
        var columns = new List<MetadataColumn> { new() { ColumnName = "inbound_qty" } };
        var runtime = SemanticFieldReference.Metric("入库数量", "inbound_qty");

        Assert.True(SemanticFieldBindingMatcher.IsPhysicallyResolved(runtime, columns));
    }

    [Fact]
    public void IsPhysicallyResolved_列名不匹配_返回false()
    {
        var columns = new List<MetadataColumn> { new() { ColumnName = "other_col" } };
        var runtime = SemanticFieldReference.Metric("入库数量", "inbound_qty");

        Assert.False(SemanticFieldBindingMatcher.IsPhysicallyResolved(runtime, columns));
    }

    [Fact]
    public void IsPhysicallyResolved_空白物理列_返回false()
    {
        var columns = new List<MetadataColumn> { new() { ColumnName = "inbound_qty" } };
        var runtime = SemanticFieldReference.Metric("入库数量", "");

        Assert.False(SemanticFieldBindingMatcher.IsPhysicallyResolved(runtime, columns));
    }

    [Fact]
    public void 三阶段词汇一致_同一指标Builder匹配与Validator物理解析结论相同()
    {
        // 同一指标既能被 Builder 的权威绑定匹配，也能被 Validator 的物理列集合解析到。
        var metric = new QueryMetric { SemanticText = "入库数量", Field = "inbound_qty" };
        var bindings = new List<SemanticFieldBinding>
        {
            SemanticFieldBinding.From(new QueryPlanMetricResolution { SemanticText = "入库数量", Column = "inbound_qty", ColumnId = 1 }),
        };
        var columns = new List<MetadataColumn> { new() { ColumnName = "inbound_qty" } };

        var builderResolved = SemanticFieldBindingMatcher.CanResolve(SemanticFieldReference.From(metric), bindings);
        var validatorResolved = SemanticFieldBindingMatcher.IsPhysicallyResolved(SemanticFieldReference.FromPlan(metric), columns);

        Assert.True(builderResolved);
        Assert.True(validatorResolved);
        Assert.Equal(builderResolved, validatorResolved);
    }

    #endregion
}
