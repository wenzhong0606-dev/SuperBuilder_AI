using SuperBuilder_AI.Models;

namespace SuperBuilder_AI.Models.Metadata;

/// <summary>
/// 表示一条元数据学习记录，用于记录与元数据列相关的问题、回答是否正确以及用户反馈。
/// </summary>
public class MetadataLearningRecord : BaseEntity
{
	/// <summary>
	/// 所属租户的标识。
	/// </summary>
	public long? TenantId { get; set; }

	/// <summary>
	/// 提示或问题内容。
	/// </summary>
	public string? Question { get; set; }

	/// <summary>
	/// 关联的元数据列的标识。
	/// </summary>
	public long? MetadataColumnId { get; set; }

	/// <summary>
	/// 用户回答是否正确。
	/// </summary>
	public bool? Correct { get; set; }

	/// <summary>
	/// 用户或系统提供的反馈信息。
	/// </summary>
	public string? Feedback { get; set; }
}
