namespace SuperBuilder_AI.Models.Identity;

/// <summary>角色-权限关联（TenantId 与所属角色一致；全局角色为 TenantId=0）。</summary>
public class RolePermission
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public long RoleId { get; set; }
    public long PermissionId { get; set; }
}
