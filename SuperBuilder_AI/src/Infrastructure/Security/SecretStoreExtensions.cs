using System;

namespace SuperBuilder_AI.Infrastructure.Security;

/// <summary>
/// 机密存储扩展：读取端统一兼容「已加密(v1: 信封) / 遗留明文」两种存储形态，
/// 使存量未加密数据与单元测试明文行也能被安全读取，避免解密异常拖垮服务。
/// </summary>
public static class SecretStoreExtensions
{
	/// <summary>
	/// 解析存储值的明文：以 <c>v1:</c> 开头视为密文信封，调用 <see cref="ISecretStore.Unprotect"/>；
	/// 否则按遗留明文原样返回（兼容存量数据与单元测试）。
	/// </summary>
	public static string? ResolvePlaintext(this ISecretStore store, string? stored)
	{
		if (stored is null) return null;
		return stored.StartsWith("v1:", StringComparison.Ordinal)
			? store.Unprotect(stored)
			: stored;
	}
}
