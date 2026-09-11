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
    /// <summary>列举租户内组织（按编码排序），含部门计数。</summary>
    Task<IReadOnlyList<OrganizationView>> ListOrganizationsAsync(long tenantId, CancellationToken ct = default);

    /// <summary>创建组织；同租户内规范化编码重复返回失败。</summary>
    Task<IdentityResult> CreateOrganizationAsync(long tenantId, string code, string? name, string? description, CancellationToken ct = default);

    /// <summary>列举租户内部门（按组织、编码排序），含组织名与成员计数。</summary>
    Task<IReadOnlyList<DepartmentView>> ListDepartmentsAsync(long tenantId, CancellationToken ct = default);

    /// <summary>创建部门（须隶属同租户组织；上级部门可空）。</summary>
    Task<IdentityResult> CreateDepartmentAsync(long tenantId, long organizationId, long? parentId, string code, string? name, string? description, CancellationToken ct = default);

    /// <summary>列举租户内用户组（按编码排序），含角色码与成员计数。</summary>
    Task<IReadOnlyList<UserGroupView>> ListUserGroupsAsync(long tenantId, CancellationToken ct = default);

    /// <summary>创建用户组，可同时承载角色（角色可为全局）。</summary>
    Task<IdentityResult> CreateUserGroupAsync(long tenantId, string code, string? name, string? description, string[]? roleCodes, CancellationToken ct = default);

    /// <summary>全量替换用户组承载的角色集（RBAC 关联；角色可为全局）。</summary>
    Task<IdentityResult> SetUserGroupRolesAsync(long tenantId, long groupId, string[]? roleCodes, CancellationToken ct = default);

    /// <summary>向用户组添加成员（幂等）。</summary>
    Task<IdentityResult> AddUserGroupMemberAsync(long tenantId, long groupId, long userId, CancellationToken ct = default);

    /// <summary>从用户组移除成员（幂等）。</summary>
    Task<IdentityResult> RemoveUserGroupMemberAsync(long tenantId, long groupId, long userId, CancellationToken ct = default);

    /// <summary>设置用户的部门归属（<paramref name="departmentId"/> 为 null 时清除归属）。</summary>
    Task<IdentityResult> SetUserDepartmentAsync(long tenantId, long userId, long? departmentId, CancellationToken ct = default);
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
