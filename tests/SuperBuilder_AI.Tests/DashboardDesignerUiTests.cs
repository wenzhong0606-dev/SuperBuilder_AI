using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using SuperBuilder_AI.Components.Components.Pages.Analysis;
using SuperBuilder_AI.Components.Services;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M12-13 可视化设计器界面回归：验证从 DSL 解析出组件、调色板可新增组件、源码模式可切换。
/// </summary>
public class DashboardDesignerUiTests : BunitContext
{
    private const string Dsl =
        "{\"version\":\"1.0\",\"title\":\"Demo\",\"pages\":[{\"id\":\"p1\",\"name\":\"Overview\",\"order\":1," +
        "\"widgets\":[{\"type\":\"chart\",\"id\":\"c1\",\"position\":{\"x\":0,\"y\":0,\"w\":4,\"h\":3}}," +
        "{\"type\":\"kpi\",\"id\":\"k1\",\"position\":{\"x\":4,\"y\":0,\"w\":4,\"h\":3}}]}]}";

    private IRenderedComponent<DashboardDesigner> RenderDesigner()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var api = DispatchProxy.Create<IApiClient, DesignerApi>();
        var state = new AppState { Token = "test", Permissions = new[] { "dashboard:edit", "dashboard:publish" } };
        state.MarkSessionRestored();
        Services.AddSingleton(api);
        Services.AddSingleton(state);
        Services.AddSingleton(new ToastService());
        Services.AddSingleton(new LocalizationService(null!, null!));
        return Render<DashboardDesigner>(p => p.Add(x => x.Id, 7L));
    }

    [Fact]
    public void Designer_RendersWidgetsFromDsl_AndAddsFromPalette()
    {
        var page = RenderDesigner();

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".sb-dz-widget").Count));

        page.FindAll(".sb-dz-palette-item").First().Click();

        page.WaitForAssertion(() => Assert.Equal(3, page.FindAll(".sb-dz-widget").Count));
    }

    [Fact]
    public void Designer_SourceMode_ShowsDslTextarea()
    {
        var page = RenderDesigner();

        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".sb-dz-mode-toggle button")));

        page.FindAll(".sb-dz-mode-toggle button")[1].Click();

        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".sb-dz-source textarea")));
    }

    public class DesignerApi : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "GetJsonAsync")
                return Task.FromResult<(JsonElement?, int, string?, string?)>(
                    (JsonSerializer.SerializeToElement(new { id = 7, title = "Demo", status = "draft", dslJson = Dsl }), 200, null, null));
            throw new NotSupportedException(method.Name);
        }
    }
}
