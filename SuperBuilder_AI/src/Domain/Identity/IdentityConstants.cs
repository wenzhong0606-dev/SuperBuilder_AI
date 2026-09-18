namespace SuperBuilder_AI.Models.Identity;

/// <summary>内置角色码（TenantId=0 全局角色）。</summary>
public static class IdentityRoles
{
    public const string PlatformAdmin = "platform-admin";
    public const string TenantAdmin = "tenant-admin";
    public const string Member = "member";
    public const string Viewer = "viewer";
}

/// <summary>内置权限码（按资源分类）。</summary>
public static class IdentityPermissions
{
    public const string DashboardView = "dashboard:view";
    public const string DashboardCreate = "dashboard:create";
    public const string DashboardEdit = "dashboard:edit";
    public const string DashboardPublish = "dashboard:publish";
    public const string DashboardDelete = "dashboard:delete";

    public const string AppView = "app:view";
    public const string AppCreate = "app:create";
    public const string AppEdit = "app:edit";
    public const string AppPublish = "app:publish";
    public const string AppDelete = "app:delete";

    public const string AgentView = "agent:view";
    public const string AgentCreate = "agent:create";
    public const string AgentManage = "agent:manage";
    public const string AgentDelete = "agent:delete";

    public const string ThemeView = "theme:view";
    public const string ThemeEdit = "theme:edit";
    public const string ThemePublish = "theme:publish";

    public const string MetadataView = "metadata:view";
    public const string MetadataEdit = "metadata:edit";
    public const string MetadataScan = "metadata:scan";
    public const string MetadataCancelScan = "metadata:cancel_scan";
    public const string MetadataDelete = "metadata:delete";
    public const string MetadataCleanupMetadata = "metadata:cleanup_metadata";

    public const string AuditView = "audit:view";

    public const string LocalizationView = "localization:view";
    public const string LocalizationManage = "localization:manage";

    public const string BillingView = "billing:view";
    public const string BillingManage = "billing:manage";

    public const string IdentityManage = "identity:manage";

    public const string PlatformDiagnosticsView = "platform:diagnostics:view";
    public const string PlatformDiagnosticsManage = "platform:diagnostics:manage";
    public const string PlatformTenantView = "platform:tenant:view";
    public const string PlatformTenantManage = "platform:tenant:manage";
    public const string PlatformTenantSettingsManage = "platform:tenant-settings:manage";
    public const string PlatformAuditView = "platform:audit:view";
    public const string PlatformQuotaManage = "platform:quota:manage";
    public const string PlatformAdminManage = "platform:admin:manage";
}

/// <summary>权限定义（种子用）。</summary>
public sealed record PermissionDef(string Code, string Name, string Category, string Description);

/// <summary>角色定义（种子用，含其权限码集合）。</summary>
public sealed record RoleDef(string Code, string Name, string Description, string[] Permissions);

/// <summary>Identity 种子目录：平台全局角色与权限的权威定义，供幂等种子使用。</summary>
public static class IdentityCatalog
{
    public static readonly IReadOnlyList<PermissionDef> Permissions = new List<PermissionDef>
    {
        new(IdentityPermissions.DashboardView, "查看仪表盘", "dashboard", "查看仪表盘"),
        new(IdentityPermissions.DashboardCreate, "创建仪表盘", "dashboard", "创建仪表盘"),
        new(IdentityPermissions.DashboardEdit, "编辑仪表盘", "dashboard", "编辑仪表盘"),
        new(IdentityPermissions.DashboardPublish, "发布仪表盘", "dashboard", "发布仪表盘"),
        new(IdentityPermissions.DashboardDelete, "删除仪表盘", "dashboard", "删除仪表盘"),

        new(IdentityPermissions.AppView, "查看应用", "app", "查看应用"),
        new(IdentityPermissions.AppCreate, "创建应用", "app", "创建应用"),
        new(IdentityPermissions.AppEdit, "编辑应用", "app", "编辑应用"),
        new(IdentityPermissions.AppPublish, "发布应用", "app", "发布应用"),
        new(IdentityPermissions.AppDelete, "删除应用", "app", "删除应用"),

        new(IdentityPermissions.AgentView, "查看Agent", "agent", "查看Agent"),
        new(IdentityPermissions.AgentCreate, "创建Agent", "agent", "创建Agent"),
        new(IdentityPermissions.AgentManage, "管理Agent", "agent", "管理Agent"),
        new(IdentityPermissions.AgentDelete, "删除Agent", "agent", "删除Agent"),

        new(IdentityPermissions.ThemeView, "查看主题", "theme", "查看主题"),
        new(IdentityPermissions.ThemeEdit, "编辑主题", "theme", "编辑主题"),
        new(IdentityPermissions.ThemePublish, "发布主题", "theme", "发布主题"),

        new(IdentityPermissions.MetadataView, "查看元数据", "metadata", "查看业务元数据"),
        new(IdentityPermissions.MetadataEdit, "编辑元数据", "metadata", "编辑业务元数据"),
        new(IdentityPermissions.MetadataScan, "扫描数据源", "metadata", "扫描数据源"),
        new(IdentityPermissions.MetadataCancelScan, "取消扫描", "metadata", "取消进行中的元数据扫描"),
        new(IdentityPermissions.MetadataDelete, "删除数据源", "metadata", "停用或删除数据源"),
        new(IdentityPermissions.MetadataCleanupMetadata, "清理元数据", "metadata", "彻底清理数据源及其元数据与向量"),

        new(IdentityPermissions.AuditView, "查看审计", "audit", "查看审计日志"),

        new(IdentityPermissions.LocalizationView, "查看多语言", "localization", "查看语言目录与界面文本"),
        new(IdentityPermissions.LocalizationManage, "管理平台多语言", "localization", "管理平台语言目录与基线文本"),

        new(IdentityPermissions.BillingView, "查看账单", "billing", "查看账单与配额"),
        new(IdentityPermissions.BillingManage, "管理账单", "billing", "管理账单与配额"),

        new(IdentityPermissions.IdentityManage, "管理身份", "identity", "管理用户与角色"),

        new(IdentityPermissions.PlatformDiagnosticsView, "查看平台诊断", "platform", "查看平台指标与诊断状态"),
        new(IdentityPermissions.PlatformDiagnosticsManage, "管理平台诊断", "platform", "运行测试与重建平台诊断索引"),
        new(IdentityPermissions.PlatformTenantView, "查看租户", "platform", "查看平台租户目录"),
        new(IdentityPermissions.PlatformTenantManage, "管理租户", "platform", "创建、启用和停用租户"),
        new(IdentityPermissions.PlatformTenantSettingsManage, "管理租户设置", "platform", "管理目标租户的平台设置"),
        new(IdentityPermissions.PlatformAuditView, "查看平台审计", "platform", "查看平台治理审计"),
        new(IdentityPermissions.PlatformQuotaManage, "管理平台配额", "platform", "管理租户配额策略"),
        new(IdentityPermissions.PlatformAdminManage, "管理平台管理员", "platform", "新增/停用/启用/重置平台治理管理员口令"),
    };

    public static readonly IReadOnlyList<RoleDef> Roles = new List<RoleDef>
    {
        new(IdentityRoles.PlatformAdmin, "平台治理管理员", "仅限平台治理面的最小权限", new[]
        {
            IdentityPermissions.PlatformDiagnosticsView, IdentityPermissions.PlatformDiagnosticsManage,
            IdentityPermissions.PlatformTenantView, IdentityPermissions.PlatformTenantManage,
            IdentityPermissions.PlatformTenantSettingsManage,             IdentityPermissions.PlatformAuditView,
            IdentityPermissions.PlatformQuotaManage,
            IdentityPermissions.PlatformAdminManage,
            IdentityPermissions.LocalizationView, IdentityPermissions.LocalizationManage,
        }),
        new(IdentityRoles.TenantAdmin, "租户管理员", "租户内管理权限（不含平台账单）", new[]
        {
            IdentityPermissions.DashboardView, IdentityPermissions.DashboardCreate, IdentityPermissions.DashboardEdit,
            IdentityPermissions.DashboardPublish, IdentityPermissions.DashboardDelete,
            IdentityPermissions.AppView, IdentityPermissions.AppCreate, IdentityPermissions.AppEdit,
            IdentityPermissions.AppPublish, IdentityPermissions.AppDelete,
            IdentityPermissions.AgentView, IdentityPermissions.AgentCreate, IdentityPermissions.AgentManage,
            IdentityPermissions.ThemeView, IdentityPermissions.ThemeEdit, IdentityPermissions.ThemePublish,
            IdentityPermissions.MetadataView, IdentityPermissions.MetadataEdit, IdentityPermissions.MetadataScan, IdentityPermissions.MetadataCancelScan,
            IdentityPermissions.MetadataDelete, IdentityPermissions.MetadataCleanupMetadata,
            IdentityPermissions.MetadataDelete, IdentityPermissions.MetadataCleanupMetadata,
            IdentityPermissions.AuditView, IdentityPermissions.IdentityManage,
            IdentityPermissions.LocalizationView,
        }),
        new(IdentityRoles.Member, "成员", "创建与编辑权限", new[]
        {
            IdentityPermissions.DashboardView, IdentityPermissions.DashboardCreate, IdentityPermissions.DashboardEdit, IdentityPermissions.DashboardPublish,
            IdentityPermissions.AppView, IdentityPermissions.AppCreate, IdentityPermissions.AppEdit, IdentityPermissions.AppPublish,
            IdentityPermissions.AgentView, IdentityPermissions.AgentCreate,
            IdentityPermissions.ThemeView,
            IdentityPermissions.MetadataView,
        }),
        new(IdentityRoles.Viewer, "只读访客", "仅查看权限", new[]
        {
            IdentityPermissions.DashboardView,
            IdentityPermissions.AppView,
            IdentityPermissions.AgentView,
            IdentityPermissions.ThemeView,
            IdentityPermissions.MetadataView,
        }),
    };
}
