using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.Metadata;
using System.Threading;

namespace SuperBuilder_AI.Interfaces;

/// <summary>
/// Metadata向量索引服务
/// </summary>
public interface IMetadataVectorService
{
	/// <summary>
	/// 创建Metadata表+字段向量
	/// </summary>
	/// <param name="ct">取消令牌，透传给 Embedding / Qdrant 调用，使挂起请求可被 job 取消中断。</param>
	Task<MetadataVectorIndexResult>
		IndexAsync(
			MetadataTable metadataTable,
			CancellationToken ct = default);
}
