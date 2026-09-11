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
/// M12-14 应用可视化设计器界面回归：验证从 AppDsl 解析出组件、调色板可新增组件、源码模式可切换。
/// </summary>
public class AppDesignerUiTests : BunitContext
{
    private const string Dsl =
        "{\"version\":\"1.0\",\"code\":\"demo\",\"name\":\"Demo\",\"pages\":[{\"id\":\"p1\",\"name\":\"Overview\",\"order\":0," +
        "\"components\":[{\"type\":\"chart\",\"id\":\"c1\",\"title\":\"Sales\",\"position\":{\"x\":0,\"y\":0,\"w\":6,\"h\":3}}," +
        "{\"type\":\"kpi\",\"id\":\"k1\",\"title\":\"KPI\",\"position\":{\"x\":6,\"y\":0,\"w\":6,\"h\":3}}]}]}";

    private IRenderedComponent<AppDesigner> RenderDesigner()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var api = DispatchProxy.Create<IApiClient, DesignerApi>();
        var state = new AppState { Token = "test", Permissions = new[] { "app:edit", "app:publish" } };
        state.MarkSessionRestored();
        Services.AddSingleton(api);
        Services.AddSingleton(state);
        Services.AddSingleton(new ToastService());
        Services.AddSingleton(new LocalizationService(null!, null!));
        return Render<AppDesigner>(p => p.Add(x => x.Code, "demo"));
    }

    [Fact]
    public void Designer_RendersComponentsFromDsl_AndAddsFromPalette()
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
                    (JsonSerializer.SerializeToElement(new { code = "demo", name = "Demo", dslJson = Dsl }), 200, null, null));
            throw new NotSupportedException(method.Name);
        }
    }
}
