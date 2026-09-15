using System.Collections.Generic;
using System.Threading;

namespace SuperBuilder_AI.Interfaces;

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
	/// <param name="text">待向量化文本。</param>
	/// <param name="textType">
	/// 文本类型。
	/// document: 用于 Metadata 入库；query: 用于用户问题检索。
	/// </param>
	/// <param name="ct">取消令牌，透传到底层 HTTP 调用，使挂起的请求可被 job 取消中断。</param>
	Task<float[]> GenerateAsync(
		string text,
		string textType = "document",
		CancellationToken ct = default);

	/// <summary>
	/// 批量将文本转换为 Embedding 向量。返回顺序与输入一致；任一文本为空将抛异常。
	/// 默认实现退化为逐条调用；具体实现（如 Qwen）应覆盖为真正的批量请求以减少网络往返。
	/// </summary>
	/// <param name="ct">取消令牌，透传到底层 HTTP 调用。</param>
	async Task<IReadOnlyList<float[]>> GenerateBatchAsync(
		IEnumerable<string> texts,
		string textType = "document",
		CancellationToken ct = default)
	{
		var list = texts.ToList();
		var results = new List<float[]>(list.Count);
		foreach (var t in list)
			results.Add(await GenerateAsync(t, textType, ct));
		return results;
	}

	/// <summary>当前模型维度。</summary>
	int Dimension { get; }

	/// <summary>当前模型名称。</summary>
	string ModelName { get; }
}
