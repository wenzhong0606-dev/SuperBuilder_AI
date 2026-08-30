using System.Collections.Generic;
using System.Linq;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.Dashboard;
using SuperBuilder_AI.Services.BI.Dashboard;
using Xunit;

namespace SuperBuilder_AI.Tests.Dashboard;

/// <summary>
/// P6.1/P6.2 仪表盘 DSL 模型与序列化校验单测（纯内存、离线可断言）。
///
/// 覆盖：
///   1. 六类组件的多态往返（判别式 <c>type</c> 正确读写、派生类字段不丢失）；
///   2. 结构化校验（版本、标题、页面/组件 Id 唯一、枚举取值、取数必填）；
///   3. <strong>红线校验</strong>：文本组件不得携带裸 HTML / 脚本；
///   4. 校验器返回<strong>全部</strong>错误而非首个即止，便于编辑器一次展示。
/// </summary>
public class DashboardDslSerializerTests
{
    private readonly IDashboardDslSerializer _serializer = new DashboardDslSerializer();

    private static DashboardDsl BuildValidDsl()
        => new()
        {
            Code = "sales-overview",
            Title = "销售总览",
            Description = "按月份查看销售趋势",
            Pages = new List<PageDsl>
            {
                new()
                {
                    Id = "p1",
                    Name = "概览",
                    Order = 0,
                    Layout = new LayoutDsl(),
                    Widgets = new List<WidgetDsl>
                    {
                        new KpiWidgetDsl
                        {
                            Id = "w-kpi",
                            Title = "总销售额",
                            ValueField = "销售额",
                            Order = 0,
                            Query = new WidgetQueryDsl
                            {
                                Metrics = new List<MetricDsl> { new() { Field = "销售额", Aggregation = AggregateTypes.Sum } },
                            },
                        },
                        new ChartWidgetDsl
                        {
                            Id = "w-chart",
                            Title = "月度趋势",
                            ChartType = ChartTypes.Line,
                            CategoryField = "月份",
                            ValueFields = new List<string> { "销售额" },
                            Order = 1,
                            Query = new WidgetQueryDsl
                            {
                                Dimensions = new List<string> { "月份" },
                                Metrics = new List<MetricDsl> { new() { Field = "销售额", Aggregation = AggregateTypes.Sum } },
                                Sorts = new List<SortDsl> { new() { Field = "月份", Direction = "ASC" } },
                                Limit = 100,
                            },
                        },
                        new TableWidgetDsl
                        {
                            Id = "w-table",
                            Title = "明细",
                            Order = 2,
                            Columns = new List<TableColumnDsl> { new() { Field = "客户", Header = "客户名称" } },
                            Query = new WidgetQueryDsl { Question = "各客户销售额明细" },
                        },
                        new FilterWidgetDsl
                        {
                            Id = "w-filter",
                            Field = "区域",
                            Control = "select",
                            Order = 3,
                        },
                        new TextWidgetDsl
                        {
                            Id = "w-text",
                            Content = "本页展示销售核心指标。",
                            Order = 4,
                        },
                        new AiInsightWidgetDsl
                        {
                            Id = "w-ai",
                            Prompt = "指出销售额异常波动的原因",
                            MaxInsights = 3,
                            Order = 5,
                            Query = new WidgetQueryDsl { Question = "最近12个月销售额" },
                        },
                    },
                },
            },
            GlobalFilters = new List<FilterDsl>
            {
                new() { Field = "年份", Operator = FilterOperators.Equal, Value = "2026" },
            },
        };

    [Fact]
    public void RoundTrip_ValidDsl_PreservesEverything()
    {
        var original = BuildValidDsl();

        var json = _serializer.Serialize(original);
        Assert.True(_serializer.TryDeserialize(json, out var restored, out var errors), string.Join("; ", errors));

        Assert.NotNull(restored);
        Assert.Equal(original.Title, restored!.Title);
        Assert.Equal(original.Code, restored.Code);
        Assert.Single(restored.Pages);
        Assert.Equal(6, restored.Pages[0].Widgets.Count);
        Assert.Single(restored.GlobalFilters);
    }

    [Fact]
    public void RoundTrip_PreservesWidgetPolymorphicTypes()
    {
        var json = _serializer.Serialize(BuildValidDsl());
        Assert.True(_serializer.TryDeserialize(json, out var restored, out _));

        var actual = restored!.Pages[0].Widgets.Select(w => w.Type).ToList();

        Assert.Equal(
            new[]
            {
                WidgetTypes.Kpi, WidgetTypes.Chart, WidgetTypes.Table,
                WidgetTypes.Filter, WidgetTypes.Text, WidgetTypes.AiInsight,
            },
            actual);
    }

    [Fact]
    public void RoundTrip_ChartSpecificFieldsSurvive()
    {
        var json = _serializer.Serialize(BuildValidDsl());
        Assert.True(_serializer.TryDeserialize(json, out var restored, out _));

        var chart = Assert.IsType<ChartWidgetDsl>(restored!.Pages[0].Widgets.First(w => w.Type == WidgetTypes.Chart));

        Assert.Equal(ChartTypes.Line, chart.ChartType);
        Assert.Equal("月份", chart.CategoryField);
        Assert.Equal(new[] { "销售额" }, chart.ValueFields);
        Assert.True(chart.ShowLegend);
    }

    [Fact]
    public void Serialize_WritesTypeDiscriminator()
    {
        var json = _serializer.Serialize(BuildValidDsl());

        // 判别式必须是 "type"，这是渲染器与前端解释 DSL 的契约。
        Assert.Contains("\"type\": \"kpi\"", json);
        Assert.Contains("\"type\": \"chart\"", json);
        Assert.Contains("\"type\": \"aiInsight\"", json);
    }

    [Fact]
    public void Validate_ValidDsl_HasNoErrors()
    {
        Assert.Empty(_serializer.Validate(BuildValidDsl()));
    }

    [Theory]
    [InlineData("9.9")]
    [InlineData("")]
    [InlineData("abc")]
    public void Validate_UnsupportedVersion_IsRejected(string version)
    {
        var dsl = BuildValidDsl();
        dsl.Version = version;

        var errors = _serializer.Validate(dsl);

        Assert.Contains(errors, e => e.Contains("不支持的 DSL 版本"));
    }

    [Fact]
    public void Validate_EmptyTitle_IsRejected()
    {
        var dsl = BuildValidDsl();
        dsl.Title = "   ";

        Assert.Contains(_serializer.Validate(dsl), e => e.Contains("标题不能为空"));
    }

    [Fact]
    public void Validate_NoPages_IsRejected()
    {
        var dsl = BuildValidDsl();
        dsl.Pages.Clear();

        Assert.Contains(_serializer.Validate(dsl), e => e.Contains("至少需要一个页面"));
    }

    [Fact]
    public void Validate_DuplicatePageId_IsRejected()
    {
        var dsl = BuildValidDsl();
        var clone = BuildValidDsl().Pages[0];
        clone.Id = "p1";
        dsl.Pages.Add(clone);

        Assert.Contains(_serializer.Validate(dsl), e => e.Contains("页面 Id 重复"));
    }

    [Fact]
    public void Validate_DuplicateWidgetId_IsRejected()
    {
        var dsl = BuildValidDsl();
        dsl.Pages[0].Widgets.Add(new TextWidgetDsl { Id = "w-text", Content = "重复" });

        Assert.Contains(_serializer.Validate(dsl), e => e.Contains("组件 Id 重复"));
    }

    [Fact]
    public void Validate_DataWidgetWithoutQuery_IsRejected()
    {
        var dsl = BuildValidDsl();
        // 图表必须有取数定义，否则渲染时无从取数；文本与筛选器可无 Query。
        dsl.Pages[0].Widgets.Add(new ChartWidgetDsl { Id = "w-no-query", ChartType = ChartTypes.Bar });

        Assert.Contains(_serializer.Validate(dsl), e => e.Contains("未定义取数"));
    }

    [Fact]
    public void Validate_TextWidgetWithoutQuery_IsAllowed()
    {
        var dsl = BuildValidDsl();
        dsl.Pages[0].Widgets.Add(new TextWidgetDsl { Id = "w-plain", Content = "说明文字" });

        Assert.DoesNotContain(_serializer.Validate(dsl), e => e.Contains("未定义取数"));
    }

    [Fact]
    public void Validate_UnsupportedChartType_IsRejected()
    {
        var dsl = BuildValidDsl();
        ((ChartWidgetDsl)dsl.Pages[0].Widgets.First(w => w.Type == WidgetTypes.Chart)).ChartType = "radar3d";

        Assert.Contains(_serializer.Validate(dsl), e => e.Contains("图表类型不受支持"));
    }

    [Fact]
    public void Validate_UnsupportedAggregation_IsRejected()
    {
        var dsl = BuildValidDsl();
        var chart = (ChartWidgetDsl)dsl.Pages[0].Widgets.First(w => w.Type == WidgetTypes.Chart);
        chart.Query!.Metrics[0].Aggregation = "MEDIAN";

        Assert.Contains(_serializer.Validate(dsl), e => e.Contains("聚合方式不受支持"));
    }

    [Fact]
    public void Validate_UnsupportedFilterOperator_IsRejected()
    {
        var dsl = BuildValidDsl();
        dsl.GlobalFilters[0].Operator = "~~";

        Assert.Contains(_serializer.Validate(dsl), e => e.Contains("筛选操作符不受支持"));
    }

    [Fact]
    public void Validate_InvalidSortDirection_IsRejected()
    {
        var dsl = BuildValidDsl();
        var chart = (ChartWidgetDsl)dsl.Pages[0].Widgets.First(w => w.Type == WidgetTypes.Chart);
        chart.Query!.Sorts[0].Direction = "ASCENDING";

        Assert.Contains(_serializer.Validate(dsl), e => e.Contains("排序方向不受支持"));
    }

    [Fact]
    public void Validate_NonPositiveLimit_IsRejected()
    {
        var dsl = BuildValidDsl();
        var chart = (ChartWidgetDsl)dsl.Pages[0].Widgets.First(w => w.Type == WidgetTypes.Chart);
        chart.Query!.Limit = 0;

        Assert.Contains(_serializer.Validate(dsl), e => e.Contains("Limit 必须大于 0"));
    }

    [Theory]
    // P6 红线：DSL 只允许结构化文本，绝不承载裸 HTML / 脚本
    [InlineData("<div>销售</div>")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("点<a href=\"javascript:alert(1)\">这里</a>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    public void Validate_RawHtmlInTextWidget_IsRejected(string content)
    {
        var dsl = BuildValidDsl();
        dsl.Pages[0].Widgets.Add(new TextWidgetDsl { Id = "w-html", Content = content });

        Assert.Contains(_serializer.Validate(dsl), e => e.Contains("HTML"));
    }

    [Fact]
    public void Validate_MarkdownText_IsAllowed()
    {
        // Markdown 是结构化文本，不是 HTML，应放行。
        var dsl = BuildValidDsl();
        dsl.Pages[0].Widgets.Add(new TextWidgetDsl
        {
            Id = "w-md",
            Content = "## 说明\n- 第一项\n- 第二项",
        });

        Assert.DoesNotContain(_serializer.Validate(dsl), e => e.Contains("HTML"));
    }

    [Fact]
    public void Validate_ReturnsAllErrorsNotJustFirst()
    {
        // 编辑器体验：一次性拿到全部问题。
        var dsl = BuildValidDsl();
        dsl.Version = "9.9";
        dsl.Title = "";
        dsl.Pages.Clear();

        Assert.True(_serializer.Validate(dsl).Count >= 3);
    }

    [Fact]
    public void TryDeserialize_MalformedJson_ReturnsFalseWithReason()
    {
        Assert.False(_serializer.TryDeserialize("{ not json", out _, out var errors));
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void TryDeserialize_EmptyJson_ReturnsFalse()
    {
        Assert.False(_serializer.TryDeserialize("   ", out _, out _));
        Assert.False(_serializer.TryDeserialize(null, out _, out _));
    }

    [Fact]
    public void TryDeserialize_UnknownWidgetType_ReturnsFalse()
    {
        // 未知组件类型：多态反序列化失败必须被捕获，而不是抛出未处理异常。
        var json = """
        {
          "version": "1.0",
          "title": "t",
          "pages": [{
            "id": "p1",
            "name": "n",
            "widgets": [{ "type": "hologram", "id": "w1" }]
          }]
        }
        """;

        Assert.False(_serializer.TryDeserialize(json, out _, out var errors));
        Assert.NotEmpty(errors);
    }
}
