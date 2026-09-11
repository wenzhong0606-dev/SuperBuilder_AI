using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SuperBuilder_AI.Components.Components.Pages.Analysis;
using SuperBuilder_AI.Components.Services;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M12-15 语义模型关系图界面回归：验证实体渲染为节点、关系渲染为边、点击节点跳转实体详情、无关系/无实体分支。
/// </summary>
public class SemanticModelGraphUiTests : BunitContext
{
    public SemanticModelGraphUiTests()
    {
        Services.AddSingleton<LocalizationService>(new LocalizationService(null!, null!));
    }

    private static List<JsonElement> EntitySet() => new()
    {
        JsonSerializer.SerializeToElement(new { id = 1, name = "customer", displayName = "客户", businessDomain = "Sales" }),
        JsonSerializer.SerializeToElement(new { id = 2, name = "order", displayName = "订单", businessDomain = "Sales" }),
    };

    private static List<JsonElement> RelSet() => new()
    {
        JsonSerializer.SerializeToElement(new { id = 1, sourceEntityId = 2, targetEntityId = 1, name = "order_customer" }),
    };

    private (IRenderedComponent<SemanticModelGraph> Page, NavigationManager Nav) RenderGraph(
        List<JsonElement> entities, List<JsonElement> relationships)
    {
        var page = Render<SemanticModelGraph>(p => p
            .Add(x => x.Entities, entities)
            .Add(x => x.Relationships, relationships));
        var nav = Services.GetRequiredService<NavigationManager>();
        return (page, nav);
    }

    [Fact]
    public void Graph_RendersNodes_AndEdges()
    {
        var (page, _) = RenderGraph(EntitySet(), RelSet());

        Assert.Equal(2, page.FindAll(".sb-graph-node").Count);
        Assert.Single(page.FindAll("line"));
    }

    [Fact]
    public void Graph_ClickNode_NavigatesToEntityDetail()
    {
        var (page, nav) = RenderGraph(EntitySet(), RelSet());

        page.FindAll(".sb-graph-node").First().Click();

        Assert.EndsWith("business-model/entities/1", nav.Uri);
    }

    [Fact]
    public void Graph_NoRelationships_ShowsNote_ButRendersNodes()
    {
        var (page, _) = RenderGraph(EntitySet(), new List<JsonElement>());

        Assert.Equal(2, page.FindAll(".sb-graph-node").Count);
        Assert.NotEmpty(page.FindAll(".sb-graph-note"));
    }

    [Fact]
    public void Graph_NoEntities_ShowsEmptyState()
    {
        var (page, _) = RenderGraph(new List<JsonElement>(), new List<JsonElement>());

        Assert.Empty(page.FindAll(".sb-graph-node"));
    }
}
