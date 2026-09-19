using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SuperBuilder_AI.Services.Auth;

/// <summary>
/// 无状态令牌主体（令牌校验成功后解析出的声明）。
/// </summary>
public sealed record TokenPrincipal(
	long UserId,
	long TenantId,
	string Username,
	IReadOnlyList<string> Permissions)
{
	/// <summary>安全戳（P0-04B 令牌吊销）。令牌签发时的用户安全戳；为空表示遗留令牌（无吊销校验）。</summary>
	public string? SecurityStamp { get; init; }

	/// <summary>
	/// 主租户（home tenant，M2-05）。用户归属的主租户（= User.TenantId）。
	/// 切换后 <see cref="TenantId"/> 为生效租户、<see cref="HomeTenantId"/> 仍为归属主租户，
	/// 供 AuthMiddleware 以主租户定位用户行（安全戳/启用校验）与审计。
	/// 未携带该声明（旧令牌/未切换）时退化为与 <see cref="TenantId"/> 一致。
	/// </summary>
	public long HomeTenantId { get; init; }
}

/// <summary>
/// 令牌服务端口。
/// </summary>
public interface ITokenService
{
	/// <summary>
	/// 访问令牌有效期。默认 60 分钟；可用配置 <c>Auth:AccessTokenLifetimeMinutes</c> 覆盖。
	/// 既驱动令牌载荷 <c>exp</c>，也为各登录/切换入口返回的 <c>ExpiresInSeconds</c> 提供单一来源，
	/// 避免 DTO 告知客户端的过期时间与令牌真实过期脱节。
	/// </summary>
	TimeSpan Lifetime => TimeSpan.FromMinutes(60);

	/// <summary>为指定主体签发一个 HMAC 签名令牌。</summary>
	string Issue(long tenantId, long userId, string username, IEnumerable<string> permissions, string? securityStamp = null, long? homeTenantId = null);

	/// <summary>校验令牌；无效或过期返回 null。</summary>
	TokenPrincipal? Validate(string? token);
}

/// <summary>
/// 轻量无状态令牌服务（P11.0 安全轨道）。
///
/// <para>
/// 采用 HMAC-SHA256 手动实现 JWT 风格令牌（<c>header.payload.signature</c>），
/// 不引入任何外部 NuGet 包，满足零依赖约束。令牌载荷包含：
/// <list type="bullet">
/// <item><c>sub</c>：用户 Id</item>
/// <item><c>tid</c>：租户 Id</item>
/// <item><c>name</c>：用户名</item>
/// <item><c>perms</c>：权限码集合（便于下游声明式鉴权，免二次查库）</item>
/// <item><c>iat</c> / <c>exp</c>：签发与过期时间戳（默认 60 分钟）</item>
/// </list>
/// </para>
///
/// <para>签名密钥来自配置 <c>Auth:SigningKey</c>；调用方必须先完成环境级校验，本服务不提供默认值。</para>
/// </summary>
public sealed class TokenService : ITokenService
{
	private readonly byte[] _key;

	/// <inheritdoc />
	public TimeSpan Lifetime { get; }

	public TokenService(string? signingKey, TimeSpan? lifetime = null)
	{
		if (string.IsNullOrWhiteSpace(signingKey))
			throw new ArgumentException("A non-empty signing key is required.", nameof(signingKey));

		var key = signingKey.Trim();
		_key = Encoding.UTF8.GetBytes(key);
		Lifetime = lifetime ?? TimeSpan.FromMinutes(60);
	}

	public string Issue(long tenantId, long userId, string username, IEnumerable<string> permissions, string? securityStamp = null, long? homeTenantId = null)
	{
		var now = DateTimeOffset.UtcNow;
		var payload = new TokenPayload
		{
			Sub = userId,
			Tid = tenantId,
			Name = username,
			Perms = permissions as List<string> ?? new List<string>(permissions),
			Sec = securityStamp,
			// M2-05：切换后 tid=生效租户；htid=归属主租户（缺省与 tid 一致，旧调用方无感）。
			Htid = homeTenantId ?? tenantId,
			Iat = now.ToUnixTimeSeconds(),
			Exp = now.Add(Lifetime).ToUnixTimeSeconds(),
		};

		var headerB64 = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "HS256", typ = "JWT" }));
		var payloadB64 = Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload));
		var signingInput = $"{headerB64}.{payloadB64}";
		var sig = Base64Url(Hmac(signingInput));
		return $"{signingInput}.{sig}";
	}

	public TokenPrincipal? Validate(string? token)
	{
		if (string.IsNullOrWhiteSpace(token)) return null;

		var parts = token.Split('.');
		if (parts.Length != 3) return null;

		var signingInput = $"{parts[0]}.{parts[1]}";
		var expectedSig = Base64Url(Hmac(signingInput));
		if (!CryptographicOperations.FixedTimeEquals(
				Encoding.ASCII.GetBytes(expectedSig),
				Encoding.ASCII.GetBytes(parts[2])))
			return null;

		TokenPayload? payload;
		try
		{
			payload = JsonSerializer.Deserialize<TokenPayload>(FromBase64Url(parts[1]));
		}
		catch
		{
			return null;
		}

		if (payload is null) return null;
		if (payload.Exp <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return null;

		return new TokenPrincipal(
			payload.Sub,
			payload.Tid,
			payload.Name ?? string.Empty,
			payload.Perms ?? new List<string>())
		{
			SecurityStamp = payload.Sec,
			HomeTenantId = payload.Htid ?? payload.Tid,
		};
	}

	private byte[] Hmac(string signingInput)
	{
		using var hmac = new HMACSHA256(_key);
		return hmac.ComputeHash(Encoding.ASCII.GetBytes(signingInput));
	}

	private static string Base64Url(byte[] data) => Base64Url(Convert.ToBase64String(data));

	private static string Base64Url(string base64)
		=> base64.Replace('+', '-').Replace('/', '_').TrimEnd('=');

	private static byte[] FromBase64Url(string base64Url)
	{
		var padded = base64Url.Replace('-', '+').Replace('_', '/');
		var mod = padded.Length % 4;
		if (mod != 0) padded += new string('=', 4 - mod);
		return Convert.FromBase64String(padded);
	}

	private sealed class TokenPayload
	{
		public long Sub { get; set; }
		public long Tid { get; set; }
		public string? Name { get; set; }
		public List<string>? Perms { get; set; }
		public string? Sec { get; set; }
		// M2-05：归属主租户（home tenant）。缺失时退化为与 Tid 一致。
		public long? Htid { get; set; }
		public long Iat { get; set; }
		public long Exp { get; set; }
	}
}
