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
    /// <summary>
    /// 规范化登录名（小写、去首尾空白），用于租户内唯一约束 <c>(TenantId, NormalizedUsername)</c>。
    /// 由写入路径（<see cref="IIdentityService.CreateUserAsync"/>）在创建/改名时填充；存量行由种子幂等回填。
    /// </summary>
    public string? NormalizedUsername { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    /// <summary>规范化邮箱（小写、去首尾空白），用于唯一性/匹配。可为空。</summary>
    public string? NormalizedEmail { get; set; }
    /// <summary>邮箱是否已验证（用于邀请/首次设密/忘记密码流程的后续阶段）。</summary>
    public bool EmailConfirmed { get; set; }
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

    /// <summary>规范化用户名：去首尾空白并转小写（不变式），用于租户内唯一约束与查找。</summary>
    public static string NormalizeUsername(string? username) =>
        (username ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>规范化邮箱：去首尾空白并转小写；空输入返回空字符串。</summary>
    public static string NormalizeEmail(string? email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();
}
