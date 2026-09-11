using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.Auth;

namespace SuperBuilder_AI.Services.Identity;

/// <summary>
/// Identity/RBAC 服务实现（确定性，不调 LLM）。
/// 权限解析：User → UserRole → Role → RolePermission → Permission.Code（去重）。
/// 角色可为全局（TenantId=0）或租户特定；用户只能指派本租户或全局角色。
/// </summary>
public class IdentityService : IIdentityService
{
	public const string PlatformTenantCode = "platform";
    private readonly SuperBIContext _ctx;
    private readonly IPasswordHasher _hasher;

    public IdentityService(SuperBIContext ctx, IPasswordHasher hasher)
    {
        _ctx = ctx;
        _hasher = hasher;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await EnsureGlobalCatalog(ct);
        await BackfillSecurityStampsAsync(ct);
    }

    /// <summary>
    /// P0-04B 兼容回填：为尚未生成 <see cref="User.SecurityStamp"/> 的存量用户补发安全戳，
    /// 使吊销校验对全体用户生效。幂等（仅处理 NULL）。
    /// </summary>
    private async Task BackfillSecurityStampsAsync(CancellationToken ct)
    {
        var needStamp = await _ctx.Users
            .Where(u => u.SecurityStamp == null || u.SecurityStamp == string.Empty)
            .ToListAsync(ct);
        foreach (var u in needStamp)
            u.SecurityStamp = Guid.NewGuid().ToString("N");
        if (needStamp.Count > 0)
            await _ctx.SaveChangesAsync(ct);
    }

    private async Task EnsureGlobalCatalog(CancellationToken ct)
    {
		var platformTenant = await _ctx.Tenants.IgnoreQueryFilters()
			.FirstOrDefaultAsync(t => t.TenantCode == PlatformTenantCode, ct);
		if (platformTenant is null)
		{
			platformTenant = new Tenant { TenantCode = PlatformTenantCode, TenantName = "Platform Governance", Enabled = true };
			_ctx.Tenants.Add(platformTenant);
			await _ctx.SaveChangesAsync(ct);
		}

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

		// Built-in role bindings are authoritative. This removes the historical
		// business permissions from platform-admin instead of leaving additive residue.
		var platformRoleId = roleEntities[IdentityRoles.PlatformAdmin];
		var platformPermissionIds = IdentityCatalog.Roles
			.Single(r => r.Code == IdentityRoles.PlatformAdmin).Permissions
			.Select(code => permEntities[code]).ToHashSet();
		var obsoletePlatformBindings = await _ctx.RolePermissions
			.Where(rp => rp.RoleId == platformRoleId && !platformPermissionIds.Contains(rp.PermissionId))
			.ToListAsync(ct);
		_ctx.RolePermissions.RemoveRange(obsoletePlatformBindings);

		// Governance role membership is valid only for users owned by the positive-id
		// platform tenant. Historical tenant-user bindings are revoked during seeding.
		var invalidGovernanceBindings = await _ctx.UserRoles
			.Where(ur => ur.RoleId == platformRoleId && ur.TenantId != platformTenant.Id)
			.ToListAsync(ct);
		_ctx.UserRoles.RemoveRange(invalidGovernanceBindings);
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task<IdentityResult> CreateUserAsync(long tenantId, string username, string? displayName, string? email, string[]? roleCodes, CancellationToken ct = default)
    {
        if (tenantId <= 0) return IdentityResult.Fail("tenantId 必须大于 0");
        if (string.IsNullOrWhiteSpace(username)) return IdentityResult.Fail("username 必填");

        // M1-03：User→Tenant 一致性（同 M1-02 的 TenantCode 策略）——DB 级 FK 延后，避免破坏以
        // new User{TenantId=N} 直接注入且不建对应租户行的集成测试种子；此处仅校验 tenantId>0，
        // 完整外键与存在性校验列入后续硬化项（见 Master_Development_Plan.md）。
        var normalized = User.NormalizeUsername(username);
        if (await _ctx.Users.AnyAsync(u => u.TenantId == tenantId && u.NormalizedUsername == normalized, ct))
            return IdentityResult.Fail($"该租户内用户名已存在: {username}");

        var user = new User
        {
            TenantId = tenantId,
            Username = username,
            NormalizedUsername = normalized,
            DisplayName = displayName ?? username,
            Email = email ?? string.Empty,
            NormalizedEmail = User.NormalizeEmail(email),
            EmailConfirmed = false,
            SecurityStamp = Guid.NewGuid().ToString("N"),
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
        await RotateSecurityStampAsync(tenantId, userId, ct);
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
        await RotateSecurityStampAsync(tenantId, userId, ct);
        return IdentityResult.Ok(userId);
    }

    public async Task<IdentityResult> SetPasswordAsync(long tenantId, long userId, string password, CancellationToken ct = default)
    {
        if (tenantId <= 0) return IdentityResult.Fail("tenantId 必须大于 0");
        if (string.IsNullOrWhiteSpace(password)) return IdentityResult.Fail("password 必填");
        if (!await _ctx.Users.AnyAsync(u => u.Id == userId && u.TenantId == tenantId, ct))
            return IdentityResult.Fail($"用户不存在: {userId}");

        var user = await _ctx.Users.FirstAsync(u => u.Id == userId && u.TenantId == tenantId, ct);
        user.PasswordHash = _hasher.Hash(password);
        // 口令变更即轮换安全戳，使所有旧令牌失效（P0-04B）。
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await _ctx.SaveChangesAsync(ct);
        return IdentityResult.Ok(userId);
    }

    /// <summary>
    /// 设置用户状态（M1-03：用户状态机 + 停用轮换）。仅允许 <see cref="UserStatus.Active"/> ↔
    /// <see cref="UserStatus.Disabled"/> 切换；状态变更即轮换 <see cref="User.SecurityStamp"/>，
    /// 使既有令牌在下次请求时失效（下次请求 401，须重新登录）。
    /// </summary>
    public async Task<IdentityResult> SetUserStatusAsync(long tenantId, long userId, UserStatus newStatus, CancellationToken ct = default)
    {
        if (tenantId <= 0) return IdentityResult.Fail("tenantId 必须大于 0");
        var user = await _ctx.Users.FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, ct);
        if (user is null) return IdentityResult.Fail($"用户不存在: {userId}");
        if (user.Status == newStatus) return IdentityResult.Ok(userId);

        // 状态机：仅允许 Active <-> Disabled 切换。
        var allowed = (user.Status, newStatus) switch
        {
            (UserStatus.Active, UserStatus.Disabled) => true,
            (UserStatus.Disabled, UserStatus.Active) => true,
            _ => false,
        };
        if (!allowed) return IdentityResult.Fail($"不允许的状态切换: {user.Status} -> {newStatus}");

        user.Status = newStatus;
        // 停用/启用即轮换安全戳，使既有令牌失效（M1-03：停用后轮换）。
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await _ctx.SaveChangesAsync(ct);
        return IdentityResult.Ok(userId);
    }

    /// <summary>
    /// P0-04B 吊销：角色/权限变更后轮换用户安全戳，使既有令牌立即失效（下次请求 401，须重新登录）。
    /// 用户不存在时静默跳过（调用方已先行校验）。
    /// </summary>
    private async Task RotateSecurityStampAsync(long tenantId, long userId, CancellationToken ct)
    {
        var rows = await _ctx.Users
            .Where(u => u.Id == userId && u.TenantId == tenantId)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.SecurityStamp, Guid.NewGuid().ToString("N")), ct);
        // ExecuteUpdateAsync 在部分提供程序返回受影响行数；用户存在性已由调用方保证。
        _ = rows;
    }

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(long tenantId, long userId, CancellationToken ct = default)
    {
        // 直接角色（User → UserRole → Role）。
        var directRoleIds = await _ctx.UserRoles
            .Where(ur => ur.TenantId == tenantId && ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync(ct);

        // M12-17：并入「用户 → 用户组 → 角色」链路——用户组承载的角色对其成员生效。
        // 有效角色 = 直接角色 ∪ 组角色；两者并集后统一解析权限码。
        // M12 增量：仅**已启用**的用户组参与（停用组的角色不再计入有效权限）。
        var groupRoleIds = await (
            from m in _ctx.UserGroupMembers.IgnoreQueryFilters()
            join g in _ctx.UserGroups.IgnoreQueryFilters() on m.UserGroupId equals g.Id
            where m.TenantId == tenantId && m.UserId == userId && g.IsEnabled
            join gr in _ctx.UserGroupRoles.IgnoreQueryFilters() on m.UserGroupId equals gr.UserGroupId
            where gr.TenantId == tenantId
            select gr.RoleId).ToListAsync(ct);

        var roleIds = directRoleIds.Concat(groupRoleIds).Distinct().ToList();
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
		var codes = roleCodes.Where(code => code != IdentityRoles.PlatformAdmin).ToList();
        return await _ctx.Roles
            .Where(r => (r.TenantId == tenantId || r.TenantId == 0) && codes.Contains(r.Code))
            .Select(r => r.Id).ToListAsync(ct);
    }

    private async Task<long?> ResolveRoleIdAsync(long tenantId, string roleCode, CancellationToken ct)
    {
		if (roleCode == IdentityRoles.PlatformAdmin) return null;
        return await _ctx.Roles
            .Where(r => (r.TenantId == tenantId || r.TenantId == 0) && r.Code == roleCode)
            .Select(r => (long?)r.Id).FirstOrDefaultAsync(ct);
    }
}
