namespace SuperBulider_AI.Models.AI;

/// <summary>
/// 查询执行结果。
///
/// 用于承载动态业务数据库查询结果。
///
/// 数据结构:
///
/// List
///   Dictionary
///
/// 示例:
///
/// [
///   {
///     "Customer":"张三",
///     "Amount":1000
///   }
/// ]
///
/// </summary>
public class QueryResult
{


	/// <summary>
	/// 查询是否成功。
	/// </summary>
	public bool Success
	{
		get;
		set;
	}




	/// <summary>
	/// 查询结果数据。
	///
	/// 每一项代表一行数据。
	/// </summary>
	public List<Dictionary<string, object?>>
		Rows
	{
		get;
		set;
	}
	=
	new();





	/// <summary>
	/// 返回数据数量。
	/// </summary>
	public int Count
	{
		get
		{
			return Rows.Count;
		}
	}





	/// <summary>
	/// 错误信息。
	///
	/// 查询失败时使用。
	/// </summary>
	public string?
	ErrorMessage
	{
		get;
		set;
	}


}