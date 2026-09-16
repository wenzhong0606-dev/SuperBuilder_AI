using System;
using System.Security.Cryptography;
using System.Text;

namespace SuperBuilder_AI.Services.Auth;

/// <summary>
/// 口令哈希端口（P0-04A 正式口令认证）。
/// 实现须满足：相同明文每次生成不同密文（随机盐）、恒定时间验证、不存储明文。
/// </summary>
public interface IPasswordHasher
{
	/// <summary>对明文口令生成可存储的哈希（含算法标识、迭代次数与随机盐）。</summary>
	string Hash(string plainPassword);

	/// <summary>验证明文与已存储哈希是否匹配；哈希格式非法或为空返回 false。</summary>
	bool Verify(string? plainPassword, string? storedHash);
}

/// <summary>
/// PBKDF2-HMACSHA256 口令哈希实现（P0-04A）。
///
/// <para>
/// 零外部依赖，仅用 BCL <see cref="Rfc2898DeriveBytes"/>。存储格式：
/// <c>pbkdf2:&lt;iterations&gt;:&lt;saltBase64&gt;:&lt;hashBase64&gt;</c>。
/// 每次 <see cref="Hash"/> 使用 16 字节随机盐，故相同明文产生不同密文。
/// 验证采用 <see cref="CryptographicOperations.FixedTimeEquals"/> 抗时序攻击。
/// </para>
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
	private const string Prefix = "pbkdf2";
	private const int SaltBytes = 16;
	private const int KeyBytes = 32;
	private const int Iterations = 100_000;

	public string Hash(string plainPassword)
	{
		if (plainPassword is null) throw new ArgumentNullException(nameof(plainPassword));

		var salt = RandomNumberGenerator.GetBytes(SaltBytes);
		var key = Derive(salt, plainPassword);

		return $"{Prefix}:{Iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(key)}";
	}

	public bool Verify(string? plainPassword, string? storedHash)
	{
		if (string.IsNullOrWhiteSpace(plainPassword) || string.IsNullOrWhiteSpace(storedHash))
			return false;

		var parts = storedHash.Split(':');
		if (parts.Length != 4 || !string.Equals(parts[0], Prefix, StringComparison.Ordinal))
			return false;

		if (!int.TryParse(parts[1], out var iterations) || iterations <= 0)
			return false;

		byte[] salt;
		byte[] expected;
		try
		{
			salt = Convert.FromBase64String(parts[2]);
			expected = Convert.FromBase64String(parts[3]);
		}
		catch (FormatException)
		{
			return false;
		}

		var actual = Derive(salt, plainPassword!, iterations);
		return CryptographicOperations.FixedTimeEquals(actual, expected);
	}

	private static byte[] Derive(byte[] salt, string plainPassword, int iterations = Iterations)
	{
		return Rfc2898DeriveBytes.Pbkdf2(
			Encoding.UTF8.GetBytes(plainPassword),
			salt,
			iterations,
			HashAlgorithmName.SHA256,
			KeyBytes);
	}
}
