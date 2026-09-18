namespace SuperBuilder_AI.Components.Components.Constants;

/// <summary>
/// 前端权限码单一事实来源：与后端 <c>SuperBuilder_AI.Models.Identity.IdentityPermissions</c> 保持一致的字符串常量。
/// 菜单（<see cref="SuperBuilder_AI.Components.Components.Layout.NavMenuItems"/>）与页面（<c>PermissionGuard</c>）统一引用本表，杜绝手敲字符串漂移。
/// </summary>
/// <remarks>
/// 后端 <c>/api/auth/me</c> 返回的就是这些码；<c>NavMenuItems.EnforcePermissions=true</c> 后，
/// 拥有对应码的账号才可见菜单项、可直连对应页面。
/// </remarks>
public static class PermissionCodes
{
    // —— 平台治理面（platform-admin 角色持有）——
    public const string PlatformTenantView = "platform:tenant:view";
    public const string PlatformTenantManage = "platform:tenant:manage";
    public const string PlatformDiagnosticsView = "platform:diagnostics:view";
    public const string PlatformQuotaManage = "platform:quota:manage";
    public const string PlatformAdminManage = "platform:admin:manage";

    // —— 多语言（localization）——
    public const string LocalizationView = "localization:view";
    public const string LocalizationManage = "localization:manage";

    // —— 租户内管理面 ——
    public const string IdentityManage = "identity:manage";
    public const string AuditView = "audit:view";
    public const string ThemeView = "theme:view";
    public const string BillingView = "billing:view";

    // —— 应用与自定义组件 ——
    public const string AppView = "app:view";
    public const string AppCreate = "app:create";
    public const string AppEdit = "app:edit";
    public const string AppDelete = "app:delete";
    public const string AppPublish = "app:publish";

    // —— 仪表盘（与后端 IdentityPermissions 对齐）——
    public const string DashboardView = "dashboard:view";
    public const string DashboardCreate = "dashboard:create";
    public const string DashboardEdit = "dashboard:edit";
    public const string DashboardPublish = "dashboard:publish";
    public const string DashboardDelete = "dashboard:delete";

    // —— 智能体 ——
    public const string AgentView = "agent:view";
    public const string AgentCreate = "agent:create";
    public const string AgentManage = "agent:manage";
    public const string AgentDelete = "agent:delete";

    // —— 主题（编辑 / 发布）——
    public const string ThemeEdit = "theme:edit";
    public const string ThemePublish = "theme:publish";

    // —— 业务元数据 ——
    public const string MetadataView = "metadata:view";
    public const string MetadataEdit = "metadata:edit";
    public const string MetadataScan = "metadata:scan";
    public const string MetadataCancelScan = "metadata:cancel_scan";
    public const string MetadataDelete = "metadata:delete";
    public const string MetadataCleanupMetadata = "metadata:cleanup_metadata";
}
