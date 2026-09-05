namespace SuperBuilder_AI.Models.AI;

/// <summary>
/// 向量孤儿检测结果：Qdrant 中存在但数据库中已无对应 Metadata 记录的 Vector Point。
/// </summary>
public class MetadataVectorOrphanResult
{
	/// <summary>
	/// Qdrant 中的 Point 总数。
	/// </summary>
	public int QdrantPointCount { get; set; }

	/// <summary>
	/// 数据库中引用的 Vector ID 数量。
	/// </summary>
	public int DatabaseVectorCount { get; set; }

	/// <summary>
	/// 孤儿（仅存在于 Qdrant）数量。
	/// </summary>
	public int OrphanCount { get; set; }

	/// <summary>
	/// 孤儿 Point ID 列表。
	/// </summary>
	public IReadOnlyList<string> OrphanIds { get; set; }
		= Array.Empty<string>();
}
