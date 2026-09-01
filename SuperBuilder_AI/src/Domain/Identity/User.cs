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
    /// <summary>
    /// 安全戳（P0-04B 令牌吊销）。每次口令变更、角色/权限变更或账号禁用时轮换；
    /// 令牌载荷携带其值，<see cref="AuthMiddleware"/> 在校验时比对，不一致即视为已吊销（401）。
    /// 新用户创建时生成；存量用户由 <see cref="IIdentityService"/> 种子幂等回填。
    /// </summary>
    public string? SecurityStamp { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
}
