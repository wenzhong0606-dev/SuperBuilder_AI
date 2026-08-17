namespace SuperBulider_AI.Models.BI;

/// <summary>
/// QueryPlan语义验证结果。
///
/// Phase 2.2.3
///
/// 用于:
///
/// QuerySemanticValidator
///
/// 返回:
///
/// 是否通过
///
/// 错误列表
///
/// 警告列表
/// </summary>
public class QuerySemanticValidationResult
{
	/// <summary>
	/// 是否验证通过。
	///
	/// 当不存在 Error 级别问题时:
	///
	/// true
	/// </summary>
	public bool IsValid
	{
		get
		{
			return Errors.Count == 0;
		}
	}



	/// <summary>
	/// 所有验证问题。
	/// </summary>
	public List<SemanticValidationError> Errors
	{
		get;
		set;
	}
		= new();



	/// <summary>
	/// 获取错误列表。
	/// </summary>
	public IEnumerable<SemanticValidationError>
		ErrorItems =>
			Errors.Where(
				x =>
					x.Severity ==
					SemanticValidationSeverity.Error);



	/// <summary>
	/// 获取警告列表。
	/// </summary>
	public IEnumerable<SemanticValidationError>
		WarningItems =>
			Errors.Where(
				x =>
					x.Severity ==
					SemanticValidationSeverity.Warning);



	/// <summary>
	/// 添加错误。
	/// </summary>
	public void AddError(
		SemanticValidationError error)
	{
		ArgumentNullException.ThrowIfNull(error);

		Errors.Add(error);
	}



	/// <summary>
	/// 添加错误快捷方法。
	/// </summary>
	public void AddError(
		string type,
		string field,
		string message,
		long? metadataColumnId = null,
		long? metadataTableId = null)
	{
		Errors.Add(
			new SemanticValidationError
			{
				Type = type,

				Field = field,

				Message = message,

				MetadataColumnId =
					metadataColumnId,

				MetadataTableId =
					metadataTableId,

				Severity =
					SemanticValidationSeverity.Error
			});
	}



	/// <summary>
	/// 添加警告。
	/// </summary>
	public void AddWarning(
		string type,
		string field,
		string message,
		long? metadataColumnId = null,
		long? metadataTableId = null)
	{
		Errors.Add(
			new SemanticValidationError
			{
				Type = type,

				Field = field,

				Message = message,

				MetadataColumnId =
					metadataColumnId,

				MetadataTableId =
					metadataTableId,

				Severity =
					SemanticValidationSeverity.Warning
			});
	}
}