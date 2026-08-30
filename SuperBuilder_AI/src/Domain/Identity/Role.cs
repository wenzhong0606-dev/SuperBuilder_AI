using System;

namespace SuperBuilder_AI.Models.Identity;

/// <summary>角色（TenantId=0 表示平台全局角色，对所有租户可见可指派）。</summary>
public class Role
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
}
