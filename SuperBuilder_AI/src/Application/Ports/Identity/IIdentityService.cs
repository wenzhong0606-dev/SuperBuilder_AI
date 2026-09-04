using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Models.Identity;

namespace SuperBuilder_AI.Interfaces.Identity;

/// <summary>Identity/RBAC 服务端口（确定性，不调 LLM）。</summary>
public interface IIdentityService
{
    /// <summary>幂等种子：仅当平台全局角色/权限缺失时插入（TenantId=0）。</summary>
    Task SeedAsync(CancellationToken ct = default);

    /// <summary>在指定租户下创建用户，并指派给定角色码（可引用全局角色）。</summary>
    Task<IdentityResult> CreateUserAsync(long tenantId, string username, string? displayName, string? email, string[]? roleCodes, CancellationToken ct = default);

    /// <summary>为用户指派角色（角色可为全局或本租户）。</summary>
    Task<IdentityResult> AssignRoleAsync(long tenantId, long userId, string roleCode, CancellationToken ct = default);

    /// <summary>撤销用户角色。</summary>
    Task<IdentityResult> RevokeRoleAsync(long tenantId, long userId, string roleCode, CancellationToken ct = default);

    /// <summary>设置/重置用户口令（P0-04A），并更新安全戳使旧令牌失效。</summary>
    Task<IdentityResult> SetPasswordAsync(long tenantId, long userId, string password, CancellationToken ct = default);

    /// <summary>设置用户状态（M1-03：启用/禁用），状态变更即轮换安全戳使旧令牌失效。</summary>
    Task<IdentityResult> SetUserStatusAsync(long tenantId, long userId, UserStatus newStatus, CancellationToken ct = default);

    /// <summary>解析用户经角色聚合后的全部权限码（去重）。</summary>
    Task<IReadOnlyList<string>> GetPermissionsAsync(long tenantId, long userId, CancellationToken ct = default);

    /// <summary>判断用户是否拥有某权限。</summary>
    Task<bool> HasPermissionAsync(long tenantId, long userId, string permissionCode, CancellationToken ct = default);
}
