using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using SuperBuilder_AI.Components.Components.Pages.Admin;
using SuperBuilder_AI.Components.Services;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M12-18 数据权限界面回归：行级策略渲染（含表/列富化）、启用切换调用、筛选与空状态。
/// </summary>
public class DataPoliciesUiTests : BunitContext
{
    private static JsonElement Policies() => JsonSerializer.SerializeToElement(new[]
    {
        new
        {
            id = 1,
            dataSourceId = 50,
            metadataTableId = 51,
            metadataColumnId = 52,
            dataSourceName = "ds5",
            tableName = "orders",
            columnName = "region",
            dataType = "nvarchar",
            subjectType = 1,
            subjectId = (long?)55,
            subjectKey = (string?)null,
            subjectValue = (string?)null,
            subjectDisplayName = "Alice",
            effect = 1,
            @operator = "=",
            value = "east",
            enabled = true,
            version = 1,
            updatedTime = "2026-09-11T00:00:00Z",
        },
        new
        {
            id = 2,
            dataSourceId = 60,
            metadataTableId = 61,
            metadataColumnId = 62,
            dataSourceName = "ds6",
            tableName = "customers",
            columnName = "country",
            dataType = "nvarchar",
            subjectType = 0,
            subjectId = (long?)null,
            subjectKey = (string?)null,
            subjectValue = (string?)null,
            subjectDisplayName = "",
            effect = 2,
            @operator = "IS NOT NULL",
            value = "",
            enabled = false,
            version = 3,
            updatedTime = "2026-09-11T00:00:00Z",
        },
    });

    private static JsonElement DataSources() => JsonSerializer.SerializeToElement(new[]
    {
        new { id = 50, name = "ds5", dbType = "SQLSERVER", enabled = true, tableCount = 3, columnCount = 20 },
        new { id = 60, name = "ds6", dbType = "MYSQL", enabled = true, tableCount = 2, columnCount = 12 },
    });

    private static JsonElement Users() => JsonSerializer.SerializeToElement(new[]
    {
        new { id = 55, tenantId = 5, username = "alice", displayName = "Alice", email = "", status = "Active" },
    });

    private static JsonElement Roles() => JsonSerializer.SerializeToElement(new[]
    {
        new { id = 56, tenantId = 5, code = "viewer", name = "查看者", description = "" },
    });

    private (IRenderedComponent<DataPolicies> Page, PoliciesApi Api) RenderPolicies(bool empty = false)
    {
        var proxy = DispatchProxy.Create<IApiClient, PoliciesApi>();
        var api = (PoliciesApi)(object)proxy;
        api.Empty = empty;
        var state = new AppState { Token = "test", Permissions = new[] { "identity:manage", "metadata:edit" } };
        state.MarkSessionRestored();
        Services.AddSingleton(proxy);
        Services.AddSingleton(state);
        Services.AddSingleton(new ToastService());
        Services.AddSingleton(new LocalizationService(null!, null!));
        return (Render<DataPolicies>(), api);
    }

    [Fact]
    public void DataPolicies_Renders_Rows_With_Enriched_Table_And_Column()
    {
        var (page, _) = RenderPolicies();

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".data-table tbody tr").Count));
        Assert.Contains("orders", page.Markup);
        Assert.Contains("region", page.Markup);
        Assert.Contains("Alice", page.Markup);
        Assert.Contains("ds6", page.Markup);
    }

    [Fact]
    public void DataPolicies_Toggle_Calls_Toggle_Endpoint()
    {
        var (page, api) = RenderPolicies();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".data-table tbody tr").Count));

        // 第一行状态为「已启用」→ 按钮显示「停用」，点击应调用 toggle? 端点并传 enabled=false
        page.FindAll(".sb-row-actions button")[0].Click();

        page.WaitForAssertion(() =>
            Assert.Contains(api.Posts, p => p.Url.Contains("api/data-policies/row/1/toggle")));
    }

    [Fact]
    public void DataPolicies_Filter_By_DataSource_Narrows_Rows()
    {
        var (page, _) = RenderPolicies();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".data-table tbody tr").Count));

        var select = page.Find("select");
        select.Change("60");

        page.WaitForAssertion(() =>
        {
            var rows = page.FindAll(".data-table tbody tr");
            Assert.Single(rows);
            Assert.Contains("customers", page.Markup);
        });
    }

    [Fact]
    public void DataPolicies_Empty_Shows_EmptyState_NotTable()
    {
        var (page, _) = RenderPolicies(empty: true);

        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".empty-state")));
        Assert.Empty(page.FindAll(".data-table tbody tr"));
        Assert.Contains("暂无行级安全策略", page.Markup);
    }

    /// <summary>数据权限替身：按请求 URL 分流返回固定投影，并记录写操作以便断言端点调用。</summary>
    public class PoliciesApi : DispatchProxy
    {
        public bool Empty { get; set; }
        public List<(string Url, object? Body)> Posts { get; } = new();

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            var url = args is { Length: > 0 } ? args[0] as string ?? "" : "";

            if (method!.Name == "GetJsonAsync")
            {
                JsonElement data;
                if (Empty)
                {
                    data = JsonSerializer.SerializeToElement(Array.Empty<object>());
                }
                else if (url.Contains("data-sources/manage")) data = DataSources();
                else if (url.Contains("/users")) data = Users();
                else if (url.Contains("/roles")) data = Roles();
                else data = Policies();
                return Task.FromResult<(JsonElement?, int, string?, string?)>((data, 200, null, null));
            }

            if (method!.Name == "PostAsync")
            {
                Posts.Add((url, args is { Length: > 1 } ? args[1] : null));
                return Task.FromResult<(bool, int, string?, string?)>((true, 200, null, null));
            }

            throw new NotSupportedException(method.Name);
        }
    }
}
