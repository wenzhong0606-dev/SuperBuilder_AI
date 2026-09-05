using SuperBuilder_AI.Models;
using SuperBuilder_AI.Models.Organization;

namespace SuperBuilder_AI.Models.Metadata;

/// <summary>
/// 表示一条元数据学习记录，用于记录与元数据列相关的问题、回答是否正确以及用户反馈。
/// </summary>
public class MetadataLearningRecord : BaseEntity
{
	/// <summary>
	/// 所属租户的标识（必填）。
	/// </summary>
	public long TenantId { get; set; }

	/// <summary>
	/// 关联租户实体。
	/// </summary>
	public Tenant? Tenant { get; set; }

	/// <summary>
	/// 提示或问题内容。
	/// </summary>
	public string? Question { get; set; }

	/// <summary>
	/// 关联的元数据列的标识。
	/// </summary>
	public long? MetadataColumnId { get; set; }

	/// <summary>
	/// 关联的元数据列实体。
	/// </summary>
	public MetadataColumn? MetadataColumn { get; set; }

	/// <summary>
	/// 用户回答是否正确。
	/// </summary>
	public bool? Correct { get; set; }

	/// <summary>
	/// 用户或系统提供的反馈信息。
	/// </summary>
	public string? Feedback { get; set; }

	/// <summary>
	/// 校验学习记录的租户一致性：若同时关联了元数据列，
	/// 则记录的租户必须与列所属表的租户一致。
	/// </summary>
	public static bool IsTenantConsistent(
		long recordTenantId,
		long? columnTenantId)
	{
		if (columnTenantId is null)
		{
			return true;
		}

		return recordTenantId == columnTenantId.Value;
	}
}
