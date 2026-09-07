namespace SuperBuilder_AI.Models.ModelAccount;

/// <summary>
/// 租户级模型账号绑定（BYO 模型 API Key，M7-07 / 参考 M10-02）。
///
/// <para>
/// <strong>密钥加密存储（红线）</strong>：API Key 仅以密文 <see cref="EncryptedKey"/>（AES-GCM 信封）持久化，
/// 绝不明文落库；展示层使用掩码 <see cref="MaskedKey"/>（如 <c>sk-***1234</c>）。明文仅在服务端经
/// <see cref="ISecretStore"/> 解密后注入 LLM 客户端，任何 API 响应都不下发明文。
/// </para>
///
/// <para>
/// 租户作用域：<see cref="TenantId"/> 恒 &gt; 0，不存在全局行；同一租户内 (Provider, ModelId) 唯一（一个模型仅一个绑定）。
/// </para>
/// </summary>
public class ModelAccount : BaseEntity
{
	/// <summary>所属租户 Id（&gt; 0）。</summary>
	public long TenantId { get; set; }

	/// <summary>供应商标识（如 <c>Qwen</c> / <c>OpenAI</c> / <c>Azure</c> / <c>DeepSeek</c>）。</summary>
	public string Provider { get; set; } = string.Empty;

	/// <summary>模型标识（如 <c>qwen-plus</c> / <c>gpt-4o</c> / <c>deepseek-chat</c>），与模型目录对齐。</summary>
	public string ModelId { get; set; } = string.Empty;

	/// <summary>展示名（冗余，便于列表展示，默认取 Provider/ModelId）。</summary>
	public string DisplayName { get; set; } = string.Empty;

	/// <summary>AES-GCM 密文信封（<c>v1:base64</c>）；禁止日志记录、禁止明文下发。</summary>
	public string EncryptedKey { get; set; } = string.Empty;

	/// <summary>展示掩码（如 <c>sk-***1234</c>），供前端展示，不含任何明文片段之外的可还原信息。</summary>
	public string MaskedKey { get; set; } = string.Empty;

	/// <summary>备注（可选）。</summary>
	public string? Note { get; set; }

	/// <summary>是否为租户默认模型（每租户至多一个）。</summary>
	public bool IsDefault { get; set; }
}
