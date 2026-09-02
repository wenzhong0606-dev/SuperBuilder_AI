using System;
using System.Collections.Generic;
using System.Linq;
using Bunit;
using SuperBuilder_AI.Components.Components.Shared.UI;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>S5-5 · SbDataTable 组件单测（bUnit 渲染 + 排序）。</summary>
public class SbDataTableTests : BunitContext
{
    private sealed record Sample(string Name, int Score);

    private static IReadOnlyList<SbColumn<Sample>> Columns() => new SbColumn<Sample>[]
    {
        SbCol.Of<Sample>("名称", m => m.Name),
        SbCol.Of<Sample>("分数", m => m.Score.ToString(), sortable: true)
    };

    [Fact]
    public void Renders_All_Rows_And_Headers()
    {
        var items = new[]
        {
            new Sample("张三", 30),
            new Sample("李四", 10)
        };

        var cut = Render<SbDataTable<Sample>>(p => p
            .Add(x => x.Items, items)
            .Add(x => x.Columns, Columns()));

        var headers = cut.FindAll("thead th").Select(t => t.TextContent.Trim()).ToArray();
        Assert.Equal(new[] { "名称", "分数" }, headers[..2]);

        var rows = cut.FindAll("tbody tr");
        Assert.Equal(2, rows.Count);
        Assert.Contains("张三", rows[0].TextContent);
        Assert.Contains("李四", rows[1].TextContent);
    }

    [Fact]
    public void Empty_List_Renders_Empty_State()
    {
        var cut = Render<SbDataTable<Sample>>(p => p
            .Add(x => x.Items, Array.Empty<Sample>())
            .Add(x => x.Columns, Columns()));

        Assert.Contains("暂无数据", cut.Markup);
    }

    [Fact]
    public void Clicking_Sortable_Header_Reorders_Rows()
    {
        var items = new[]
        {
            new Sample("B", 30),
            new Sample("A", 10)
        };
        var cut = Render<SbDataTable<Sample>>(p => p
            .Add(x => x.Items, items)
            .Add(x => x.Columns, Columns()));

        // 默认插入序：B, A —— 首行文本为 "B30"
        Assert.Equal("B30", cut.FindAll("tbody tr")[0].TextContent.Trim());

        // 点击「名称」表头升序排序 → A, B —— 首行文本变为 "A10"
        cut.FindAll("thead th")[0].Click();
        Assert.Equal("A10", cut.FindAll("tbody tr")[0].TextContent.Trim());
    }

    [Fact]
    public void MaxHeight_Adds_Scroll_Container()
    {
        var cut = Render<SbDataTable<Sample>>(p => p
            .Add(x => x.Items, new[] { new Sample("X", 1) })
            .Add(x => x.Columns, Columns())
            .Add(x => x.MaxHeight, "420px"));

        Assert.Contains("table-scroll", cut.Find(".table-wrap").ClassName);
    }
}
