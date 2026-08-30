using System;

namespace SuperBuilder_AI.Models.Identity;

/// <summary>权限（TenantId=0 表示平台全局权限，对所有租户可见）。</summary>
public class Permission
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    /// <summary>权限码，如 "dashboard:view"。</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>权限分类，如 dashboard/app/agent/theme/metadata/audit/billing/identity。</summary>
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
}
