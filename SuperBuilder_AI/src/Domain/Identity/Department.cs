namespace SuperBuilder_AI.Models.Identity;

/// <summary>
/// 部门（M12-17）：隶属于某个组织（<see cref="OrganizationId"/>），支持树形层级（<see cref="ParentId"/>）。
/// <para>
/// 用户经 <see cref="UserDepartmentMember"/> 归属部门，从而间接归属组织。
/// 租户作用域由 <see cref="TenantId"/> 承载，查询过滤器保证不跨租户可见。
/// </para>
/// </summary>
public class Department : BaseEntity
{
    /// <summary>所属租户（租户作用域隔离键）。</summary>
    public long TenantId { get; set; }

    /// <summary>所属组织 Id（必填；须为同租户组织）。</summary>
    public long OrganizationId { get; set; }

    /// <summary>上级部门 Id（可空 = 顶级部门）；层内构成树。</summary>
    public long? ParentId { get; set; }

    /// <summary>部门编码（同租户内唯一）。</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>部门名称。</summary>
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
