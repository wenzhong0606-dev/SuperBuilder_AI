namespace SuperBulider_AI.Models.Metadata;

/// <summary>
/// Metadata检索结果
/// </summary>
public class MetadataSearchResult
{


	/// <summary>
	/// 表ID
	/// </summary>
	public long TableId { get; set; }



	/// <summary>
	/// 表名称
	/// </summary>
	public string TableName { get; set; } = string.Empty;



	/// <summary>
	/// 字段列表
	/// </summary>
	public List<string> Columns { get; set; }
		= new();



	/// <summary>
	/// 匹配分数
	/// </summary>
	public double Score { get; set; }

}