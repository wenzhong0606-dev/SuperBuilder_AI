using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.Identity;

namespace SuperBuilder_AI.Services.Identity;

/// <summary>
/// Identity/RBAC 服务实现（确定性，不调 LLM）。
/// 权限解析：User → UserRole → Role → RolePermission → Permission.Code（去重）。
/// 角色可为全局（TenantId=0）或租户特定；用户只能指派本租户或全局角色。
/// </summary>
public class IdentityService : IIdentityService
{
    private readonly SuperBIContext _ctx;

    public IdentityService(SuperBIContext ctx) => _ctx = ctx;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await EnsureGlobalCatalog(ct);
    }

    private async Task EnsureGlobalCatalog(CancellationToken ct)
    {
        // 1) 权限
        var existingPerms = new HashSet<string>(
            await _ctx.Permissions.Where(p => p.TenantId == 0).Select(p => p.Code).ToListAsync(ct));
        foreach (var perm in IdentityCatalog.Permissions)
        {
            if (existingPerms.Add(perm.Code))
            {
                _ctx.Permissions.Add(new Permission
                {
                    TenantId = 0,
                    Code = perm.Code,
                    Name = perm.Name,
                    Category = perm.Category,
                    Description = perm.Description,
                });
            }
        }
        await _ctx.SaveChangesAsync(ct);

        // 2) 角色
        var existingRoles = new HashSet<string>(
            await _ctx.Roles.Where(r => r.TenantId == 0).Select(r => r.Code).ToListAsync(ct));
        foreach (var role in IdentityCatalog.Roles)
        {
            if (existingRoles.Add(role.Code))
            {
                _ctx.Roles.Add(new Role
                {
                    TenantId = 0,
                    Code = role.Code,
                    Name = role.Name,
                    Description = role.Description,
                });
            }
        }
        await _ctx.SaveChangesAsync(ct);

        // 3) 角色→权限绑定（幂等，按 (RoleId, PermissionId) 去重）
        var roleEntities = await _ctx.Roles.Where(r => r.TenantId == 0)
            .ToDictionaryAsync(r => r.Code, r => r.Id, ct);
        var permEntities = await _ctx.Permissions.Where(p => p.TenantId == 0)
            .ToDictionaryAsync(p => p.Code, p => p.Id, ct);

        var existingBindings = new HashSet<(long, long)>(
            (await _ctx.RolePermissions.Where(rp => rp.TenantId == 0)
                .Select(rp => new { rp.RoleId, rp.PermissionId }).ToListAsync(ct))
            .Select(x => (x.RoleId, x.PermissionId)));

        foreach (var role in IdentityCatalog.Roles)
        {
            var roleId = roleEntities[role.Code];
            foreach (var permCode in role.Permissions)
            {
                var permId = permEntities[permCode];
                if (existingBindings.Add((roleId, permId)))
                {
                    _ctx.RolePermissions.Add(new RolePermission
                    {
                        TenantId = 0,
                        RoleId = roleId,
                        PermissionId = permId,
                    });
                }
            }
        }
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task<IdentityResult> CreateUserAsync(long tenantId, string username, string displayName, string email, string[]? roleCodes, CancellationToken ct = default)
    {
        if (tenantId <= 0) return IdentityResult.Fail("tenantId 必须大于 0");
        if (string.IsNullOrWhiteSpace(username)) return IdentityResult.Fail("username 必填");
        if (await _ctx.Users.AnyAsync(u => u.Username == username, ct))
            return IdentityResult.Fail($"用户名已存在: {username}");

        var user = new User
        {
            TenantId = tenantId,
            Username = username,
            DisplayName = displayName ?? username,
            Email = email ?? string.Empty,
        };
        _ctx.Users.Add(user);
        await _ctx.SaveChangesAsync(ct);

        if (roleCodes != null && roleCodes.Length > 0)
        {
            var roleIds = await ResolveRoleIdsAsync(tenantId, roleCodes, ct);
            foreach (var roleId in roleIds)
            {
                _ctx.UserRoles.Add(new UserRole { TenantId = tenantId, UserId = user.Id, RoleId = roleId });
            }
            await _ctx.SaveChangesAsync(ct);
        }
        return IdentityResult.Ok(user.Id);
    }

    public async Task<IdentityResult> AssignRoleAsync(long tenantId, long userId, string roleCode, CancellationToken ct = default)
    {
        if (tenantId <= 0) return IdentityResult.Fail("tenantId 必须大于 0");
        if (string.IsNullOrWhiteSpace(roleCode)) return IdentityResult.Fail("roleCode 必填");
        if (!await _ctx.Users.AnyAsync(u => u.Id == userId && u.TenantId == tenantId, ct))
            return IdentityResult.Fail($"用户不存在: {userId}");

        var roleId = await ResolveRoleIdAsync(tenantId, roleCode, ct);
        if (roleId == null) return IdentityResult.Fail($"角色不存在: {roleCode}");

        if (await _ctx.UserRoles.AnyAsync(ur => ur.TenantId == tenantId && ur.UserId == userId && ur.RoleId == roleId.Value, ct))
            return IdentityResult.Ok(userId);

        _ctx.UserRoles.Add(new UserRole { TenantId = tenantId, UserId = userId, RoleId = roleId.Value });
        await _ctx.SaveChangesAsync(ct);
        return IdentityResult.Ok(userId);
    }

    public async Task<IdentityResult> RevokeRoleAsync(long tenantId, long userId, string roleCode, CancellationToken ct = default)
    {
        if (tenantId <= 0) return IdentityResult.Fail("tenantId 必须大于 0");
        if (string.IsNullOrWhiteSpace(roleCode)) return IdentityResult.Fail("roleCode 必填");

        var roleId = await ResolveRoleIdAsync(tenantId, roleCode, ct);
        if (roleId == null) return IdentityResult.Fail($"角色不存在: {roleCode}");

        var link = await _ctx.UserRoles
            .FirstOrDefaultAsync(ur => ur.TenantId == tenantId && ur.UserId == userId && ur.RoleId == roleId.Value, ct);
        if (link == null) return IdentityResult.Ok(userId);

        _ctx.UserRoles.Remove(link);
        await _ctx.SaveChangesAsync(ct);
        return IdentityResult.Ok(userId);
    }

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(long tenantId, long userId, CancellationToken ct = default)
    {
        var roleIds = await _ctx.UserRoles
            .Where(ur => ur.TenantId == tenantId && ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .Distinct().ToListAsync(ct);
        if (roleIds.Count == 0) return Array.Empty<string>();

        var permIds = await _ctx.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.PermissionId)
            .Distinct().ToListAsync(ct);
        if (permIds.Count == 0) return Array.Empty<string>();

        var codes = await _ctx.Permissions
            .Where(p => permIds.Contains(p.Id))
            .Select(p => p.Code)
            .Distinct().ToListAsync(ct);
        return codes;
    }

    public async Task<bool> HasPermissionAsync(long tenantId, long userId, string permissionCode, CancellationToken ct = default)
    {
        var perms = await GetPermissionsAsync(tenantId, userId, ct);
        return perms.Contains(permissionCode);
    }

    private async Task<List<long>> ResolveRoleIdsAsync(long tenantId, IEnumerable<string> roleCodes, CancellationToken ct)
    {
        var codes = roleCodes.ToList();
        return await _ctx.Roles
            .Where(r => (r.TenantId == tenantId || r.TenantId == 0) && codes.Contains(r.Code))
            .Select(r => r.Id).ToListAsync(ct);
    }

    private async Task<long?> ResolveRoleIdAsync(long tenantId, string roleCode, CancellationToken ct)
    {
        return await _ctx.Roles
            .Where(r => (r.TenantId == tenantId || r.TenantId == 0) && r.Code == roleCode)
            .Select(r => (long?)r.Id).FirstOrDefaultAsync(ct);
    }
}
