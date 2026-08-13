using SuperBulider_AI.Models.Metadata;


namespace SuperBulider_AI.Models.BI;

/// <summary>
/// 查询表计划。
///
/// 对应:
///
/// MetadataTable
///
/// 用于动态查询生成阶段。
/// </summary>
public class QueryTable
{


	/// <summary>
	/// Metadata表Id。
	/// </summary>
	public long MetadataTableId
	{
		get;
		set;
	}



	/// <summary>
	/// 数据源Id。
	/// </summary>
	public long DataSourceId
	{
		get;
		set;
	}



	/// <summary>
	/// 数据库表名。
	/// </summary>
	public string? TableName
	{
		get;
		set;
	}



	/// <summary>
	/// 表描述。
	/// </summary>
	public string? TableComment
	{
		get;
		set;
	}

}
