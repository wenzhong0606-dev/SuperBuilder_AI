using System;
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
/// M12-17 组织目录界面回归：组织/部门/用户组渲染、Tab 切换、组行操作与空状态。
/// </summary>
public class IdentityDirectoryUiTests : BunitContext
{
    /// <summary>M12 增量：目录列表端点返回分页信封，页面须解析 <c>items</c>。</summary>
    private static JsonElement Page(object[] items) => JsonSerializer.SerializeToElement(new
    {
        items,
        total = items.Length,
        page = 1,
        pageSize = items.Length,
    });

    private static JsonElement Orgs() => Page(new[]
    {
        new { id = 1, code = "hq", name = "总部", description = "", isEnabled = true, departmentCount = 2 },
        new { id = 2, code = "branch", name = "分部", description = "", isEnabled = true, departmentCount = 0 },
    });

    private static JsonElement Departments() => Page(new[]
    {
        new { id = 1, organizationId = 1, organizationName = "总部", parentId = (long?)null, code = "sales", name = "销售部", description = "", isEnabled = true, memberCount = 1 },
    });

    private static JsonElement Groups() => Page(new[]
    {
        new { id = 1, code = "analysts", name = "分析师", description = "", isEnabled = true, roleCodes = new[] { "viewer", "member" }, memberCount = 3 },
    });

    private static JsonElement Roles() => JsonSerializer.SerializeToElement(new[]
    {
        new { id = 1, tenantId = 0, code = "viewer", name = "只读访客", description = "" },
        new { id = 2, tenantId = 0, code = "member", name = "成员", description = "" },
    });

    private static JsonElement Users() => JsonSerializer.SerializeToElement(new[]
    {
        new { id = 7, tenantId = 5, username = "alice", displayName = "Alice", email = "", status = "Active" },
    });

    private IRenderedComponent<IdentityDirectory> RenderDirectory(bool empty = false)
    {
        var api = empty
            ? DispatchProxy.Create<IApiClient, EmptyDirectoryApi>()
            : DispatchProxy.Create<IApiClient, DirectoryApi>();
        var state = new AppState { Token = "test", Permissions = new[] { "identity:manage" } };
        state.MarkSessionRestored();
        Services.AddSingleton(api);
        Services.AddSingleton(state);
        Services.AddSingleton(new ToastService());
        Services.AddSingleton(new LocalizationService(null!, null!));
        return Render<IdentityDirectory>();
    }

    [Fact]
    public void Directory_Renders_Organizations_From_Api()
    {
        var page = RenderDirectory();

        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".sb-tab")));
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".data-table tbody tr").Count));
        Assert.Contains("hq", page.Markup);
    }

    [Fact]
    public void Directory_SwitchToGroupsTab_Shows_Groups_With_Roles()
    {
        var page = RenderDirectory();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".sb-tab")));

        page.FindAll(".sb-tab")[2].Click(); // 用户组

        page.WaitForAssertion(() =>
        {
            Assert.Single(page.FindAll(".data-table tbody tr"));
            Assert.Contains("analysts", page.Markup);
            Assert.Contains("viewer", page.Markup);
        });
    }

    [Fact]
    public void Directory_GroupRowActions_Are_Rendered()
    {
        var page = RenderDirectory();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".sb-tab")));

        page.FindAll(".sb-tab")[2].Click();

        page.WaitForAssertion(() =>
            // M12 增量：用户组行操作 = 重命名 / 编辑角色 / 添加成员 / 启停 / 删除
            Assert.Equal(5, page.FindAll(".sb-row-actions button").Count));
    }

    [Fact]
    public void Directory_Parses_PagedEnvelope_Items()
    {
        var page = RenderDirectory();
        // 替身返回分页信封 { items, total, page, pageSize }；页面须正确取 items 渲染。
        page.WaitForAssertion(() => Assert.Contains("hq", page.Markup));
    }

    [Fact]
    public void Directory_EmptyOrganizations_Shows_EmptyState_NotTable()
    {
        var page = RenderDirectory(empty: true);

        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".sb-tab")));
        Assert.Empty(page.FindAll(".data-table tbody tr"));
    }

    /// <summary>组织目录替身：按请求 URL 分流返回固定投影数据。</summary>
    public class DirectoryApi : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "GetJsonAsync")
            {
                var url = args is { Length: > 0 } ? args[0] as string ?? "" : "";
                JsonElement data;
                if (url.Contains("/departments")) data = Departments();
                else if (url.Contains("/user-groups")) data = Groups();
                else if (url.Contains("/roles")) data = Roles();
                else if (url.Contains("/users")) data = Users();
                else data = Orgs();
                return Task.FromResult<(JsonElement?, int, string?, string?)>((data, 200, null, null));
            }
            throw new NotSupportedException(method.Name);
        }
    }

    /// <summary>空集替身：验证空状态分支不渲染表格。</summary>
    public class EmptyDirectoryApi : DispatchProxy
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
