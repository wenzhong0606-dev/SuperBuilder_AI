using System;
using SuperBuilder_AI.Models;

namespace SuperBuilder_AI.Models.Identity;

/// <summary>
/// 刷新令牌持久化记录（Phase 2）。
///
/// <para>
/// API 仅保存令牌的密码学哈希，明文只在签发响应与 Web 服务端会话中短暂存在，日志与数据库均不保存明文。
/// 轮换链：每次刷新签发新记录（沿用 <see cref="FamilyId"/>），旧记录以 <see cref="RevokedAtUtc"/> 或
/// <see cref="ReplacedByTokenHash"/> 标记失效。并发刷新与复用检测依赖唯一 <see cref="TokenHash"/> 约束 +
/// 乐观并发（<see cref="BaseEntity.RowVersion"/>，继承基类）。
/// </para>
/// </summary>
public sealed class RefreshToken : BaseEntity
{
	/// <summary>刷新令牌的 SHA-256 哈希（小写 hex）。全局唯一，防止明文重复存储与并发插入撞键。</summary>
	public string TokenHash { get; set; } = string.Empty;

	/// <summary>令牌归属用户。</summary>
	public long UserId { get; set; }

	/// <summary>用户所属主租户（冗余存储，便于按租户隔离查询；不建外键，User 已含 TenantId）。</summary>
	public long TenantId { get; set; }

	/// <summary>签发时的用户安全戳；赎回时与用户库当前安全戳比对，不一致即视为已吊销（口令/角色变更）。</summary>
	public string SecurityStamp { get; set; } = string.Empty;

	/// <summary>令牌族标识（登录时新建，刷新时沿用）。检测到复用即按 FamilyId 吊销整条链。</summary>
	public string FamilyId { get; set; } = string.Empty;

	/// <summary>过期时间（UTC）。</summary>
	public DateTime ExpiresAtUtc { get; set; }

	/// <summary>撤销时间（UTC）；非 null 表示已失效（轮换/吊销/复用检测）。</summary>
	public DateTime? RevokedAtUtc { get; set; }

	/// <summary>被替换后新令牌的哈希（轮换链；与 RevokedAtUtc 二选一标记旧令牌失效，便于审计追溯）。</summary>
	public string? ReplacedByTokenHash { get; set; }

	/// <summary>客户端 IP（审计）。</summary>
	public string? ClientIp { get; set; }

	/// <summary>客户端 User-Agent（审计）。</summary>
	public string? UserAgent { get; set; }
}
