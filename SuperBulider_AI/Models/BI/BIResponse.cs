namespace SuperBulider_AI.Models.AI;

/// <summary>
/// AI BI完整响应结果。
///
/// 用于承载一次完整自然语言BI查询的最终结果。
///
/// 流程:
///
/// 用户问题
///     ↓
/// QueryIntent
///     ↓
/// QueryPlan
///     ↓
/// SqlQuery
///     ↓
/// QueryResult
///     ↓
/// QueryAnswer
///
/// 最终统一返回。
/// </summary>
public class BIResponse
{
	/// <summary>
	/// 是否执行成功。
	/// </summary>
	public bool Success
	{
		get;
		set;
	}

	/// <summary>
	/// 用户原始问题。
	/// </summary>
	public string Question
	{
		get;
		set;
	}
	=
	string.Empty;

	/// <summary>
	/// AI生成的SQL。
	/// </summary>
	public string? Sql
	{
		get;
		set;
	}

	/// <summary>
	/// SQL查询返回的数据。
	/// </summary>
	public QueryResult? Data
	{
		get;
		set;
	}

	/// <summary>
	/// AI对查询结果的自然语言分析。
	/// </summary>
	public QueryAnswer? Answer
	{
		get;
		set;
	}

	/// <summary>
	/// 查询过程中发生的错误信息。
	/// </summary>
	public string? ErrorMessage
	{
		get;
		set;
	}
}