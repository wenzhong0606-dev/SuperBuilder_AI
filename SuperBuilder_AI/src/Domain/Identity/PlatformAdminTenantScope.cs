using System;
using System.ComponentModel.DataAnnotations;

namespace SuperBuilder_AI.Models.Identity;

/// <summary>
/// 平台管理员租户范围绑定（M2-02）。
/// <para>
/// 约定（见 Master_Development_Plan DEC-03）：某平台管理员在表中<b>无记录</b> → 管理<b>全部租户</b>（默认）；
/// <b>有记录</b> → 仅管理所列租户。范围为累加式白名单，不做黑名单排除。
/// </para>
/// 该表与业务/BI 数据完全隔离，仅用于治理面「谁能管理哪些租户」的强制校验，不影响 Golden 行为契约。
/// </summary>
public class PlatformAdminTenantScope
{
    public long Id { get; set; }

    /// <summary>被授权的平台管理员用户 Id（TenantId=平台租户）。</summary>
    public long AdminUserId { get; set; }

    /// <summary>被授权管理的目标租户 Id。</summary>
    public long TenantId { get; set; }

    /// <summary>授权时间（UTC）。由写入路径以 DateTime.UtcNow 填充。</summary>
    public DateTime GrantedAt { get; set; }

    /// <summary>授权操作者标识（平台治理管理员用户名 / "system"）。</summary>
    [MaxLength(128)]
    public string? GrantedBy { get; set; }
}
