using System;

namespace SuperBuilder_AI.Models.Identity;

/// <summary>
/// 用户组（M12-17）：租户内的横切集合（如"华东区销售"、"财务审批人"）。
/// <para>
/// 与部门（组织结构）正交：部门表达"归属"，用户组表达"职能/授权集合"。
/// 用户组可承载角色（<see cref="UserGroupRole"/>），其成员经组角色获得权限——
/// 这是 M12-17 与 M2 已建 RBAC 的关联点：<c>GetPermissionsAsync</c> 会把
/// 「用户 → 组 → 角色 → 权限」链路并入用户的有效权限集。
/// </para>
/// </summary>
public class UserGroup : BaseEntity
{
    /// <summary>所属租户（租户作用域隔离键）。</summary>
    public long TenantId { get; set; }

    /// <summary>用户组编码（同租户内唯一）。</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>用户组名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>描述。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>是否启用。</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>规范化编码（小写、去首尾空白），用于同租户内唯一约束。</summary>
    public string? NormalizedCode { get; set; }

    /// <summary>规范化编码（不变式）：去首尾空白并转小写。</summary>
    public static string NormalizeCode(string? code) =>
        (code ?? string.Empty).Trim().ToLowerInvariant();
}

/// <summary>用户组成员关系（M12-17）：<see cref="UserGroup"/> ↔ <see cref="User"/>。</summary>
public class UserGroupMember
{
    /// <summary>主键。</summary>
    public long Id { get; set; }

    /// <summary>所属租户（与组一致）。</summary>
    public long TenantId { get; set; }

    /// <summary>用户组 Id。</summary>
    public long UserGroupId { get; set; }

    /// <summary>成员用户 Id。</summary>
    public long UserId { get; set; }

    /// <summary>加入时间（UTC）。</summary>
    public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 用户组-角色关联（M12-17）：用户组承载的 RBAC 角色（可引用全局角色 TenantId=0）。
/// <para>成员经组获得角色，从而合并进有效权限集（与直接 <see cref="UserRole"/> 并集）。</para>
/// </summary>
public class UserGroupRole
{
    /// <summary>主键。</summary>
    public long Id { get; set; }

    /// <summary>所属租户（与组一致）。</summary>
    public long TenantId { get; set; }

    /// <summary>用户组 Id。</summary>
    public long UserGroupId { get; set; }

    /// <summary>角色 Id（可指向全局角色）。</summary>
    public long RoleId { get; set; }
}

/// <summary>用户-部门归属（M12-17）：<see cref="User"/> ↔ <see cref="Department"/>。</summary>
public class UserDepartmentMember
{
    /// <summary>主键。</summary>
    public long Id { get; set; }

    /// <summary>所属租户（与部门一致）。</summary>
    public long TenantId { get; set; }

    /// <summary>用户 Id。</summary>
    public long UserId { get; set; }

    /// <summary>部门 Id。</summary>
    public long DepartmentId { get; set; }

    /// <summary>加入时间（UTC）。</summary>
    public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
}
