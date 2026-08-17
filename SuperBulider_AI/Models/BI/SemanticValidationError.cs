namespace SuperBulider_AI.Models.BI;

/// <summary>
/// QueryPlan语义验证错误。
///
/// Phase 2.2.3
///
/// 描述:
///
/// QueryPlan
///     ↓
/// MetadataSemantic
///
/// 映射过程中发现的问题。
/// </summary>
public class SemanticValidationError
{
	/// <summary>
	/// 错误类型。
	///
	/// 示例:
	///
	/// FieldNotFound
	///
	/// SemanticMismatch
	///
	/// InvalidAggregation
	///
	/// InvalidDimension
	/// </summary>
	public string Type { get; set; }
		= string.Empty;



	/// <summary>
	/// 错误字段。
	///
	/// 对应:
	///
	/// QueryField.ColumnName
	///
	/// QueryMetric.Field
	///
	/// QueryDimension.Field
	/// </summary>
	public string Field { get; set; }
		= string.Empty;



	/// <summary>
	/// MetadataColumn Id。
	///
	/// 如果无法关联Metadata，
	/// 可以为空。
	/// </summary>
	public long?
	MetadataColumnId
	{ get; set; }



	/// <summary>
	/// MetadataTable Id。
	/// </summary>
	public long?
	MetadataTableId
	{ get; set; }



	/// <summary>
	/// 错误描述。
	/// </summary>
	public string Message { get; set; }
		= string.Empty;



	/// <summary>
	/// 严重级别。
	///
	/// Error:
	/// 阻止SQL生成
	///
	/// Warning:
	/// 允许继续
	/// </summary>
	public SemanticValidationSeverity Severity { get; set; }
		= SemanticValidationSeverity.Error;
}



/// <summary>
/// 语义验证严重级别。
/// </summary>
public enum SemanticValidationSeverity
{
	/// <summary>
	/// 错误。
	///
	/// 必须修复。
	/// </summary>
	Error = 1,


	/// <summary>
	/// 警告。
	///
	/// 可以继续执行。
	/// </summary>
	Warning = 2
}