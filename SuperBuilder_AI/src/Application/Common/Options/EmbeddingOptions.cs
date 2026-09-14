namespace SuperBuilder_AI.Application.Common.Options;

/// <summary>
/// Qwen Embedding 配置。
///
/// 用于:
///
/// IEmbeddingService
///        ↓
/// QwenEmbeddingService
///        ↓
/// qwen3.7-text-embedding
///
/// </summary>
public class EmbeddingOptions
{
	/// <summary>
	/// Qwen Embedding API Key。
	///
	/// 不建议直接写入 appsettings.json。
	/// 推荐使用 User Secrets 或环境变量。
	/// </summary>
	public string ApiKey { get; set; } = string.Empty;

	/// <summary>
	/// Embedding API Endpoint。
	///
	/// 例如:
	///
	/// https://xxx/compatible-mode/v1/embeddings
	/// </summary>
	public string Endpoint { get; set; } = string.Empty;

	/// <summary>
	/// Embedding 模型。
	/// </summary>
	public string Model { get; set; }
		= "qwen3.7-text-embedding";

	/// <summary>
	/// Embedding 向量维度。
	///
	/// 当前项目统一使用 1024。
	/// </summary>
	public int Dimensions { get; set; } = 1024;

	/// <summary>
	/// HTTP 请求超时时间。
	/// </summary>
	public int TimeoutSeconds { get; set; } = 120;

	/// <summary>
	/// 单次批量 Embedding 请求的最大文本条数。
	///
	/// 单批过大可能触发 Qwen 单批上限或限流（429）。
	/// 分片后片内仍是批量请求（保留吞吐），片间避免单次过载。
	/// </summary>
	public int BatchSize { get; set; } = 16;
}