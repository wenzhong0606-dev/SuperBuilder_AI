using SuperBulider_AI.Models.AI;


namespace SuperBulider_AI.Interfaces;

/// <summary>
/// Qdrant向量存储接口
/// </summary>
public interface IQdrantService
{


	/// <summary>
	/// 创建Collection
	/// </summary>
	Task CreateCollectionAsync();



	/// <summary>
	/// 判断Collection
	/// </summary>
	Task<bool> ExistsAsync();



	/// <summary>
	/// 插入Vector
	/// </summary>
	Task UpsertAsync(
		string id,
		float[] vector,
		Dictionary<string, object> payload);



	/// <summary>
	/// 搜索Vector
	/// </summary>
	Task<List<VectorSearchResult>> QueryAsync(
		float[] vector,
		int limit = 10);



	/// <summary>
	/// 删除Vector
	/// </summary>
	Task DeleteAsync(
		string id);

}