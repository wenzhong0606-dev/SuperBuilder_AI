using System.Collections.Generic;
using SuperBuilder_AI.Models.AI;
using System.Threading;

namespace SuperBuilder_AI.Interfaces;

/// <summary>
/// 向量检索过滤条件（§L.4）：限定到租户 + 一组 (数据源, 版本) 组合。
/// 语义搜索据此构造 Qdrant filter：must tenant_id==TenantId，should [(ds==D AND version==V) ...]。
/// </summary>
public sealed record VectorSearchFilter(
	long TenantId,
	IReadOnlyList<(long DataSourceId, int MetadataVersion)> AllowedVersions,
	/// <summary>
	/// 过渡期兼容兜底（§L.4）：为 true 时在 should 中加入「metadata_version 缺字段」分支，
	/// 保证尚未回填的存量 untagged point 仍可被召回。回填完成后由调用方置 false，退化为严格过滤。
	/// </summary>
	bool IncludeUntagged = false);

/// <summary>
/// Qdrant 向量存储接口。
/// </summary>
public interface IQdrantService
{
	/// <summary>创建 Collection。如果已经存在，则不重复创建。</summary>
	Task CreateCollectionAsync();

	/// <summary>判断 Collection 是否存在。</summary>
	Task<bool> ExistsAsync();

	/// <summary>删除并重新创建 Collection。用于 Embedding 模型发生变化后，清理旧向量并重新建立索引。</summary>
	Task RecreateCollectionAsync();

	/// <summary>插入或更新 Vector。</summary>
	/// <param name="ct">取消令牌，透传给底层 gRPC 调用。</param>
	Task UpsertAsync(
		string id,
		float[] vector,
		Dictionary<string, object> payload,
		CancellationToken ct = default);

	/// <summary>
	/// 批量插入或更新 Vector。顺序与输入一致。
	/// 默认实现退化为逐条 Upsert；具体实现（Qdrant）应覆盖为单次批量写入以减少往返。
	/// </summary>
	/// <param name="ct">取消令牌，透传给底层 gRPC 调用。</param>
	async Task UpsertBatchAsync(
		IEnumerable<(string Id, float[] VectorData, IReadOnlyDictionary<string, object> Payload)> points,
		CancellationToken ct = default)
	{
		if (points == null)
			return;
		foreach (var (id, vector, payload) in points)
		{
			var dict = payload as Dictionary<string, object>
				?? new Dictionary<string, object>(payload);
			await UpsertAsync(id, vector, dict, ct);
		}
	}

	/// <summary>搜索 Vector（不指定过滤，沿用旧行为全量召回）。</summary>
	Task<List<VectorSearchResult>> QueryAsync(float[] vector, int limit = 10);

	/// <summary>
	/// 搜索 Vector（§L.4 版本隔离）。传入 <paramref name="filter"/> 时按租户 + (数据源,版本) 组合约束召回；
	/// filter 为 null 时退化为全量召回（阶段 A 兼容）。
	/// 默认实现：忽略 filter，退化为全量召回（与旧行为一致）；具体实现（QdrantService）覆盖为带过滤召回。
	/// </summary>
	async Task<List<VectorSearchResult>> QueryAsync(
		float[] vector,
		int limit,
		VectorSearchFilter? filter,
		CancellationToken ct = default)
	{
		return await QueryAsync(vector, limit);
	}

	/// <summary>
	/// 取回指定 point 的向量与 payload（§L.4 存量回填用，避免重新 embedding）。
	/// 不存在返回 null。默认实现返回 null（无存储后端）。
	/// </summary>
	async Task<(float[] Vector, IReadOnlyDictionary<string, object> Payload)?> RetrieveVectorAsync(
		string id,
		CancellationToken ct = default)
	{
		return null;
	}

	/// <summary>批量删除指定 Vector。默认实现为 no-op；具体实现（QdrantService）覆盖为真实批量删除。</summary>
	async Task DeleteBatchAsync(IEnumerable<string> ids, CancellationToken ct = default)
	{
		await Task.CompletedTask;
	}

	/// <summary>删除指定 Vector。</summary>
	Task DeleteAsync(string id);

	/// <summary>列出 Collection 中全部 Vector Point ID（用于孤儿检测）。实现应分页滚动获取，避免一次性加载向量本身。</summary>
	Task<IReadOnlyList<string>> ListPointIdsAsync(CancellationToken cancellationToken = default);
}
