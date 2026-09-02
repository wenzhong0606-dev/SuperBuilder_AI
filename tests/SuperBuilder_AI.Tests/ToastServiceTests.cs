using System.Collections.Generic;
using SuperBuilder_AI.Components.Services;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>S5-5 · ToastService 纯逻辑单测（无需 bUnit）。</summary>
public class ToastServiceTests
{
    [Fact]
    public void Show_Adds_Item_And_Raises_OnChanged()
    {
        var svc = new ToastService();
        var raised = 0;
        svc.OnChanged += () => raised++;

        svc.Show("已保存");

        Assert.Single(svc.Items);
        Assert.Equal("已保存", svc.Items[0].Text);
        Assert.Equal("info", svc.Items[0].Tone);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Show_Ignores_Empty_Text()
    {
        var svc = new ToastService();
        svc.Show("   ");
        Assert.Empty(svc.Items);
    }

    [Fact]
    public void Show_Caps_At_Five_Items()
    {
        var svc = new ToastService();
        for (var i = 0; i < 8; i++) svc.Show($"t{i}");
        Assert.Equal(5, svc.Items.Count);
        Assert.Equal("t7", svc.Items[^1].Text);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("info")]
    [InlineData("warning")]
    [InlineData("danger")]
    public void Tone_Helpers_Map_Correctly(string expected)
    {
        var svc = new ToastService();
        var map = new Dictionary<string, System.Action<string>>
        {
            ["success"] = svc.Success,
            ["info"] = svc.Info,
            ["warning"] = svc.Warning,
            ["danger"] = svc.Error
        };
        map[expected]("x");
        Assert.Equal(expected, svc.Items[0].Tone);
    }

    [Fact]
    public void Dismiss_Removes_By_Id_And_Raises()
    {
        var svc = new ToastService();
        var raised = 0;
        svc.OnChanged += () => raised++;
        svc.Show("a");
        var id = svc.Items[0].Id;

        svc.Dismiss(id);

        Assert.Empty(svc.Items);
        Assert.Equal(2, raised);
    }
}
