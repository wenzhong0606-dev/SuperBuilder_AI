namespace SuperBuilder_AI.Application.Common.Options;

/// <summary>
/// Qwen LLM 配置（绑定 <c>appsettings</c> 的 <c>Qwen</c> 节）。
/// 仅取版本相关字段；凭据不进入本选项对象。
/// </summary>
public sealed class QwenOptions
{
	/// <summary>配置节名。</summary>
	public const string SectionName = "Qwen";

	/// <summary>对话模型标识（如 <c>qwen-plus</c>）；用于缓存键的 ModelVersion 维度。</summary>
	public string Model { get; set; } = "qwen-plus";

	/// <summary>向量嵌入模型标识（如 <c>qwen3.7-text-embedding</c>）。</summary>
	public string EmbeddingModel { get; set; } = "qwen3.7-text-embedding";
}
