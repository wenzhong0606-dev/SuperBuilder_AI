using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Models.Identity;

namespace SuperBuilder_AI.Interfaces.Identity;

/// <summary>
/// Identity 组织目录服务端口（M12-17，确定性、不调 LLM）。
/// <para>
/// 管理租户内的组织结构（组织 / 部门）与用户组，并把用户组与 M2 已建 RBAC 关联：
/// 用户组可承载角色（<see cref="UserGroupRole"/>），其成员经组角色获得权限。
/// 所有读写均限定在调用方租户作用域内；跨租户一律不可见。
/// </para>
/// </summary>
public interface IIdentityDirectoryService
{
    /// <summary>
    /// 列举租户内组织（按编码排序，含部门计数）。
    /// <paramref name="pageSize"/> &lt;= 0 表示不分页（返回全量，<see cref="DirectoryPage{T}.Total"/> 为总条数）。
    /// </summary>
    Task<DirectoryPage<OrganizationView>> ListOrganizationsAsync(long tenantId, int page = 0, int pageSize = 0, CancellationToken ct = default);

    /// <summary>创建组织；同租户内规范化编码重复返回失败。</summary>
    Task<IdentityResult> CreateOrganizationAsync(long tenantId, string code, string? name, string? description, CancellationToken ct = default);

    /// <summary>M12 增量：重命名 / 修改组织描述（编码不可变）。</summary>
    Task<IdentityResult> UpdateOrganizationAsync(long tenantId, long id, string? name, string? description, CancellationToken ct = default);

    /// <summary>M12 增量：启用 / 停用组织（停用后其部门不再作为新建部门的可选父级目标之外仍可浏览）。</summary>
    Task<IdentityResult> SetOrganizationEnabledAsync(long tenantId, long id, bool enabled, CancellationToken ct = default);

    /// <summary>M12 增量：删除组织；其下仍有部门时拒绝（防孤儿部门）。</summary>
    Task<IdentityResult> DeleteOrganizationAsync(long tenantId, long id, CancellationToken ct = default);

    /// <summary>
    /// 列举租户内部门（按组织、编码排序，含组织名与成员计数）。
    /// <paramref name="pageSize"/> &lt;= 0 表示不分页。
    /// </summary>
    Task<DirectoryPage<DepartmentView>> ListDepartmentsAsync(long tenantId, int page = 0, int pageSize = 0, CancellationToken ct = default);

    /// <summary>创建部门（须隶属同租户组织；上级部门可空）。</summary>
    Task<IdentityResult> CreateDepartmentAsync(long tenantId, long organizationId, long? parentId, string code, string? name, string? description, CancellationToken ct = default);

    /// <summary>M12 增量：重命名 / 修改部门描述，并可改挂组织或上级部门（均须属本租户）。</summary>
    Task<IdentityResult> UpdateDepartmentAsync(long tenantId, long id, string? name, string? description, long? organizationId, long? parentId, CancellationToken ct = default);

    /// <summary>M12 增量：启用 / 停用部门。</summary>
    Task<IdentityResult> SetDepartmentEnabledAsync(long tenantId, long id, bool enabled, CancellationToken ct = default);

    /// <summary>M12 增量：删除部门；仍有成员或子部门时拒绝。</summary>
    Task<IdentityResult> DeleteDepartmentAsync(long tenantId, long id, CancellationToken ct = default);

    /// <summary>
    /// 列举租户内用户组（按编码排序，含角色码与成员计数）。
    /// <paramref name="pageSize"/> &lt;= 0 表示不分页。
    /// </summary>
    Task<DirectoryPage<UserGroupView>> ListUserGroupsAsync(long tenantId, int page = 0, int pageSize = 0, CancellationToken ct = default);

    /// <summary>创建用户组，可同时承载角色（角色可为全局）。</summary>
    Task<IdentityResult> CreateUserGroupAsync(long tenantId, string code, string? name, string? description, string[]? roleCodes, CancellationToken ct = default);

    /// <summary>M12 增量：重命名 / 修改用户组描述。</summary>
    Task<IdentityResult> UpdateUserGroupAsync(long tenantId, long id, string? name, string? description, CancellationToken ct = default);

    /// <summary>M12 增量：启用 / 停用用户组；停用后其承载的角色不再计入成员有效权限。</summary>
    Task<IdentityResult> SetUserGroupEnabledAsync(long tenantId, long id, bool enabled, CancellationToken ct = default);

    /// <summary>M12 增量：删除用户组，并级联清理其成员与角色关联。</summary>
    Task<IdentityResult> DeleteUserGroupAsync(long tenantId, long id, CancellationToken ct = default);

    /// <summary>全量替换用户组承载的角色集（RBAC 关联；角色可为全局）。</summary>
    Task<IdentityResult> SetUserGroupRolesAsync(long tenantId, long groupId, string[]? roleCodes, CancellationToken ct = default);

    /// <summary>向用户组添加成员（幂等）。</summary>
    Task<IdentityResult> AddUserGroupMemberAsync(long tenantId, long groupId, long userId, CancellationToken ct = default);

    /// <summary>从用户组移除成员（幂等）。</summary>
    Task<IdentityResult> RemoveUserGroupMemberAsync(long tenantId, long groupId, long userId, CancellationToken ct = default);

    /// <summary>设置用户的部门归属（<paramref name="departmentId"/> 为 null 时清除归属）。</summary>
    Task<IdentityResult> SetUserDepartmentAsync(long tenantId, long userId, long? departmentId, CancellationToken ct = default);
}

/// <summary>
/// 目录分页信封（M12 增量）。<paramref name="PageSize"/> 为 0 表示未分页（返回全量）。
/// </summary>
public sealed record DirectoryPage<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    /// <summary>构造未分页结果（页大小为总条数）。</summary>
    public static DirectoryPage<T> OfAll(IReadOnlyList<T> items) => new(items, items.Count, 1, items.Count);
}

/// <summary>组织只读投影（M12-17）。</summary>
public sealed record OrganizationView(
    long Id,
    string Code,
    string Name,
    string Description,
    bool IsEnabled,
    int DepartmentCount);

/// <summary>部门只读投影（M12-17）。</summary>
public sealed record DepartmentView(
    long Id,
    long OrganizationId,
    string OrganizationName,
    long? ParentId,
    string Code,
    string Name,
    string Description,
    bool IsEnabled,
    int MemberCount);

/// <summary>用户组只读投影（M12-17），含承载角色码与成员数。</summary>
public sealed record UserGroupView(
    long Id,
    string Code,
    string Name,
    string Description,
    bool IsEnabled,
    IReadOnlyList<string> RoleCodes,
    int MemberCount);
