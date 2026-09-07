using SuperBuilder_AI.Components.Components.Constants;
using SuperBuilder_AI.Components.Localization;

namespace SuperBuilder_AI.Components.Components.Layout;

/// <summary>导航项元数据。</summary>
public sealed class NavItem
{
    /// <summary>路由地址（相对，不带前导 /）。</summary>
    public string Href { get; init; } = "";
    public string Title { get; init; } = "";
    /// <summary>资源键（<see cref="Keys.Nav"/>）；为空时回退到 <c>Nav.{Href}</c> 兼容键。</summary>
    public string? Key { get; init; }
    /// <summary>图标 symbol id（见 IconSprite）。</summary>
    public string Icon { get; init; } = "";
    /// <summary>所需权限码；为空表示仅要求登录。</summary>
    public string? Permission { get; init; }
    /// <summary>是否仅在侧栏显示（不出现在面包屑父级链中）。</summary>
    public bool HideInBreadcrumb { get; init; }
}

/// <summary>导航分组。</summary>
public sealed class NavGroup
{
    public string Title { get; init; } = "";
    /// <summary>分组标题资源键（<see cref="Keys.Nav.Group*"/>）；为空时回退到分组 Title。</summary>
    public string? Key { get; init; }
    public IReadOnlyList<NavItem> Items { get; init; } = Array.Empty<NavItem>();
}

/// <summary>
/// 导航单一事实来源：侧栏、面包屑、权限过滤均读此表。
/// 新增页面时只需在此登记，无需改动 NavMenu。
/// </summary>
public static class NavMenuItems
{
    /// <summary>
    /// 是否启用基于权限码的菜单/页面过滤。
    /// 前端权限码已与后端 <c>IdentityPermissions</c> 对齐（见 <c>PermissionCodes</c>），
    /// <c>/api/auth/me</c> 返回用户实际持有的码集合，故可安全开启。
    /// 守卫内置兜底：当权限数据尚未加载（owned.Count==0）时一律放行，避免首帧误隐藏。
    /// </summary>
    public static bool EnforcePermissions { get; set; } = true;

    public static IReadOnlyList<NavGroup> Groups { get; } = new[]
    {
        new NavGroup
        {
            Title = "旗舰",
            Key = Keys.Nav.GroupFlagship,
            Items = new[]
            {
                new NavItem { Href = "", Title = "首页", Key = Keys.Nav.Home, Icon = "sb-ico-home" },
                new NavItem { Href = "ask", Title = "Ask BI 智能问数", Key = Keys.Nav.Ask, Icon = "sb-ico-ask" }
            }
        },
        new NavGroup
        {
            Title = "分析",
            Key = Keys.Nav.GroupAnalysis,
            Items = new[]
            {
                new NavItem { Href = "dashboards", Title = "仪表盘", Key = Keys.Nav.Dashboards, Icon = "sb-ico-dash" },
                new NavItem { Href = "apps", Title = "应用工厂", Key = Keys.Nav.Apps, Icon = "sb-ico-app" },
                new NavItem { Href = "agent", Title = "智能体 / Copilot", Key = Keys.Nav.Agent, Icon = "sb-ico-agent" },
                new NavItem { Href = "semantic-labels", Title = "语义标签", Key = Keys.Nav.SemanticLabels, Icon = "sb-ico-tag" },
                new NavItem { Href = "business-model", Title = "语义模型", Key = Keys.Nav.BusinessModel, Icon = "sb-ico-model" }
            }
        },
        new NavGroup
        {
            Title = "自定义",
            Key = Keys.Nav.GroupCustom,
            Items = new[]
            {
                new NavItem { Href = "components", Title = "组件库", Key = Keys.Nav.Components, Icon = "sb-ico-comp" },
                new NavItem { Href = "themes", Title = "主题编辑器", Key = Keys.Nav.ThemeEditor, Icon = "sb-ico-theme" }
            }
        },
        new NavGroup
        {
            Title = "平台扩展",
            Key = Keys.Nav.GroupPlatformExt,
            Items = new[]
            {
                new NavItem { Href = "data-sources", Title = "数据源管理", Key = Keys.Nav.DataSources, Icon = "sb-ico-db" },
                new NavItem { Href = "model-accounts", Title = "模型与账号", Key = Keys.Nav.ModelAccounts, Icon = "sb-ico-key" }
            }
        },
        new NavGroup
        {
            Title = "管理后台",
            Key = Keys.Nav.GroupAdmin,
            Items = new[]
            {
                new NavItem { Href = "admin/tenants", Title = "租户", Key = Keys.Nav.Tenants, Icon = "sb-ico-tenant", Permission = PermissionCodes.PlatformTenantView },
                new NavItem { Href = "admin/tenant-members", Title = "租户成员", Key = Keys.Nav.TenantMembers, Icon = "sb-ico-users", Permission = PermissionCodes.PlatformTenantManage },
                new NavItem { Href = "admin/self-registration", Title = "自助注册", Key = Keys.Nav.SelfRegistration, Icon = "sb-ico-tenant", Permission = PermissionCodes.PlatformAdminManage },
                new NavItem { Href = "admin/demo-data", Title = "演示数据", Key = Keys.Nav.DemoData, Icon = "sb-ico-db", Permission = PermissionCodes.PlatformAdminManage },
                new NavItem { Href = "admin/platform-admins", Title = "平台管理员", Key = Keys.Nav.PlatformAdmins, Icon = "sb-ico-user", Permission = PermissionCodes.PlatformAdminManage },
                new NavItem { Href = "admin/platform-admin-scopes", Title = "管理员租户范围", Key = Keys.Nav.PlatformAdminScopes, Icon = "sb-ico-scope", Permission = PermissionCodes.PlatformAdminManage },
                new NavItem { Href = "admin/identity", Title = "身份权限", Key = Keys.Nav.Identity, Icon = "sb-ico-shield", Permission = PermissionCodes.IdentityManage },
                new NavItem { Href = "admin/audit", Title = "审计", Key = Keys.Nav.Audit, Icon = "sb-ico-audit", Permission = PermissionCodes.AuditView },
                new NavItem { Href = "admin/quota", Title = "配额", Key = Keys.Nav.Quota, Icon = "sb-ico-quota", Permission = PermissionCodes.PlatformQuotaManage },
                new NavItem { Href = "admin/localization", Title = "多语言", Key = Keys.Nav.Localization, Icon = "sb-ico-lang", Permission = PermissionCodes.LocalizationView },
                new NavItem { Href = "admin/themes", Title = "主题", Key = Keys.Nav.Themes, Icon = "sb-ico-theme", Permission = PermissionCodes.ThemeView },
                new NavItem { Href = "admin/system", Title = "系统状态", Key = Keys.Nav.System, Icon = "sb-ico-activity" }
            }
        }
    };

    /// <summary>按路由地址查找导航项（用于面包屑标题）。</summary>
    public static NavItem? FindByHref(string href)
    {
        var target = (href ?? "").Trim('/');
        foreach (var g in Groups)
            foreach (var it in g.Items)
                if (string.Equals(it.Href.Trim('/'), target, StringComparison.OrdinalIgnoreCase))
                    return it;
        return null;
    }

    /// <summary>查找导航项所属分组。</summary>
    public static NavGroup? FindGroup(NavItem item)
    {
        foreach (var g in Groups)
            if (g.Items.Any(x => ReferenceEquals(x, item)))
                return g;
        return null;
    }
}
