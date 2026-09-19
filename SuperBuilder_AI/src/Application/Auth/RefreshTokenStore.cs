using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models.Identity;

namespace SuperBuilder_AI.Services.Auth;

/// <summary>刷新令牌赎回结果状态。</summary>
public enum RefreshRedeemStatus
{
	/// <summary>成功轮换，返回新刷新令牌明文。</summary>
	Success,
	/// <summary>未知刷新令牌（未找到哈希）。</summary>
	Unknown,
	/// <summary>已过期。</summary>
	Expired,
	/// <summary>已撤销（被其他请求轮换/吊销）。</summary>
	Revoked,
	/// <summary>复用检测：已撤销的令牌再次出现，已按 FamilyId 吊销整条链。</summary>
	ReuseDetected,
	/// <summary>安全戳不匹配（口令/角色变更已轮换 SecurityStamp）。</summary>
	StampMismatch,
	/// <summary>并发刷新或已被撤销（原子撤销受影响行数为 0，保证最多一次成功）。</summary>
	ConcurrencyFailure,
}

/// <summary>刷新令牌赎回结果（Phase 2）。</summary>
public sealed record RefreshRedeemOutcome(
	RefreshRedeemStatus Status,
	string? NewRefreshToken = null,
	string? FamilyId = null,
	long UserId = 0,
	long TenantId = 0,
	string? Username = null,
	string? SecurityStamp = null);

/// <summary>
/// 刷新令牌存储（Phase 2）。API 侧刷新轮换与静默续期的权威数据来源。
///
/// <para>
/// 设计要点：
/// <list type="bullet">
/// <item>仅持久化令牌哈希；明文只在返回时短暂存在。</item>
/// <item>每次刷新在同一事务语义下撤销旧令牌并签发新令牌（同 FamilyId），旧记录以 RevokedAtUtc 或 ReplacedByTokenHash 标记。</item>
/// <item>并发刷新：原子更新（<c>ExecuteUpdate</c> 受影响行数 = 1）保证最多一次成功，第二个请求受影响行数 = 0 即失败。</item>
/// <item>复用检测：已撤销令牌再次出现 → 按 FamilyId 吊销整条链（判定泄露）。</item>
/// </list>
/// </para>
/// </summary>
/// <summary>刷新令牌存储端口（Phase 2）。</summary>
public interface IRefreshTokenStore
{
	/// <summary>创建刷新令牌记录（生成 FamilyId 与明文），返回明文与 FamilyId。</summary>
	Task<(string RefreshTokenPlain, string FamilyId)> CreateAsync(
		long userId, long tenantId, string securityStamp, string? clientIp, string? userAgent,
		DateTimeOffset expiresAtUtc, CancellationToken ct = default);

	/// <summary>赎回刷新令牌：校验并原子轮换，返回新刷新令牌明文与用户标识。</summary>
	Task<RefreshRedeemOutcome> RedeemAsync(
		string refreshToken, string? clientIp = null, string? userAgent = null, CancellationToken ct = default);

	/// <summary>按 FamilyId 吊销整条令牌链（复用检测 / 账号禁用）。</summary>
	Task RevokeFamilyAsync(string familyId, CancellationToken ct = default);
}

public sealed class RefreshTokenStore : IRefreshTokenStore
{
	private readonly SuperBIContext _db;
	private readonly double _refreshLifetimeDays;

	public RefreshTokenStore(SuperBIContext db, IConfiguration configuration)
	{
		_db = db;
		_refreshLifetimeDays = configuration.GetValue("Auth:RefreshTokenLifetimeDays", 14.0);
	}

	/// <inheritdoc />
	public async Task<(string RefreshTokenPlain, string FamilyId)> CreateAsync(
		long userId, long tenantId, string securityStamp, string? clientIp, string? userAgent,
		DateTimeOffset expiresAtUtc, CancellationToken ct = default)
	{
		var plain = GenerateOpaqueToken();
		var familyId = Guid.NewGuid().ToString("N");
		_db.RefreshTokens.Add(new RefreshToken
		{
			TokenHash = Hash(plain),
			UserId = userId,
			TenantId = tenantId,
			SecurityStamp = securityStamp,
			FamilyId = familyId,
			ExpiresAtUtc = expiresAtUtc.UtcDateTime,
			ClientIp = clientIp,
			UserAgent = userAgent,
		});
		await _db.SaveChangesAsync(ct);
		return (plain, familyId);
	}

	/// <inheritdoc />
	public async Task<RefreshRedeemOutcome> RedeemAsync(
		string refreshToken, string? clientIp = null, string? userAgent = null, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(refreshToken))
			return new RefreshRedeemOutcome(RefreshRedeemStatus.Unknown);

		var hash = Hash(refreshToken);
		var record = await _db.RefreshTokens.AsNoTracking()
			.FirstOrDefaultAsync(r => r.TokenHash == hash, ct);
		if (record is null)
			return new RefreshRedeemOutcome(RefreshRedeemStatus.Unknown);

		if (record.RevokedAtUtc is not null)
		{
			// 复用检测：已撤销（或已轮换）的令牌再次出现 —— 吊销整条 family 链并记录安全事件。
			await RevokeFamilyAsync(record.FamilyId, ct);
			return new RefreshRedeemOutcome(RefreshRedeemStatus.ReuseDetected, FamilyId: record.FamilyId);
		}

		if (record.ExpiresAtUtc < DateTime.UtcNow)
			return new RefreshRedeemOutcome(RefreshRedeemStatus.Expired, FamilyId: record.FamilyId);

		var user = await _db.Users.AsNoTracking()
			.FirstOrDefaultAsync(u => u.Id == record.UserId && u.TenantId == record.TenantId, ct);
		if (user is null)
			return new RefreshRedeemOutcome(RefreshRedeemStatus.Unknown);

		// P0-04B：安全戳比对。口令/角色变更会轮换 SecurityStamp；不一致即令牌已吊销。
		if (!string.Equals(record.SecurityStamp, user.SecurityStamp, StringComparison.Ordinal))
			return new RefreshRedeemOutcome(RefreshRedeemStatus.StampMismatch, FamilyId: record.FamilyId);

		// 原子撤销旧记录：仅当仍有效（RevokedAtUtc == null）时置位成功（受影响行数 = 1）。
		// 并发刷新或已被其他请求撤销时受影响行数 = 0，保证最多一次成功。
		var revoked = await _db.RefreshTokens
			.Where(r => r.TokenHash == hash && r.RevokedAtUtc == null)
			.ExecuteUpdateAsync(setters => setters.SetProperty(r => r.RevokedAtUtc, DateTime.UtcNow), ct);
		if (revoked == 0)
			return new RefreshRedeemOutcome(RefreshRedeemStatus.ConcurrencyFailure, FamilyId: record.FamilyId);

		// 签发新刷新令牌（同 FamilyId），明文仅此一次返回。
		var newPlain = GenerateOpaqueToken();
		var newHash = Hash(newPlain);
		var newExpires = DateTime.UtcNow.AddDays(_refreshLifetimeDays);
		_db.RefreshTokens.Add(new RefreshToken
		{
			TokenHash = newHash,
			UserId = record.UserId,
			TenantId = record.TenantId,
			SecurityStamp = user.SecurityStamp,
			FamilyId = record.FamilyId,
			ExpiresAtUtc = newExpires,
			ClientIp = clientIp,
			UserAgent = userAgent,
		});

		// 轮换链：旧记录的 ReplacedByTokenHash 指向新哈希（审计追溯）。
		await _db.RefreshTokens
			.Where(r => r.TokenHash == hash)
			.ExecuteUpdateAsync(setters => setters.SetProperty(r => r.ReplacedByTokenHash, newHash), ct);

		await _db.SaveChangesAsync(ct);

		return new RefreshRedeemOutcome(
			RefreshRedeemStatus.Success,
			NewRefreshToken: newPlain,
			FamilyId: record.FamilyId,
			UserId: record.UserId,
			TenantId: record.TenantId,
			Username: user.Username,
			SecurityStamp: user.SecurityStamp);
	}

	/// <inheritdoc />
	public async Task RevokeFamilyAsync(string familyId, CancellationToken ct = default)
	{
		await _db.RefreshTokens
			.Where(r => r.FamilyId == familyId && r.RevokedAtUtc == null)
			.ExecuteUpdateAsync(setters => setters.SetProperty(r => r.RevokedAtUtc, DateTime.UtcNow), ct);
	}

	private static string Hash(string refreshToken)
	{
		using var sha = SHA256.Create();
		var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(refreshToken));
		return Convert.ToHexString(bytes).ToLowerInvariant();
	}

	private static string GenerateOpaqueToken()
	{
		var buf = new byte[32];
		using var rng = RandomNumberGenerator.Create();
		rng.GetBytes(buf);
		return Convert.ToHexString(buf);
	}
}
