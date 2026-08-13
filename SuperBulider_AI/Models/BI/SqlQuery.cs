namespace SuperBulider_AI.Models.BI;

/// <summary>
/// 动态SQL查询对象。
///
/// 用于:
///
/// QueryPlan
///
/// 转换为:
///
/// 可执行SQL
///
/// </summary>
public class SqlQuery
{


	/// <summary>
	/// SQL文本。
	/// </summary>
	public string Sql
	{
		get;
		set;
	}
	=
	string.Empty;




	/// <summary>
	/// 参数集合。
	///
	/// 防止SQL注入。
	/// </summary>
	public Dictionary<string, object?>
		Parameters
	{
		get;
		set;
	}
	=
	new();



}