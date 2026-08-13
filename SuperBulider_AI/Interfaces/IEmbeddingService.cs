namespace SuperBulider_AI.Interfaces;

/// <summary>
/// 文本向量化服务
/// </summary>
public interface IEmbeddingService
{


	/// <summary>
	/// 文本生成Embedding
	/// </summary>
	Task<float[]> GenerateAsync(
		string text);



	/// <summary>
	/// 当前模型维度
	/// </summary>
	int Dimension { get; }


	/// <summary>
	/// 模型名称
	/// </summary>
	string ModelName { get; }

}