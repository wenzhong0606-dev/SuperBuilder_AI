namespace SuperBuilder_AI.Configuration;

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
}