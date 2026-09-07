namespace SuperBuilder_AI.Application.ModelAccounts;

/// <summary>
/// 模型目录项（能力驱动，供前端绑定下拉与默认模型选择）。
/// </summary>
public sealed record ModelCatalogItem(
	/// <summary>供应商标识（与绑定 Provider 对齐）。</summary>
	string Provider,
	/// <summary>模型标识（与绑定 ModelId 对齐）。</summary>
	string ModelId,
	/// <summary>展示名。</summary>
	string DisplayName,
	/// <summary>能力标签（如 <c>chat</c> / <c>embedding</c> / <c>chat,embedding</c>）。</summary>
	string? Capabilities,
	/// <summary>上下文窗口（token 数，可选）。</summary>
	int? ContextWindow,
	/// <summary>区域（可选）。</summary>
	string? Region,
	/// <summary>状态（<c>active</c> / <c>preview</c> / <c>deprecated</c>）。</summary>
	string Status);

/// <summary>
/// 模型目录（M7-07 静态种子配置；DB 实体化推迟至 M10-02 BYO 里程碑）。
/// 当前为内置参考目录，覆盖通义千问 / OpenAI / Azure OpenAI / DeepSeek 主力模型。
/// <c>GET /api/model-catalog</c> 以此驱动前端下拉，确保"目录驱动"而非硬编码。
/// </summary>
public static class ModelCatalogProvider
{
	public static IReadOnlyList<ModelCatalogItem> GetCatalog() => new List<ModelCatalogItem>
	{
		new("Qwen", "qwen-plus", "Qwen-Plus", "chat,embedding", 131072, "cn", "active"),
		new("Qwen", "qwen-max", "Qwen-Max", "chat,embedding", 32768, "cn", "active"),
		new("OpenAI", "gpt-4o", "OpenAI GPT-4o", "chat,embedding", 128000, "global", "active"),
		new("Azure", "azure-gpt-4o", "Azure OpenAI", "chat,embedding", 128000, "global", "active"),
		new("DeepSeek", "deepseek-chat", "DeepSeek", "chat", 64000, "cn", "active"),
	};
}
