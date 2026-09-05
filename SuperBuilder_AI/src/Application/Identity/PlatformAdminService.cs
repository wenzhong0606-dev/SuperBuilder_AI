using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Audit;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Services.Auth;

namespace SuperBuilder_AI.Services.Identity;

/// <summary>平台管理员视图（不含口令哈希等敏感字段）。</summary>
public sealed record PlatformAdminView(
    long Id,
    string Username,
    string DisplayName,
    string Email,
    UserStatus Status,
    DateTime CreatedTime);

/// <summary>新增平台管理员请求。</summary>
public sealed record AddPlatformAdminRequest(
    string Username,
    string Password,
    string? DisplayName = null,
    string? Email = null);

/// <summary>重置平台管理员口令请求。</summary>
public sealed record ResetPlatformAdminPasswordRequest(string NewPassword);

/// <summary>平台管理员常态化治理服务（确定性，不调 LLM）。</summary>
public interface IPlatformAdminService
{
    /// <summary>列出全部平台管理员（按用户名排序）。</summary>
    Task<IReadOnlyList<PlatformAdminView>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// 通过专用治理端点新增（或重新启用并补授角色）平台管理员。
    /// 与首次引导 <see cref="PlatformAdminBootstrapper"/> 不同，本方法用于把 platform-admin
    /// 授予额外治理账号；已存在同名账号时幂等复用并补授角色/激活。
    /// </summary>
    Task<PlatformAdminView> AddAsync(AddPlatformAdminRequest request, string actor, CancellationToken ct = default);

    /// <summary>停用指定平台管理员；始终保留至少一名有效管理员，否则抛 <see cref="InvalidOperationException"/>。</summary>
    Task DisableAsync(long userId, string actor, CancellationToken ct = default);

    /// <summary>重新启用指定平台管理员。</summary>
    Task EnableAsync(long userId, string actor, CancellationToken ct = default);

    /// <summary>重置指定平台管理员口令（至少 8 位），并轮换安全戳使旧令牌失效。</summary>
    Task ResetPasswordAsync(long userId, string newPassword, string actor, CancellationToken ct = default);
}

/// <summary>
/// 平台治理管理员列表/新增/停用/启用/重置密码的权威实现。
/// 审计：每次治理动作写入 append-only <see cref="AuditLog"/>（Actor/Action/EntityType=PlatformAdmin）。
/// 不变量：至少保留一名有效（Active）平台管理员。
/// </summary>
public sealed class PlatformAdminService : IPlatformAdminService
{
    private readonly SuperBIContext _db;
    private readonly IIdentityService _identity;
    private readonly IPasswordHasher _hasher;
    private readonly IAuditLogService _audit;

    public PlatformAdminService(
        SuperBIContext db,
        IIdentityService identity,
        IPasswordHasher hasher,
        IAuditLogService audit)
    {
        _db = db;
        _identity = identity;
        _hasher = hasher;
        _audit = audit;
    }

    private async Task<(long TenantId, long RoleId)> GetPlatformScopeAsync(CancellationToken ct)
    {
        var platformTenantId = await _db.Tenants.IgnoreQueryFilters()
            .Where(t => t.TenantCode == IdentityService.PlatformTenantCode)
            .Select(t => t.Id).FirstOrDefaultAsync(ct);
        if (platformTenantId == 0)
            throw new InvalidOperationException("平台租户尚未创建，请先完成数据库迁移与平台目录种子。");

        var roleId = await _db.Roles
            .Where(r => r.TenantId == 0 && r.Code == IdentityRoles.PlatformAdmin)
            .Select(r => r.Id).FirstOrDefaultAsync(ct);
        if (roleId == 0)
            throw new InvalidOperationException("平台管理员角色尚未创建，请先完成平台目录种子。");

        return (platformTenantId, roleId);
    }

    public async Task<IReadOnlyList<PlatformAdminView>> ListAsync(CancellationToken ct = default)
    {
        var (tenantId, roleId) = await GetPlatformScopeAsync(ct);
        return await _db.Users
            .Where(u => u.TenantId == tenantId &&
                        _db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == roleId))
            .OrderBy(u => u.Username)
            .Select(u => new PlatformAdminView(
                u.Id, u.Username, u.DisplayName, u.Email, u.Status, u.CreatedTime))
            .ToListAsync(ct);
    }

    public async Task<PlatformAdminView> AddAsync(AddPlatformAdminRequest request, string actor, CancellationToken ct = default)
    {
        var (tenantId, roleId) = await GetPlatformScopeAsync(ct);
        var username = (request.Username ?? string.Empty).Trim();
        var password = request.Password ?? string.Empty;
        if (username.Length == 0 || password.Length < 8)
            throw new ArgumentException("用户名必填，口令至少 8 位。");

        var existing = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Username == username, ct);

        long userId;
        if (existing is null)
        {
            // 注意：platform-admin 被 IdentityService.ResolveRoleId(s)Async 刻意排除（防越权），
            // 故不经过 CreateUserAsync 的 roleCodes，而是在这里直接授予全局治理角色（与引导器一致）。
            var created = await _identity.CreateUserAsync(tenantId, username, request.DisplayName, request.Email, null, ct);
            if (!created.Success)
                throw new InvalidOperationException($"创建平台管理员失败：{string.Join("; ", created.Errors)}");
            userId = created.Id ?? 0;
            _db.UserRoles.Add(new UserRole { TenantId = tenantId, UserId = userId, RoleId = roleId });
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            userId = existing.Id;
            var alreadyGranted = await _db.UserRoles
                .AnyAsync(ur => ur.TenantId == tenantId && ur.UserId == userId && ur.RoleId == roleId, ct);
            if (!alreadyGranted)
            {
                _db.UserRoles.Add(new UserRole { TenantId = tenantId, UserId = userId, RoleId = roleId });
            }
            if (existing.Status != UserStatus.Active)
            {
                existing.Status = UserStatus.Active;
                existing.SecurityStamp = Guid.NewGuid().ToString("N");
            }
            if (!alreadyGranted || existing.Status != UserStatus.Active)
                await _db.SaveChangesAsync(ct);
        }

        await _identity.SetPasswordAsync(tenantId, userId, password, ct);
        await _audit.LogAsync(new AuditLogEntry(
            tenantId, "platform.admin.add", "PlatformAdmin",
            UserId: userId, Actor: actor, EntityId: userId.ToString(), Result: "success"), ct);

        return (await ListAsync(ct)).First(v => v.Id == userId);
    }

    public async Task DisableAsync(long userId, string actor, CancellationToken ct = default)
    {
        var (tenantId, roleId) = await GetPlatformScopeAsync(ct);
        var activeCount = await _db.Users
            .CountAsync(u => u.TenantId == tenantId && u.Status == UserStatus.Active &&
                             _db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == roleId), ct);
        if (activeCount <= 1)
            throw new InvalidOperationException("至少必须保留一名有效平台管理员，无法停用最后一名。");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId, ct);
        if (user is null) throw new KeyNotFoundException("平台管理员不存在。");

        user.Status = UserStatus.Disabled;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(new AuditLogEntry(
            tenantId, "platform.admin.disable", "PlatformAdmin",
            UserId: userId, Actor: actor, EntityId: userId.ToString(), Result: "success"), ct);
    }

    public async Task EnableAsync(long userId, string actor, CancellationToken ct = default)
    {
        var (tenantId, roleId) = await GetPlatformScopeAsync(ct);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId, ct);
        if (user is null) throw new KeyNotFoundException("平台管理员不存在。");

        user.Status = UserStatus.Active;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(new AuditLogEntry(
            tenantId, "platform.admin.enable", "PlatformAdmin",
            UserId: userId, Actor: actor, EntityId: userId.ToString(), Result: "success"), ct);
    }

    public async Task ResetPasswordAsync(long userId, string newPassword, string actor, CancellationToken ct = default)
    {
        var (tenantId, roleId) = await GetPlatformScopeAsync(ct);
        if ((newPassword ?? string.Empty).Length < 8)
            throw new ArgumentException("新口令至少 8 位。");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId, ct);
        if (user is null) throw new KeyNotFoundException("平台管理员不存在。");

        await _identity.SetPasswordAsync(tenantId, userId, newPassword, ct);
        // SetPasswordAsync 已轮换安全戳；此处再次确保（幂等）。
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(new AuditLogEntry(
            tenantId, "platform.admin.reset-password", "PlatformAdmin",
            UserId: userId, Actor: actor, EntityId: userId.ToString(), Result: "success"), ct);
    }
}
