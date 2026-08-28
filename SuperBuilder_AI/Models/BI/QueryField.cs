namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// 查询字段计划。
///
/// 对应:
///
/// MetadataColumn
///
/// </summary>
public class QueryField
{


	/// <summary>
	/// Metadata字段Id。
	/// </summary>
	public long MetadataColumnId
	{
		get;
		set;
	}



	/// <summary>
	/// 字段名称。
	/// </summary>
	public string? ColumnName
	{
		get;
		set;
	}



	/// <summary>
	/// 聚合方式。
	///
	/// SUM
	/// COUNT
	/// AVG
	/// MAX
	/// MIN
	///
	/// </summary>
	public string? Aggregation
	{
		get;
		set;
	}



	/// <summary>
	/// 数据类型。
	/// </summary>
	public string? DataType
	{
		get;
		set;
	}


}
