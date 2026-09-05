namespace SuperBuilder_AI.Components.Components.Constants;

/// <summary>
/// 前端权限码单一事实来源：与后端 <c>SuperBuilder_AI.Models.Identity.IdentityPermissions</c> 保持一致的字符串常量。
/// 菜单（<see cref="SuperBuilder_AI.Components.Components.Layout.NavMenuItems"/>）与页面（<c>PermissionGuard</c>）统一引用本表，杜绝手敲字符串漂移。
/// </summary>
/// <remarks>
/// 后端 <c>/api/auth/me</c> 返回的就是这些码；<c>NavMenuItems.EnforcePermissions=true</c> 后，
/// 拥有对应码的账号才可见菜单项、可直连对应页面。
/// 注意：<c>localization</c>（多语言）后端目录中暂无细粒度权限码（见 S2-7 同类后端缺口），
/// 故其菜单项保持「仅登录即可」（Permission 留空），不在本表定义，待后端补齐码后接入。
/// </remarks>
public static class PermissionCodes
{
    // —— 平台治理面（platform-admin 角色持有）——
    public const string PlatformTenantView = "platform:tenant:view";
    public const string PlatformDiagnosticsView = "platform:diagnostics:view";
    public const string PlatformQuotaManage = "platform:quota:manage";
    public const string PlatformAdminManage = "platform:admin:manage";

    // —— 租户内管理面 ——
    public const string IdentityManage = "identity:manage";
    public const string AuditView = "audit:view";
    public const string ThemeView = "theme:view";
    public const string BillingView = "billing:view";
}
