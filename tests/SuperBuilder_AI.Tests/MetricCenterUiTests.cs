using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SuperBuilder_AI.Components.Components.Pages.Analysis;
using SuperBuilder_AI.Components.Services;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M12-16 指标中心界面回归：验证指标/维度渲染、计算字段 Tab 过滤、空状态与行操作导航。
/// </summary>
public class MetricCenterUiTests : BunitContext
{
    private static JsonElement Metrics() => JsonSerializer.SerializeToElement(new[]
    {
        new
        {
            id = 1, businessEntityId = 1, entityName = "stock", entityDisplayName = "库存",
            businessDomain = "仓储域", name = "stock_qty", displayName = "库存量",
            semanticType = "quantity", aggregation = "sum", isCalculated = false, physicalBindingCount = 1,
        },
        new
        {
            id = 2, businessEntityId = 1, entityName = "stock", entityDisplayName = "库存",
            businessDomain = "仓储域", name = "turnover", displayName = "周转率",
            semanticType = "ratio", aggregation = "avg", isCalculated = true, physicalBindingCount = 0,
        },
    });

    private static JsonElement Dimensions() => JsonSerializer.SerializeToElement(new[]
    {
        new { id = 1, businessDomainId = 1, domainName = "仓储域", name = "仓库", description = "仓库维度" },
    });

    private IRenderedComponent<MetricCenter> RenderCenter(bool empty = false)
    {
        var api = empty
            ? DispatchProxy.Create<IApiClient, EmptyMetricApi>()
            : DispatchProxy.Create<IApiClient, MetricApi>();
        var state = new AppState { Token = "test", Permissions = new[] { "metadata:view" } };
        state.MarkSessionRestored();
        Services.AddSingleton(api);
        Services.AddSingleton(state);
        Services.AddSingleton(new LocalizationService(null!, null!));
        Services.AddSingleton(new ToastService());
        return Render<MetricCenter>();
    }

    [Fact]
    public void Center_Renders_Metrics_From_Api()
    {
        var page = RenderCenter();

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".data-table tbody tr").Count));
        Assert.Contains("库存量", page.Markup);
    }

    [Fact]
    public void Center_CalculatedTab_ShowsOnlyCalculatedMetrics()
    {
        var page = RenderCenter();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".sb-tab")));

        page.FindAll(".sb-tab")[2].Click(); // 计算字段

        page.WaitForAssertion(() =>
        {
            Assert.Single(page.FindAll(".data-table tbody tr"));
            Assert.Contains("周转率", page.Markup);
        });
    }

    [Fact]
    public void Center_RowAction_NavigatesToEntityDetail()
    {
        var page = RenderCenter();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".sb-row-actions button")));

        page.FindAll(".sb-row-actions button").First().Click();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("business-model/entities/1", nav.Uri);
    }

    [Fact]
    public void Center_EmptyMetrics_ShowsEmptyState_NotTable()
    {
        var page = RenderCenter(empty: true);

        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".sb-tab")));
        Assert.Empty(page.FindAll(".data-table tbody tr"));
    }

    /// <summary>指标/维度替身：按请求 URL 分流返回固定投影数据。</summary>
    public class MetricApi : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "GetJsonAsync")
            {
                var url = args is { Length: > 0 } ? args[0] as string ?? "" : "";
                if (url.Contains("/dimensions"))
                    return Task.FromResult<(JsonElement?, int, string?, string?)>((Dimensions(), 200, null, null));
                return Task.FromResult<(JsonElement?, int, string?, string?)>((Metrics(), 200, null, null));
            }
            throw new NotSupportedException(method.Name);
        }
    }

    /// <summary>空集替身：验证空状态分支不渲染表格。</summary>
    public class EmptyMetricApi : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "GetJsonAsync")
            {
                var empty = JsonSerializer.SerializeToElement(Array.Empty<object>());
                return Task.FromResult<(JsonElement?, int, string?, string?)>((empty, 200, null, null));
            }
            throw new NotSupportedException(method.Name);
        }
    }
}
