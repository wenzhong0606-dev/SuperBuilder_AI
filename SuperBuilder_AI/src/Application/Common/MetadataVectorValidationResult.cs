namespace SuperBuilder_AI.Models.AI;

/// <summary>
/// 向量一致性校验结果：存储的向量维度与当前 Qdrant 配置维度不一致的记录。
/// </summary>
public class MetadataVectorValidationResult
{
	/// <summary>
	/// 当前 Qdrant 配置的目标维度。
	/// </summary>
	public int ExpectedDimension { get; set; }

	/// <summary>
	/// 维度不一致（被标记为 Stale）的记录数量。
	/// </summary>
	public int MismatchedCount { get; set; }

	/// <summary>
	/// 维度不一致记录的标识（table:/column:/semantic: 前缀）。
	/// </summary>
	public IReadOnlyList<string> MismatchedIds { get; set; }
		= Array.Empty<string>();
}
