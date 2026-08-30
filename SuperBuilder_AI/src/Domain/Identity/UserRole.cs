namespace SuperBuilder_AI.Models.Identity;

/// <summary>用户-角色关联（TenantId 与所属用户一致；可引用 TenantId=0 的全局角色）。</summary>
public class UserRole
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public long UserId { get; set; }
    public long RoleId { get; set; }
}
