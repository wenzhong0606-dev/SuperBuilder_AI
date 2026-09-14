using System.Collections.Generic;
using SuperBuilder_AI.Models.AI;
using System.Threading;

namespace SuperBuilder_AI.Interfaces;

/// <summary>
/// Qdrant 向量存储接口。
/// </summary>
public interface IQdrantService
{
	/// <summary>
	/// 创建 Collection。
	///
	/// 如果已经存在，则不重复创建。
	/// </summary>
	Task CreateCollectionAsync();

	/// <summary>
	/// 判断 Collection 是否存在。
	/// </summary>
	Task<bool> ExistsAsync();

	/// <summary>
	/// 删除并重新创建 Collection。
	///
	/// 用于 Embedding 模型发生变化后，
	/// 清理旧向量并重新建立索引。
	/// </summary>
	Task RecreateCollectionAsync();

	/// <summary>
	/// 插入或更新 Vector。
	/// </summary>
	Task UpsertAsync(
		string id,
		float[] vector,
		Dictionary<string, object> payload);

	/// <summary>
	/// 批量插入或更新 Vector。顺序与输入一致。
	/// 默认实现退化为逐条 Upsert；具体实现（Qdrant）应覆盖为单次批量写入以减少往返。
	/// </summary>
	async Task UpsertBatchAsync(
		IEnumerable<(string Id, float[] VectorData, IReadOnlyDictionary<string, object> Payload)> points)
	{
		if (points == null)
			return;
		foreach (var (id, vector, payload) in points)
		{
			var dict = payload as Dictionary<string, object>
				?? new Dictionary<string, object>(payload);
			await UpsertAsync(id, vector, dict);
		}
	}

	/// <summary>
	/// 搜索 Vector。
	/// </summary>
	Task<List<VectorSearchResult>> QueryAsync(
		float[] vector,
		int limit = 10);

	/// <summary>
	/// 删除指定 Vector。
	/// </summary>
	Task DeleteAsync(
		string id);

	/// <summary>
	/// 列出 Collection 中全部 Vector Point ID（用于孤儿检测）。
	/// 实现应分页滚动获取，避免一次性加载向量本身。
	/// </summary>
	Task<IReadOnlyList<string>> ListPointIdsAsync(
		CancellationToken cancellationToken = default);
}
