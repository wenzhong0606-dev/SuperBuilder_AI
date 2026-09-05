using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Organization;

namespace SuperBuilder_AI.Services.Identity;

/// <summary>
/// 平台管理员租户范围视图（M2-02）。
/// <see cref="AllTenants"/>=true 表示管理员管理全部租户（表中无范围记录，默认）；
/// 否则仅能管理 <see cref="Selected"/> 列出的租户。
/// </summary>
public sealed record PlatformAdminScopeView(
    bool AllTenants,
    IReadOnlyList<TenantScopeItem> Selected);

/// <summary>范围选中的租户摘要（含编码与名称，便于前端展示与去重校验）。</summary>
public sealed record TenantScopeItem(long TenantId, string TenantCode, string TenantName);

/// <summary>设置平台管理员租户范围请求体。</summary>
public sealed record SetPlatformAdminScopeRequest(IReadOnlyList<long> TenantIds);

/// <summary>平台管理员租户范围服务（确定性，不调 LLM）。</summary>
public interface IPlatformAdminScopeService
{
    /// <summary>管理员是否在「全部租户」模式下（表中无范围记录）。</summary>
    Task<bool> HasFullScopeAsync(long adminUserId, CancellationToken ct = default);

    /// <summary>
    /// 强制校验：调用者（adminUserId）是否被授权管理 targetTenantId。
    /// 无范围记录（默认全部）或显式包含目标租户时返回 true；否则 false。
    /// </summary>
    Task<bool> CanManageAsync(long adminUserId, long targetTenantId, CancellationToken ct = default);

    /// <summary>获取管理员被显式授权的租户 Id 列表（空列表表示默认全部）。</summary>
    Task<IReadOnlyList<long>> GetScopedTenantIdsAsync(long adminUserId, CancellationToken ct = default);

    /// <summary>获取管理员的范围视图（含选中租户摘要）。</summary>
    Task<PlatformAdminScopeView> GetScopeAsync(long adminUserId, CancellationToken ct = default);

    /// <summary>
    /// 以幂等方式设置管理员的范围：先删除既有记录，再按 tenantIds 写入。
    /// 传入空列表等价于「恢复默认全部租户」。tenantIds 中不存在的租户将被忽略（不抛错），
    /// 仅写入真实存在的租户以保证外键完整。
    /// </summary>
    Task<PlatformAdminScopeView> SetScopeAsync(long adminUserId, IReadOnlyList<long> tenantIds, string actor, CancellationToken ct = default);
}

/// <summary>
/// <see cref="IPlatformAdminScopeService"/> 权威实现。
/// 范围语义：无记录 = 全部；有记录 = 仅所列租户。范围校验为治理面强制门禁，服务于 M2-02。
/// </summary>
public sealed class PlatformAdminScopeService : IPlatformAdminScopeService
{
    private readonly SuperBIContext _db;

    public PlatformAdminScopeService(SuperBIContext db) => _db = db;

    public async Task<bool> HasFullScopeAsync(long adminUserId, CancellationToken ct = default) =>
        !await _db.PlatformAdminTenantScopes.AnyAsync(s => s.AdminUserId == adminUserId, ct);

    public async Task<bool> CanManageAsync(long adminUserId, long targetTenantId, CancellationToken ct = default)
    {
        var ids = await GetScopedTenantIdsAsync(adminUserId, ct);
        // 空范围 = 默认全部租户
        if (ids.Count == 0) return true;
        return ids.Contains(targetTenantId);
    }

    public async Task<IReadOnlyList<long>> GetScopedTenantIdsAsync(long adminUserId, CancellationToken ct = default) =>
        await _db.PlatformAdminTenantScopes
            .Where(s => s.AdminUserId == adminUserId)
            .Select(s => s.TenantId)
            .Distinct()
            .ToListAsync(ct);

    public async Task<PlatformAdminScopeView> GetScopeAsync(long adminUserId, CancellationToken ct = default)
    {
        var ids = await GetScopedTenantIdsAsync(adminUserId, ct);
        if (ids.Count == 0)
            return new PlatformAdminScopeView(true, Array.Empty<TenantScopeItem>());

        var tenants = await _db.Tenants
            .AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .Select(t => new TenantScopeItem(t.Id, t.TenantCode ?? string.Empty, t.TenantName ?? string.Empty))
            .ToListAsync(ct);
        return new PlatformAdminScopeView(false, tenants);
    }

    public async Task<PlatformAdminScopeView> SetScopeAsync(long adminUserId, IReadOnlyList<long> tenantIds, string actor, CancellationToken ct = default)
    {
        // 仅保留真实存在的租户，保证外键完整，避免写入孤儿范围。
        var existing = tenantIds.Distinct().ToArray();
        var validIds = existing.Length == 0
            ? new List<long>()
            : await _db.Tenants
                .Where(t => existing.Contains(t.Id))
                .Select(t => t.Id)
                .ToListAsync(ct);

        // 幂等替换：先清后写。
        var current = await _db.PlatformAdminTenantScopes
            .Where(s => s.AdminUserId == adminUserId)
            .ToListAsync(ct);
        _db.PlatformAdminTenantScopes.RemoveRange(current);

        // 空列表 = 恢复默认（全部租户），不写入任何范围记录。
        if (validIds.Count > 0)
        {
            var now = DateTime.UtcNow;
            foreach (var tid in validIds)
            {
                _db.PlatformAdminTenantScopes.Add(new PlatformAdminTenantScope
                {
                    AdminUserId = adminUserId,
                    TenantId = tid,
                    GrantedAt = now,
                    GrantedBy = actor
                });
            }
        }
        await _db.SaveChangesAsync(ct);
        return await GetScopeAsync(adminUserId, ct);
    }
}
