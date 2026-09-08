namespace SuperBuilder_AI.Application.Common.Options;

/// <summary>
/// Qdrant配置
/// </summary>
public class QdrantOptions
{

	/// <summary>
	/// Qdrant地址
	/// </summary>
	public string Host { get; set; }
		= "localhost";


	/// <summary>
	/// gRPC端口
	/// </summary>
	public int Port { get; set; }
		= 6334;


	/// <summary>
	/// Collection名称
	/// </summary>
	public string CollectionName { get; set; }
		= "superbi_metadata";


	/// <summary>
	/// Vector维度
	/// 测试使用1536
	/// </summary>
	public ulong VectorSize { get; set; }
		= 1024;

}