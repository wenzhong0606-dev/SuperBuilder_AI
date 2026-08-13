namespace SuperBulider_AI.Models.AI;

/// <summary>
/// Metadata 全量向量重建结果。
/// </summary>
public class MetadataVectorRebuildResult
{
	/// <summary>
	/// 是否成功。
	/// </summary>
	public bool Success { get; set; }

	/// <summary>
	/// 处理的 MetadataTable 数量。
	/// </summary>
	public int TableCount { get; set; }

	/// <summary>
	/// 建立的 Table Vector 数量。
	/// </summary>
	public int TableVectorCount { get; set; }

	/// <summary>
	/// 建立的 Column Vector 数量。
	/// </summary>
	public int ColumnVectorCount { get; set; }

	/// <summary>
	/// 建立的 Semantic Vector 数量。
	/// </summary>
	public int SemanticVectorCount { get; set; }

	/// <summary>
	/// 总 Vector 数量。
	/// </summary>
	public int TotalVectorCount =>
		TableVectorCount +
		ColumnVectorCount +
		SemanticVectorCount;

	/// <summary>
	/// 错误信息。
	/// </summary>
	public string? ErrorMessage { get; set; }
}