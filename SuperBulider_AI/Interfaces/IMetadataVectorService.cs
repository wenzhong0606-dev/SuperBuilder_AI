using SuperBulider_AI.Models.AI;
using SuperBulider_AI.Models.Metadata;


namespace SuperBulider_AI.Interfaces;

/// <summary>
/// Metadata向量索引服务
/// </summary>
public interface IMetadataVectorService
{


	/// <summary>
	/// 创建Metadata表+字段向量
	/// </summary>
	Task<MetadataVectorIndexResult>
		IndexAsync(
			MetadataTable metadataTable);

}