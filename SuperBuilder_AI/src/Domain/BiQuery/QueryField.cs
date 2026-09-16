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
	/// 字段所属 MetadataTable Id。
	/// 多表 JOIN 中用于生成限定列名，避免同名字段歧义。
	/// </summary>
	public long MetadataTableId
	{
		get;
		set;
	}

	/// <summary>
	/// 字段所属物理表名。
	/// 仅作为 SQL 生成期的运行时绑定，不参与语义推理。
	/// </summary>
	public string? TableName
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
