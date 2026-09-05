using SuperBuilder_AI.Models.AI;

namespace SuperBuilder_AI.Interfaces;

/// <summary>
/// Metadata 向量索引生命周期管理服务。
/// </summary>
public interface IMetadataVectorIndexService
{
	/// <summary>
	/// 删除当前 Collection。
	/// </summary>
	Task ClearAsync();

	/// <summary>
	/// 确保 Collection 存在。
	/// </summary>
	Task CreateAsync();

	/// <summary>
	/// 清空并重新建立全部 Metadata Vector。
	/// </summary>
	Task<MetadataVectorRebuildResult>
		RebuildAsync();

	/// <summary>
	/// 为指定 MetadataTable 建立向量。
	/// </summary>
	Task<MetadataVectorIndexResult>
		IndexAsync(
			long metadataTableId);

	/// <summary>
	/// 孤儿检测：找出 Qdrant 中存在但数据库中已无对应记录的 Vector Point。
	/// </summary>
	Task<MetadataVectorOrphanResult>
		DetectOrphansAsync(
			CancellationToken cancellationToken = default);

	/// <summary>
	/// 校验向量维度一致性，将维度不匹配的记录标记为 Stale。
	/// </summary>
	Task<MetadataVectorValidationResult>
		ValidateVectorsAsync(
			CancellationToken cancellationToken = default);
}