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
/// Identity 组织目录服务（M12-17，确定性、不调 LLM）。
/// <para>
/// 组织 / 部门构成本租户的组织结构；用户组承载 RBAC 角色，其成员经组角色获得权限
/// （有效权限 = 直接 <see cref="UserRole"/> ∪ 组角色 <see cref="UserGroupRole"/>，见 <see cref="IdentityService.GetPermissionsAsync"/>）。
/// </para>
/// <para>
/// 所有查询使用 <c>IgnoreQueryFilters()</c> + 显式 <c>TenantId</c> 过滤：
/// 行为不依赖调用方是否已开启租户作用域过滤器，跨租户数据恒不可见。
/// </para>
/// </summary>
public sealed class IdentityDirectoryService : IIdentityDirectoryService
{
    private readonly SuperBIContext _ctx;

    public IdentityDirectoryService(SuperBIContext ctx) => _ctx = ctx;

    // ---------------- 组织 ----------------

    public async Task<DirectoryPage<OrganizationView>> ListOrganizationsAsync(long tenantId, int page = 0, int pageSize = 0, CancellationToken ct = default)
    {
        var query = _ctx.Organizations.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Code).ThenBy(x => x.Id);

        var total = await query.CountAsync(ct);
        if (pageSize <= 0) pageSize = 0;

        var orgs = pageSize > 0
            ? await query.Skip(Math.Max(0, page - 1) * pageSize).Take(pageSize).ToListAsync(ct)
            : await query.ToListAsync(ct);

        // 计数只针对当前页涉及的实体，避免全量分组。
        var ids = orgs.Select(o => o.Id).ToList();
        var map = new Dictionary<long, int>();
        if (ids.Count > 0)
        {
            map = (await _ctx.Departments.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.TenantId == tenantId && ids.Contains(x.OrganizationId))
                .GroupBy(x => x.OrganizationId)
                .Select(g => new { OrganizationId = g.Key, Count = g.Count() })
                .ToListAsync(ct)).ToDictionary(x => x.OrganizationId, x => x.Count);
        }

        var items = orgs.Select(o => new OrganizationView(
            o.Id, o.Code, o.Name, o.Description, o.IsEnabled,
            map.TryGetValue(o.Id, out var c) ? c : 0)).ToList();

        return pageSize > 0
            ? new DirectoryPage<OrganizationView>(items, total, Math.Max(1, page), pageSize)
            : DirectoryPage<OrganizationView>.OfAll(items);
    }

    public async Task<IdentityResult> CreateOrganizationAsync(long tenantId, string code, string? name, string? description, CancellationToken ct = default)
    {
        if (tenantId <= 0) return IdentityResult.Fail("tenantId 必须 > 0。");
        if (string.IsNullOrWhiteSpace(code)) return IdentityResult.Fail("code 必填。");

        var norm = Organization.NormalizeCode(code);
        var exists = await _ctx.Organizations.IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenantId && x.NormalizedCode == norm, ct);
        if (exists) return IdentityResult.Fail($"组织编码已存在：{code}。");

        var org = new Organization
        {
            TenantId = tenantId,
            Code = code.Trim(),
            NormalizedCode = norm,
            Name = string.IsNullOrWhiteSpace(name) ? code.Trim() : name!.Trim(),
            Description = description?.Trim() ?? string.Empty,
            IsEnabled = true,
        };
        _ctx.Organizations.Add(org);
        await _ctx.SaveChangesAsync(ct);
        return IdentityResult.Ok(org.Id);
    }

    // ---------------- 组织：M12 增量（重命名 / 启停 / 删除） ----------------

    public async Task<IdentityResult> UpdateOrganizationAsync(long tenantId, long id, string? name, string? description, CancellationToken ct = default)
    {
        var org = await _ctx.Organizations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        if (org is null) return IdentityResult.Fail("组织不存在或不属于当前租户。");

        if (name is not null && !string.IsNullOrWhiteSpace(name)) org.Name = name.Trim();
        if (description is not null) org.Description = description.Trim();
        await _ctx.SaveChangesAsync(ct);
        return IdentityResult.Ok(org.Id);
    }

    public async Task<IdentityResult> SetOrganizationEnabledAsync(long tenantId, long id, bool enabled, CancellationToken ct = default)
    {
        var org = await _ctx.Organizations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        if (org is null) return IdentityResult.Fail("组织不存在或不属于当前租户。");

        org.IsEnabled = enabled;
        await _ctx.SaveChangesAsync(ct);
        return IdentityResult.Ok(org.Id);
    }

    public async Task<IdentityResult> DeleteOrganizationAsync(long tenantId, long id, CancellationToken ct = default)
    {
        var org = await _ctx.Organizations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        if (org is null) return IdentityResult.Fail("组织不存在或不属于当前租户。");

        var deptCount = await _ctx.Departments.IgnoreQueryFilters()
            .CountAsync(x => x.TenantId == tenantId && x.OrganizationId == id, ct);
        if (deptCount > 0) return IdentityResult.Fail($"组织下仍有 {deptCount} 个部门，请先删除或移出。");

        _ctx.Organizations.Remove(org);
        await _ctx.SaveChangesAsync(ct);
        return IdentityResult.Ok(id);
    }

    // ---------------- 部门 ----------------

    public async Task<DirectoryPage<DepartmentView>> ListDepartmentsAsync(long tenantId, int page = 0, int pageSize = 0, CancellationToken ct = default)
    {
        var query = _ctx.Departments.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.OrganizationId).ThenBy(x => x.Code).ThenBy(x => x.Id);

        var total = await query.CountAsync(ct);
        var depts = pageSize > 0
            ? await query.Skip(Math.Max(0, page - 1) * pageSize).Take(pageSize).ToListAsync(ct)
            : await query.ToListAsync(ct);

        var orgNames = await _ctx.Organizations.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => new { x.Id, x.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var ids = depts.Select(d => d.Id).ToList();
        var countMap = new Dictionary<long, int>();
        if (ids.Count > 0)
        {
            countMap = (await _ctx.UserDepartmentMembers.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.TenantId == tenantId && ids.Contains(x.DepartmentId))
                .GroupBy(x => x.DepartmentId)
                .Select(g => new { DepartmentId = g.Key, Count = g.Count() })
                .ToListAsync(ct)).ToDictionary(x => x.DepartmentId, x => x.Count);
        }

        var items = depts.Select(d => new DepartmentView(
            d.Id, d.OrganizationId,
            orgNames.TryGetValue(d.OrganizationId, out var n) ? n : string.Empty,
            d.ParentId, d.Code, d.Name, d.Description, d.IsEnabled,
            countMap.TryGetValue(d.Id, out var c) ? c : 0)).ToList();

        return pageSize > 0
            ? new DirectoryPage<DepartmentView>(items, total, Math.Max(1, page), pageSize)
            : DirectoryPage<DepartmentView>.OfAll(items);
    }

    public async Task<IdentityResult> CreateDepartmentAsync(long tenantId, long organizationId, long? parentId, string code, string? name, string? description, CancellationToken ct = default)
    {
        if (tenantId <= 0) return IdentityResult.Fail("tenantId 必须 > 0。");
        if (organizationId <= 0) return IdentityResult.Fail("organizationId 必填。");
        if (string.IsNullOrWhiteSpace(code)) return IdentityResult.Fail("code 必填。");

        var orgExists = await _ctx.Organizations.IgnoreQueryFilters()
            .AnyAsync(x => x.Id == organizationId && x.TenantId == tenantId, ct);
        if (!orgExists) return IdentityResult.Fail("组织不存在或不属于当前租户。");

        if (parentId.HasValue)
        {
            var parentOk = await _ctx.Departments.IgnoreQueryFilters()
                .AnyAsync(x => x.Id == parentId.Value && x.TenantId == tenantId, ct);
            if (!parentOk) return IdentityResult.Fail("上级部门不存在或不属于当前租户。");
        }

        var norm = Department.NormalizeCode(code);
        var exists = await _ctx.Departments.IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenantId && x.NormalizedCode == norm, ct);
        if (exists) return IdentityResult.Fail($"部门编码已存在：{code}。");

        var dept = new Department
        {
            TenantId = tenantId,
            OrganizationId = organizationId,
            ParentId = parentId,
            Code = code.Trim(),
            NormalizedCode = norm,
            Name = string.IsNullOrWhiteSpace(name) ? code.Trim() : name!.Trim(),
            Description = description?.Trim() ?? string.Empty,
            IsEnabled = true,
        };
        _ctx.Departments.Add(dept);
        await _ctx.SaveChangesAsync(ct);
        return IdentityResult.Ok(dept.Id);
    }

    // ---------------- 部门：M12 增量（重命名 / 启停 / 删除） ----------------

    public async Task<IdentityResult> UpdateDepartmentAsync(
        long tenantId, long id, string? name, string? description, long? organizationId, long? parentId, CancellationToken ct = default)
    {
        var dept = await _ctx.Departments.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        if (dept is null) return IdentityResult.Fail("部门不存在或不属于当前租户。");

        var targetOrgId = organizationId ?? dept.OrganizationId;
        if (targetOrgId != dept.OrganizationId)
        {
            var orgOk = await _ctx.Organizations.IgnoreQueryFilters()
                .AnyAsync(x => x.Id == targetOrgId && x.TenantId == tenantId, ct);
            if (!orgOk) return IdentityResult.Fail("目标组织不存在或不属于当前租户。");
            dept.OrganizationId = targetOrgId;
        }

        if (parentId.HasValue)
        {
            if (parentId.Value == id) return IdentityResult.Fail("上级部门不能是自身。");
            var parentOk = await _ctx.Departments.IgnoreQueryFilters()
                .AnyAsync(x => x.Id == parentId.Value && x.TenantId == tenantId, ct);
            if (!parentOk) return IdentityResult.Fail("上级部门不存在或不属于当前租户。");
            dept.ParentId = parentId;
        }

        if (name is not null && !string.IsNullOrWhiteSpace(name)) dept.Name = name.Trim();
        if (description is not null) dept.Description = description.Trim();
        await _ctx.SaveChangesAsync(ct);
        return IdentityResult.Ok(dept.Id);
    }

    public async Task<IdentityResult> SetDepartmentEnabledAsync(long tenantId, long id, bool enabled, CancellationToken ct = default)
    {
        var dept = await _ctx.Departments.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        if (dept is null) return IdentityResult.Fail("部门不存在或不属于当前租户。");

        dept.IsEnabled = enabled;
        await _ctx.SaveChangesAsync(ct);
        return IdentityResult.Ok(dept.Id);
    }

    public async Task<IdentityResult> DeleteDepartmentAsync(long tenantId, long id, CancellationToken ct = default)
    {
        var dept = await _ctx.Departments.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        if (dept is null) return IdentityResult.Fail("部门不存在或不属于当前租户。");

        var childCount = await _ctx.Departments.IgnoreQueryFilters()
            .CountAsync(x => x.TenantId == tenantId && x.ParentId == id, ct);
        if (childCount > 0) return IdentityResult.Fail($"该部门下仍有 {childCount} 个子部门，请先删除或改挂。");

        var memberCount = await _ctx.UserDepartmentMembers.IgnoreQueryFilters()
            .CountAsync(x => x.TenantId == tenantId && x.DepartmentId == id, ct);
        if (memberCount > 0) return IdentityResult.Fail($"该部门仍有 {memberCount} 名成员，请先移出。");

        _ctx.Departments.Remove(dept);
        await _ctx.SaveChangesAsync(ct);
        return IdentityResult.Ok(id);
    }

    // ---------------- 用户组 ----------------

    public async Task<DirectoryPage<UserGroupView>> ListUserGroupsAsync(long tenantId, int page = 0, int pageSize = 0, CancellationToken ct = default)
    {
        var query = _ctx.UserGroups.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Code).ThenBy(x => x.Id);

        var total = await query.CountAsync(ct);
        var groups = pageSize > 0
            ? await query.Skip(Math.Max(0, page - 1) * pageSize).Take(pageSize).ToListAsync(ct)
            : await query.ToListAsync(ct);

        var groupIds = groups.Select(g => g.Id).ToList();

        var roleLinks = new List<(long GroupId, long RoleId)>();
        if (groupIds.Count > 0)
        {
            // 注意：不使用 ValueTuple 作投影（部分提供程序物化不稳定），改用匿名类型再本地映射。
            var raw = await _ctx.UserGroupRoles.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.TenantId == tenantId && groupIds.Contains(x.UserGroupId))
                .Select(x => new { x.UserGroupId, x.RoleId })
                .ToListAsync(ct);
            roleLinks = raw.Select(x => (x.UserGroupId, x.RoleId)).ToList();
        }

        var roleIds = roleLinks.Select(x => x.RoleId).Distinct().ToList();
        var roleCodes = new Dictionary<long, string>();
        if (roleIds.Count > 0)
        {
            var rawRoles = await _ctx.Roles.IgnoreQueryFilters().AsNoTracking()
                .Where(r => roleIds.Contains(r.Id))
                .Select(r => new { r.Id, r.Code })
                .ToListAsync(ct);
            foreach (var r in rawRoles) roleCodes[r.Id] = r.Code;
        }

        var memberCounts = groupIds.Count == 0
            ? new Dictionary<long, int>()
            : (await _ctx.UserGroupMembers.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.TenantId == tenantId && groupIds.Contains(x.UserGroupId))
                .GroupBy(x => x.UserGroupId)
                .Select(g => new { GroupId = g.Key, Count = g.Count() })
                .ToListAsync(ct)).ToDictionary(x => x.GroupId, x => x.Count);

        var items = groups.Select(g =>
        {
            var codes = roleLinks
                .Where(x => x.GroupId == g.Id && roleCodes.ContainsKey(x.RoleId))
                .Select(x => roleCodes[x.RoleId])
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();
            return new UserGroupView(
                g.Id, g.Code, g.Name, g.Description, g.IsEnabled, codes,
                memberCounts.TryGetValue(g.Id, out var c) ? c : 0);
        }).ToList();

        return pageSize > 0
            ? new DirectoryPage<UserGroupView>(items, total, Math.Max(1, page), pageSize)
            : DirectoryPage<UserGroupView>.OfAll(items);
    }

    public async Task<IdentityResult> CreateUserGroupAsync(long tenantId, string code, string? name, string? description, string[]? roleCodes, CancellationToken ct = default)
    {
        if (tenantId <= 0) return IdentityResult.Fail("tenantId 必须 > 0。");
        if (string.IsNullOrWhiteSpace(code)) return IdentityResult.Fail("code 必填。");

        var norm = UserGroup.NormalizeCode(code);
        var exists = await _ctx.UserGroups.IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenantId && x.NormalizedCode == norm, ct);
        if (exists) return IdentityResult.Fail($"用户组编码已存在：{code}。");

        var group = new UserGroup
        {
            TenantId = tenantId,
            Code = code.Trim(),
            NormalizedCode = norm,
            Name = string.IsNullOrWhiteSpace(name) ? code.Trim() : name!.Trim(),
            Description = description?.Trim() ?? string.Empty,
            IsEnabled = true,
        };
        _ctx.UserGroups.Add(group);
        await _ctx.SaveChangesAsync(ct);

        if (roleCodes is { Length: > 0 })
        {
            var roleIds = await ResolveRoleIdsAsync(tenantId, roleCodes, ct);
            foreach (var roleId in roleIds)
                _ctx.UserGroupRoles.Add(new UserGroupRole { TenantId = tenantId, UserGroupId = group.Id, RoleId = roleId });
            await _ctx.SaveChangesAsync(ct);
        }

        return IdentityResult.Ok(group.Id);
    }

    // ---------------- 用户组：M12 增量（重命名 / 启停 / 删除） ----------------

    public async Task<IdentityResult> UpdateUserGroupAsync(long tenantId, long id, string? name, string? description, CancellationToken ct = default)
    {
        var group = await _ctx.UserGroups.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        if (group is null) return IdentityResult.Fail("用户组不存在或不属于当前租户。");

        if (name is not null && !string.IsNullOrWhiteSpace(name)) group.Name = name.Trim();
        if (description is not null) group.Description = description.Trim();
        await _ctx.SaveChangesAsync(ct);
        return IdentityResult.Ok(group.Id);
    }

    public async Task<IdentityResult> SetUserGroupEnabledAsync(long tenantId, long id, bool enabled, CancellationToken ct = default)
    {
        var group = await _ctx.UserGroups.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        if (group is null) return IdentityResult.Fail("用户组不存在或不属于当前租户。");

        var changed = group.IsEnabled != enabled;
        group.IsEnabled = enabled;
        await _ctx.SaveChangesAsync(ct);
        // CACHE-01：组停用/启用即整体收回/授予组角色权限；轮换全部成员安全戳，使携旧权限的令牌失效。
        if (changed)
            await RotateSecurityStampsAsync(tenantId, await GetUserGroupMemberIdsAsync(tenantId, id, ct), ct);
        return IdentityResult.Ok(group.Id);
    }

    public async Task<IdentityResult> DeleteUserGroupAsync(long tenantId, long id, CancellationToken ct = default)
    {
        var group = await _ctx.UserGroups.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        if (group is null) return IdentityResult.Fail("用户组不存在或不属于当前租户。");

        var members = await _ctx.UserGroupMembers.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.UserGroupId == id).ToListAsync(ct);
        var roles = await _ctx.UserGroupRoles.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.UserGroupId == id).ToListAsync(ct);
        var memberIds = members.Select(m => m.UserId).ToList();

        if (members.Count > 0) _ctx.UserGroupMembers.RemoveRange(members);
        if (roles.Count > 0) _ctx.UserGroupRoles.RemoveRange(roles);
        _ctx.UserGroups.Remove(group);
        await _ctx.SaveChangesAsync(ct);
        // CACHE-01：删组即收回全部成员的组角色权限；轮换成员安全戳使携旧权限的令牌失效。
        await RotateSecurityStampsAsync(tenantId, memberIds, ct);
        return IdentityResult.Ok(id);
    }

    public async Task<IdentityResult> SetUserGroupRolesAsync(long tenantId, long groupId, string[]? roleCodes, CancellationToken ct = default)
    {
        var group = await _ctx.UserGroups.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == groupId && x.TenantId == tenantId, ct);
        if (group is null) return IdentityResult.Fail("用户组不存在或不属于当前租户。");

        var roleIds = await ResolveRoleIdsAsync(tenantId, roleCodes ?? Array.Empty<string>(), ct);

        var existing = await _ctx.UserGroupRoles.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.UserGroupId == groupId)
            .ToListAsync(ct);
        var beforeRoleIds = existing.Select(x => x.RoleId).ToHashSet();
        _ctx.UserGroupRoles.RemoveRange(existing);
        foreach (var roleId in roleIds)
            _ctx.UserGroupRoles.Add(new UserGroupRole { TenantId = tenantId, UserGroupId = groupId, RoleId = roleId });
        await _ctx.SaveChangesAsync(ct);
        // CACHE-01：组角色集变化即改变全体成员的有效权限；轮换成员安全戳使携旧权限的令牌失效。
        // 角色集未变时跳过，避免无谓的强制重登。
        if (!beforeRoleIds.SetEquals(roleIds))
            await RotateSecurityStampsAsync(tenantId, await GetUserGroupMemberIdsAsync(tenantId, groupId, ct), ct);
        return IdentityResult.Ok(groupId);
    }

    public async Task<IdentityResult> AddUserGroupMemberAsync(long tenantId, long groupId, long userId, CancellationToken ct = default)
    {
        var groupOk = await _ctx.UserGroups.IgnoreQueryFilters()
            .AnyAsync(x => x.Id == groupId && x.TenantId == tenantId, ct);
        if (!groupOk) return IdentityResult.Fail("用户组不存在或不属于当前租户。");

        var userOk = await _ctx.Users.IgnoreQueryFilters()
            .AnyAsync(x => x.Id == userId && x.TenantId == tenantId, ct);
        if (!userOk) return IdentityResult.Fail("用户不存在或不属于当前租户。");

        var exists = await _ctx.UserGroupMembers.IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenantId && x.UserGroupId == groupId && x.UserId == userId, ct);
        if (!exists)
        {
            _ctx.UserGroupMembers.Add(new UserGroupMember { TenantId = tenantId, UserGroupId = groupId, UserId = userId });
            await _ctx.SaveChangesAsync(ct);
            // CACHE-01：成员入组即经组角色获得权限；轮换其安全戳，使不含新权限的旧令牌失效，
            // 强制重新登录以取得最新权限（与 IdentityService.AssignRoleAsync 的吊销策略一致）。
            await RotateSecurityStampsAsync(tenantId, new[] { userId }, ct);
        }
        return IdentityResult.Ok(groupId);
    }

    public async Task<IdentityResult> RemoveUserGroupMemberAsync(long tenantId, long groupId, long userId, CancellationToken ct = default)
    {
        var groupOk = await _ctx.UserGroups.IgnoreQueryFilters()
            .AnyAsync(x => x.Id == groupId && x.TenantId == tenantId, ct);
        if (!groupOk) return IdentityResult.Fail("用户组不存在或不属于当前租户。");

        var existing = await _ctx.UserGroupMembers.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.UserGroupId == groupId && x.UserId == userId)
            .ToListAsync(ct);
        if (existing.Count > 0)
        {
            _ctx.UserGroupMembers.RemoveRange(existing);
            await _ctx.SaveChangesAsync(ct);
            // CACHE-01：移出成员即收回其组角色权限；轮换该用户安全戳，使携旧权限的令牌立即失效。
            await RotateSecurityStampsAsync(tenantId, new[] { userId }, ct);
        }
        return IdentityResult.Ok(groupId);
    }

    public async Task<IdentityResult> SetUserDepartmentAsync(long tenantId, long userId, long? departmentId, CancellationToken ct = default)
    {
        var userOk = await _ctx.Users.IgnoreQueryFilters()
            .AnyAsync(x => x.Id == userId && x.TenantId == tenantId, ct);
        if (!userOk) return IdentityResult.Fail("用户不存在或不属于当前租户。");

        if (departmentId.HasValue)
        {
            var deptOk = await _ctx.Departments.IgnoreQueryFilters()
                .AnyAsync(x => x.Id == departmentId.Value && x.TenantId == tenantId, ct);
            if (!deptOk) return IdentityResult.Fail("部门不存在或不属于当前租户。");
        }

        var existing = await _ctx.UserDepartmentMembers.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.UserId == userId)
            .ToListAsync(ct);
        _ctx.UserDepartmentMembers.RemoveRange(existing);
        if (departmentId.HasValue)
            _ctx.UserDepartmentMembers.Add(new UserDepartmentMember { TenantId = tenantId, UserId = userId, DepartmentId = departmentId.Value });
        await _ctx.SaveChangesAsync(ct);
        return IdentityResult.Ok(userId);
    }

    // ---------------- 内部 ----------------

    /// <summary>
    /// CACHE-01：轮换指定用户在租户内的安全戳。
    /// <para>
    /// 令牌内嵌签发时的权限声明（含组角色），AuthMiddleware 每请求以安全戳比对判定是否已吊销。
    /// 组角色/成员变更会改变用户的**有效权限**，但不会改动其直接角色，故须在此显式轮换安全戳，
    /// 使携旧权限的令牌在下次请求即 401（重新登录后取得最新权限）——与 <see cref="IdentityService"/>
    /// 的角色/口令/状态变更吊销策略一致。
    /// </para>
    /// <para>仅影响传入用户，显式 <c>TenantId</c> 过滤，绝不跨租户。</para>
    /// </summary>
    private async Task RotateSecurityStampsAsync(long tenantId, IEnumerable<long> userIds, CancellationToken ct)
    {
        if (tenantId <= 0) return;
        var ids = userIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0) return;

        await _ctx.Users.IgnoreQueryFilters()
            .Where(u => u.TenantId == tenantId && ids.Contains(u.Id))
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.SecurityStamp, Guid.NewGuid().ToString("N")), ct);
    }

    /// <summary>取用户组的成员用户 Id（仅本租户）。</summary>
    private async Task<List<long>> GetUserGroupMemberIdsAsync(long tenantId, long groupId, CancellationToken ct) =>
        await _ctx.UserGroupMembers.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.UserGroupId == groupId)
            .Select(x => x.UserId)
            .ToListAsync(ct);

    /// <summary>按角色码解析角色 Id（同租户角色 + 全局角色；platform-admin 不可指派）。</summary>
    private async Task<List<long>> ResolveRoleIdsAsync(long tenantId, IEnumerable<string> roleCodes, CancellationToken ct)
    {
        var codes = roleCodes.Where(c => !string.IsNullOrWhiteSpace(c) && c != IdentityRoles.PlatformAdmin)
            .Select(c => c.Trim()).Distinct().ToList();
        if (codes.Count == 0) return new List<long>();

        return await _ctx.Roles.IgnoreQueryFilters().AsNoTracking()
            .Where(r => (r.TenantId == tenantId || r.TenantId == 0) && codes.Contains(r.Code))
            .Select(r => r.Id).Distinct().ToListAsync(ct);
    }
}
