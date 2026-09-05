using System;

namespace SuperBuilder_AI.Models.Identity;

/// <summary>
/// 用户—租户成员关系（M2-05 / SB-P1-16）。
/// <para>
/// 用户在主租户（<see cref="User.TenantId"/>，恒为隐式成员）之外可额外归属的租户。
/// 用户可切换到的生效租户 = { 主租户 } ∪ { 本表 TenantId }。
/// </para>
/// </summary>
public class UserTenant
{
	/// <summary>主键。</summary>
	public long Id { get; set; }

	/// <summary>用户 Id（归属主租户之外、本条记录的成员用户）。</summary>
	public long UserId { get; set; }

	/// <summary>可切换到的目标租户 Id。</summary>
	public long TenantId { get; set; }

	/// <summary>是否为该用户的默认切换目标（预留；当前切换默认回主租户）。</summary>
	public bool IsDefault { get; set; }

	/// <summary>创建时间（UTC）。</summary>
	public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

	/// <summary>操作者用户 Id（管理员代加成员时记录）。</summary>
	public long? CreatedByUserId { get; set; }
}

/// <summary>
/// 平台管理员视角的成员关系行（M2-05 / SB-P1-16）。
/// <para>列出全部显式成员关系（不含隐式主租户关系），含用户名与租户标识，供治理面管理页使用。</para>
/// </summary>
public sealed record TenantMembershipAdminRow(
	long UserId,
	string UserName,
	long TenantId,
	string? TenantCode,
	string? TenantName,
	bool TenantEnabled,
	bool IsDefault,
	DateTime CreatedAtUtc,
	long? CreatedByUserId);
