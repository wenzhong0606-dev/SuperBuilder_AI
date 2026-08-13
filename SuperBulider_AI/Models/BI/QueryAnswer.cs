namespace SuperBulider_AI.Models.BI;

/// <summary>
/// AI查询结果回答。
///
/// 用于:
///
/// QueryResult
///       ↓
/// AI分析
///       ↓
/// 用户最终回答
///
/// </summary>
public class QueryAnswer
{


	/// <summary>
	/// 是否成功。
	/// </summary>
	public bool Success
	{
		get;
		set;
	}



	/// <summary>
	/// 用户问题。
	/// </summary>
	public string Question
	{
		get;
		set;
	}
	=
	string.Empty;




	/// <summary>
	/// AI自然语言回答。
	///
	/// 示例:
	///
	/// 2025年销售金额为500万元，
	/// 相比去年增长15%。
	///
	/// </summary>
	public string Answer
	{
		get;
		set;
	}
	=
	string.Empty;





	/// <summary>
	/// 数据摘要。
	///
	/// 用于前端展示。
	/// </summary>
	public Dictionary<string, object?>
		Summary
	{
		get;
		set;
	}
	=
	new();





	/// <summary>
	/// 图表建议。
	/// </summary>
	public List<VisualizationSuggestion>
		Visualizations
	{
		get;
		set;
	}
	=
	new();





	/// <summary>
	/// AI分析耗时。
	/// </summary>
	public long ElapsedMilliseconds
	{
		get;
		set;
	}



	/// <summary>
	/// 错误信息。
	/// </summary>
	public string?
	ErrorMessage
	{
		get;
		set;
	}

}