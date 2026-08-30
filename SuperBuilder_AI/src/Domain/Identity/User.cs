using System;

namespace SuperBuilder_AI.Models.Identity;

/// <summary>用户状态。</summary>
public enum UserStatus
{
    /// <summary>启用。</summary>
    Active = 0,
    /// <summary>禁用。</summary>
    Disabled = 1,
}

/// <summary>平台用户（租户作用域，TenantId 为所属租户）。</summary>
public class User
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    /// <summary>口令哈希（可选；P10.1 仅建模，认证登录在后续阶段）。</summary>
    public string? PasswordHash { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
}
