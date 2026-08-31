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
	IReadOnlyList<string> Permissions);

/// <summary>
/// 令牌服务端口。
/// </summary>
public interface ITokenService
{
	/// <summary>为指定主体签发一个 HMAC 签名令牌。</summary>
	string Issue(long tenantId, long userId, string username, IEnumerable<string> permissions);

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
/// <para>签名密钥来自配置 <c>Auth:SigningKey</c>；缺失时使用开发期默认值（生产必须显式配置）。</para>
/// </summary>
public sealed class TokenService : ITokenService
{
	private readonly byte[] _key;
	private readonly TimeSpan _lifetime = TimeSpan.FromMinutes(60);

	public TokenService(string? signingKey)
	{
		var key = signingKey ?? "dev-insecure-signing-key-P11-change-in-prod";
		_key = Encoding.UTF8.GetBytes(key);
	}

	public string Issue(long tenantId, long userId, string username, IEnumerable<string> permissions)
	{
		var now = DateTimeOffset.UtcNow;
		var payload = new TokenPayload
		{
			Sub = userId,
			Tid = tenantId,
			Name = username,
			Perms = permissions as List<string> ?? new List<string>(permissions),
			Iat = now.ToUnixTimeSeconds(),
			Exp = now.Add(_lifetime).ToUnixTimeSeconds(),
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
			payload.Perms ?? new List<string>());
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
		public long Iat { get; set; }
		public long Exp { get; set; }
	}
}
