namespace SuperBulider_AI.Interfaces;

/// <summary>
/// 文本向量化服务。
///
/// 负责将文本转换为向量。
///
/// 使用场景:
///
/// Metadata:
///     SearchText
///         ↓
///     Embedding
///         ↓
///     Qdrant
///
/// 用户问题:
///     Question
///         ↓
///     Query Embedding
///         ↓
///     Qdrant Search
/// </summary>
public interface IEmbeddingService
{
	/// <summary>
	/// 将文本转换为 Embedding 向量。
	/// </summary>
	/// <param name="text">
	/// 待向量化文本。
	/// </param>
	/// <param name="textType">
	/// 文本类型。
	///
	/// document:
	///     用于 Metadata 入库。
	///
	/// query:
	///     用于用户问题检索。
	/// </param>
	Task<float[]> GenerateAsync(
		string text,
		string textType = "document");

	/// <summary>
	/// 当前模型维度。
	/// </summary>
	int Dimension { get; }

	/// <summary>
	/// 当前模型名称。
	/// </summary>
	string ModelName { get; }
}