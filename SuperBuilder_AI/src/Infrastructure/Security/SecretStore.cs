using System.Security.Cryptography;
using System.Text;

namespace SuperBuilder_AI.Infrastructure.Security;

/// <summary>
/// 机密存储契约：将明文凭证加密为可持久化/传输的密文信封，并支持服务端解密。
/// 明文仅在服务端经 <see cref="Unprotect"/> 取出用于注入 LLM 客户端，绝不出现在任何对外响应或日志中。
/// </summary>
public interface ISecretStore
{
	/// <summary>加密明文，返回密文信封（含版本与随机数，base64）。</summary>
	string Protect(string plaintext);

	/// <summary>解密密文信封，返回明文（仅服务端调用）。</summary>
	string Unprotect(string ciphertext);
}

/// <summary>
/// 基于 AES-256-GCM 的机密存储实现（AEAD 信封加密）。
///
/// <para>
/// 密文信封格式：<c>v1:&lt;base64(nonce[12] | ciphertext | tag[16])&gt;</c>。
/// 主密钥（32 字节 AES-256）须来自环境变量 / KeyVault（<c>SecretStore:MasterKey</c> 或 <c>SecretStore__MasterKey</c>），
/// 禁止落库明文。主密钥缺失时 <see cref="AesGcmSecretStore"/> 构造即抛异常（fail-fast）。
/// </para>
/// </summary>
public sealed class AesGcmSecretStore : ISecretStore
{
	private const string VersionPrefix = "v1:";
	private const int NonceSize = 12;
	private const int TagSize = 16;
	private readonly byte[] _key;

	public AesGcmSecretStore(byte[] masterKey)
	{
		if (masterKey is null || masterKey.Length != 32)
			throw new ArgumentException("主密钥必须为 32 字节（AES-256）。", nameof(masterKey));
		_key = (byte[])masterKey.Clone();
	}

	public string Protect(string plaintext)
	{
		if (plaintext is null) throw new ArgumentNullException(nameof(plaintext));

		var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
		var nonce = RandomNumberGenerator.GetBytes(NonceSize);
		var ciphertext = new byte[plaintextBytes.Length];
		var tag = new byte[TagSize];

		using var aes = new AesGcm(_key, TagSize);
		aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

		var envelope = new byte[NonceSize + ciphertext.Length + TagSize];
		Buffer.BlockCopy(nonce, 0, envelope, 0, NonceSize);
		Buffer.BlockCopy(ciphertext, 0, envelope, NonceSize, ciphertext.Length);
		Buffer.BlockCopy(tag, 0, envelope, NonceSize + ciphertext.Length, TagSize);

		return VersionPrefix + Convert.ToBase64String(envelope);
	}

	public string Unprotect(string ciphertext)
	{
		if (ciphertext is null) throw new ArgumentNullException(nameof(ciphertext));
		if (!ciphertext.StartsWith(VersionPrefix, StringComparison.Ordinal))
			throw new ArgumentException("不支持的密文版本。", nameof(ciphertext));

		var envelope = Convert.FromBase64String(ciphertext.Substring(VersionPrefix.Length));
		if (envelope.Length < NonceSize + TagSize)
			throw new ArgumentException("密文长度不足。", nameof(ciphertext));

		var nonce = envelope.AsSpan(0, NonceSize).ToArray();
		var tag = envelope.AsSpan(envelope.Length - TagSize, TagSize).ToArray();
		var ciphertextBytes = envelope.AsSpan(NonceSize, envelope.Length - NonceSize - TagSize).ToArray();
		var plaintextBytes = new byte[ciphertextBytes.Length];

		using var aes = new AesGcm(_key, TagSize);
		aes.Decrypt(nonce, ciphertextBytes, tag, plaintextBytes);

		return Encoding.UTF8.GetString(plaintextBytes);
	}
}
