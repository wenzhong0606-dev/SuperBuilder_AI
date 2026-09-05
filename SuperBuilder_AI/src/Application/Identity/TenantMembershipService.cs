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
/// 多租户成员关系服务实现（M2-05 / SB-P1-16）。
/// <para>
/// 主租户（用户 User.TenantId）恒为隐式成员，不写入 UserTenant 表；仅额外可切换租户落表。
/// 全部读取/写入走同一注入 <see cref="SuperBIContext"/>，可被调用方事务包含。
/// </para>
/// </summary>
public sealed class TenantMembershipService : ITenantMembershipService
{
	private readonly SuperBIContext _db;

	public TenantMembershipService(SuperBIContext db)
	{
		_db = db;
	}

	public async Task AddMemberAsync(long userId, long tenantId, long? createdBy, CancellationToken ct = default)
	{
		if (userId <= 0) throw new ArgumentException("userId 必须大于 0", nameof(userId));
		if (tenantId <= 0) throw new ArgumentException("tenantId 必须大于 0", nameof(tenantId));

		var homeTenantId = await _db.Users.AsNoTracking()
			.Where(u => u.Id == userId)
			.Select(u => (long?)u.TenantId)
			.FirstOrDefaultAsync(ct);
		if (homeTenantId is null)
			throw new InvalidOperationException($"用户 {userId} 不存在。");
		// 主租户为隐式成员，无需显式记录。
		if (homeTenantId.Value == tenantId)
			return;

		var tenantExists = await _db.Tenants.AsNoTracking().AnyAsync(t => t.Id == tenantId, ct);
		if (!tenantExists)
			throw new InvalidOperationException($"目标租户 {tenantId} 不存在。");

		var already = await _db.UserTenants.AnyAsync(x => x.UserId == userId && x.TenantId == tenantId, ct);
		if (already) return;

		_db.UserTenants.Add(new UserTenant
		{
			UserId = userId,
			TenantId = tenantId,
			IsDefault = false,
			CreatedAtUtc = DateTime.UtcNow,
			CreatedByUserId = createdBy,
		});
		await _db.SaveChangesAsync(ct);
	}

	public async Task RemoveMemberAsync(long userId, long tenantId, CancellationToken ct = default)
	{
		if (userId <= 0) throw new ArgumentException("userId 必须大于 0", nameof(userId));
		if (tenantId <= 0) throw new ArgumentException("tenantId 必须大于 0", nameof(tenantId));

		var homeTenantId = await _db.Users.AsNoTracking()
			.Where(u => u.Id == userId)
			.Select(u => (long?)u.TenantId)
			.FirstOrDefaultAsync(ct);
		if (homeTenantId is null)
			throw new InvalidOperationException($"用户 {userId} 不存在。");
		if (homeTenantId.Value == tenantId)
			throw new InvalidOperationException("主租户成员关系不可移除。");

		var row = await _db.UserTenants
			.FirstOrDefaultAsync(x => x.UserId == userId && x.TenantId == tenantId, ct);
		if (row is null) return;
		_db.UserTenants.Remove(row);
		await _db.SaveChangesAsync(ct);
	}

	public async Task SetDefaultAsync(long userId, long tenantId, CancellationToken ct = default)
	{
		if (userId <= 0) throw new ArgumentException("userId 必须大于 0", nameof(userId));
		if (tenantId <= 0) throw new ArgumentException("tenantId 必须大于 0", nameof(tenantId));

		// 主租户恒为默认；显式成员置为默认并清除其余默认标记。
		var others = await _db.UserTenants.Where(x => x.UserId == userId && x.TenantId != tenantId).ToListAsync(ct);
		foreach (var o in others) o.IsDefault = false;
		var target = await _db.UserTenants
			.FirstOrDefaultAsync(x => x.UserId == userId && x.TenantId == tenantId, ct);
		if (target is not null) target.IsDefault = true;
		if (others.Count > 0 || target is not null)
			await _db.SaveChangesAsync(ct);
	}

	public async Task<bool> IsMemberAsync(long userId, long tenantId, CancellationToken ct = default)
	{
		if (userId <= 0 || tenantId <= 0) return false;
		var homeTenantId = await _db.Users.AsNoTracking()
			.Where(u => u.Id == userId)
			.Select(u => (long?)u.TenantId)
			.FirstOrDefaultAsync(ct);
		if (homeTenantId is null) return false;
		if (homeTenantId.Value == tenantId) return true;
		return await _db.UserTenants.AnyAsync(x => x.UserId == userId && x.TenantId == tenantId, ct);
	}

	public async Task<IReadOnlyList<TenantMembershipView>> GetMembershipsAsync(long userId, CancellationToken ct = default)
	{
		var user = await _db.Users.AsNoTracking()
			.FirstOrDefaultAsync(u => u.Id == userId, ct);
		if (user is null) return Array.Empty<TenantMembershipView>();

		var homeTenant = await _db.Tenants.AsNoTracking()
			.Where(t => t.Id == user.TenantId)
			.Select(t => new { t.Id, t.TenantCode, t.TenantName, t.Enabled })
			.FirstOrDefaultAsync(ct);

		var result = new List<TenantMembershipView>();
		if (homeTenant is not null)
		{
			result.Add(new TenantMembershipView(
				homeTenant.Id, homeTenant.TenantCode, homeTenant.TenantName, true, homeTenant.Enabled));
		}

		var members = await _db.UserTenants.AsNoTracking()
			.Where(m => m.UserId == userId)
			.Join(_db.Tenants.AsNoTracking(),
				m => m.TenantId, t => t.Id,
				(m, t) => new { t.Id, t.TenantCode, t.TenantName, t.Enabled, m.IsDefault })
			.ToListAsync(ct);
		foreach (var m in members)
			result.Add(new TenantMembershipView(m.Id, m.TenantCode, m.TenantName, m.IsDefault, m.Enabled));

		return result;
	}

	public async Task<IReadOnlyList<long>> GetSwitchableTenantIdsAsync(long userId, CancellationToken ct = default)
	{
		var homeTenantId = await _db.Users.AsNoTracking()
			.Where(u => u.Id == userId)
			.Select(u => (long?)u.TenantId)
			.FirstOrDefaultAsync(ct);
		var ids = new List<long>();
		if (homeTenantId is not null) ids.Add(homeTenantId.Value);
		var extra = await _db.UserTenants.AsNoTracking()
			.Where(m => m.UserId == userId)
			.Select(m => m.TenantId)
			.ToListAsync(ct);
		ids.AddRange(extra);
		return ids.Distinct().ToList();
	}

	public async Task<IReadOnlyList<TenantMembershipAdminRow>> ListAllAsync(CancellationToken ct = default)
	{
		// 仅显式成员关系（UserTenant 表）；隐式主租户关系数量庞大且不可管理，不在此列出。
		// 治理面读取：刻意忽略 User/Tenant 的租户作用域查询过滤器，以跨租户汇总全部显式成员关系。
		var raw = await _db.UserTenants.AsNoTracking()
			.Join(_db.Users.AsNoTracking().IgnoreQueryFilters(),
				m => m.UserId, u => u.Id,
				(m, u) => new { m, UserName = u.Username })
			.Join(_db.Tenants.AsNoTracking().IgnoreQueryFilters(),
				x => x.m.TenantId, t => t.Id,
				(x, t) => new
				{
					x.m.UserId,
					x.UserName,
					x.m.TenantId,
					TenantCode = t.TenantCode,
					TenantName = t.TenantName,
					TenantEnabled = t.Enabled,
					x.m.IsDefault,
					x.m.CreatedAtUtc,
					x.m.CreatedByUserId,
				})
			.OrderBy(r => r.UserId).ThenBy(r => r.TenantId)
			.ToListAsync(ct);

		return raw.Select(r => new TenantMembershipAdminRow(
			r.UserId, r.UserName, r.TenantId, r.TenantCode, r.TenantName,
			r.TenantEnabled, r.IsDefault, r.CreatedAtUtc, r.CreatedByUserId)).ToList();
	}
}
