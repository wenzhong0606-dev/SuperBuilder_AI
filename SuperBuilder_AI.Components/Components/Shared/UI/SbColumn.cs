using Microsoft.AspNetCore.Components;

namespace SuperBuilder_AI.Components.Components.Shared.UI;

/// <summary>SbDataTable 的列定义。</summary>
public sealed class SbColumn<TItem>
{
    /// <summary>列头文本。</summary>
    public string Title { get; init; } = "";

    /// <summary>取值函数（默认渲染与排序均使用它）。</summary>
    public Func<TItem, string> Value { get; init; } = _ => "";

    /// <summary>自定义单元格渲染（优先于 Value）。</summary>
    public RenderFragment<TItem>? Cell { get; init; }

    /// <summary>列宽（CSS 值，如 "160px"）。</summary>
    public string? Width { get; init; }

    /// <summary>对齐：left（默认）/ center / right。</summary>
    public string Align { get; init; } = "left";

    /// <summary>是否允许点击表头排序。</summary>
    public bool Sortable { get; init; } = true;

    /// <summary>状态列：按文本内容自动映射徽章色调。</summary>
    public bool IsStatus { get; init; }

    public string AlignCss => Align switch
    {
        "right" => "ta-right",
        "center" => "ta-center",
        _ => "ta-left"
    };
}

/// <summary>SbColumn 工厂，便于在页面中写 SbCol.Of&lt;T&gt;(...)。</summary>
public static class SbCol
{
    public static SbColumn<T> Of<T>(string title, Func<T, string> value, string? width = null,
        string align = "left", bool sortable = true, bool isStatus = false, RenderFragment<T>? cell = null)
        => new()
        {
            Title = title,
            Value = value,
            Width = width,
            Align = align,
            Sortable = sortable,
            IsStatus = isStatus,
            Cell = cell
        };
}
