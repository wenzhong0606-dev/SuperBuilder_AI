using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Models.Identity;

namespace SuperBuilder_AI.Interfaces.Identity;

/// <summary>用户在某个租户的成员关系视图（M2-05 / SB-P1-16）。</summary>
public sealed record TenantMembershipView(
	long TenantId,
	string? TenantCode,
	string? TenantName,
	bool IsDefault,
	bool Enabled);

/// <summary>
/// 多租户成员关系服务（M2-05 / SB-P1-16，确定性，不调 LLM）。
/// <para>
/// 用户恒为主租户（<see cref="Models.Identity.User.TenantId"/>）的隐式成员；本服务管理其额外的可切换租户。
/// 可切换到的生效租户 = { 主租户 } ∪ { UserTenant 表中该用户的 TenantId }。
/// </para>
/// </summary>
public interface ITenantMembershipService
{
	/// <summary>为用户增加可切换到的目标租户（幂等；主租户无需显式记录）。</summary>
	Task AddMemberAsync(long userId, long tenantId, long? createdBy, CancellationToken ct = default);

	/// <summary>移除用户的可切换租户；主租户不可移除，否则抛 <see cref="System.InvalidOperationException"/>。</summary>
	Task RemoveMemberAsync(long userId, long tenantId, CancellationToken ct = default);

	/// <summary>设置该用户的默认切换目标（预留；主租户始终为默认）。</summary>
	Task SetDefaultAsync(long userId, long tenantId, CancellationToken ct = default);

	/// <summary>判断用户是否可切换到指定租户（主租户或已登记的成员租户）。</summary>
	Task<bool> IsMemberAsync(long userId, long tenantId, CancellationToken ct = default);

	/// <summary>列出用户可切换的全部租户（含主租户，IsDefault=true）。</summary>
	Task<IReadOnlyList<TenantMembershipView>> GetMembershipsAsync(long userId, CancellationToken ct = default);

	/// <summary>返回用户可切换到的租户 Id 集合（主租户 + 成员租户）。</summary>
	Task<IReadOnlyList<long>> GetSwitchableTenantIdsAsync(long userId, CancellationToken ct = default);

	/// <summary>列出全部显式成员关系（不含隐式主租户关系），供平台治理面管理页使用。</summary>
	Task<IReadOnlyList<TenantMembershipAdminRow>> ListAllAsync(CancellationToken ct = default);
}
