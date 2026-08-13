namespace SuperBulider_AI.Models.AI;

/// <summary>
/// 数据可视化建议。
///
/// 用于AI推荐前端图表。
///
/// </summary>
public class VisualizationSuggestion
{


	/// <summary>
	/// 图表类型。
	///
	/// 示例:
	///
	/// bar
	/// line
	/// pie
	/// table
	///
	/// </summary>
	public string Type
	{
		get;
		set;
	}
	=
	string.Empty;





	/// <summary>
	/// 图表标题。
	/// </summary>
	public string Title
	{
		get;
		set;
	}
	=
	string.Empty;






	/// <summary>
	/// X轴字段。
	/// </summary>
	public string?
	XAxis
	{
		get;
		set;
	}





	/// <summary>
	/// Y轴字段。
	/// </summary>
	public List<string>
		YAxis
	{
		get;
		set;
	}
	=
	new();





	/// <summary>
	/// AI生成原因。
	///
	/// 示例:
	///
	/// 适合展示月份趋势变化。
	///
	/// </summary>
	public string?
	Reason
	{
		get;
		set;
	}


}