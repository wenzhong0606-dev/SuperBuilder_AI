using System;

namespace SuperBuilder_AI.Models.Identity;

/// <summary>
/// 组织（M12-17）：租户内的组织结构单元（如公司/机构/事业部）。
/// <para>
/// 与 <c>Organization.Tenant</c>（租户）不同层级：一个租户可有多个组织；
/// 部门（<see cref="Department"/>）挂在组织之下；用户经部门归属间接属于组织。
/// 租户作用域由 <see cref="TenantId"/> 承载，查询过滤器保证不跨租户可见。
/// </para>
/// </summary>
public class Organization : BaseEntity
{
    /// <summary>所属租户（租户作用域隔离键）。</summary>
    public long TenantId { get; set; }

    /// <summary>组织编码（同租户内唯一）。</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>组织名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>描述。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>是否启用（停用后不参与新归属，仅保留历史）。</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>规范化编码（小写、去首尾空白），用于同租户内唯一约束 <c>(TenantId, NormalizedCode)</c>。</summary>
    public string? NormalizedCode { get; set; }

    /// <summary>规范化编码（不变式）：去首尾空白并转小写。</summary>
    public static string NormalizeCode(string? code) =>
        (code ?? string.Empty).Trim().ToLowerInvariant();
}
