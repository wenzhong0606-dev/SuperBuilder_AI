using SuperBulider_AI.Models.AI;

namespace SuperBulider_AI.Interfaces;

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
}