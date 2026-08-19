namespace SuperBulider_AI.Models.BI;

/// <summary>
/// BI 对话响应。
/// </summary>
public sealed class BIResponse
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
	/// 用户原始问题。
	/// </summary>
	public string? Question
	{
		get;
		set;
	}

	/// <summary>
	/// 最终生成的 SQL。
	/// </summary>
	public string? Sql
	{
		get;
		set;
	}

	/// <summary>
	/// 查询返回的数据。
	/// </summary>
	public QueryResult? Data
	{
		get;
		set;
	}

	/// <summary>
	/// AI 对查询结果的自然语言回答。
	/// </summary>
	public QueryAnswer? Answer
	{
		get;
		set;
	}

	/// <summary>
	/// 错误信息。
	/// </summary>
	public string? ErrorMessage
	{
		get;
		set;
	}

	/// <summary>
	/// QueryPlan Explainability。
	///
	/// 包含：
	/// - QueryPlan
	/// - Validation
	/// - RepairTrace
	/// - Confidence
	/// - Decision
	/// - Explanation Summary
	///
	/// 即使 QueryPlan 最终被 Decision Gate 拒绝，
	/// Explanation 仍然可以返回给上层进行诊断和解释。
	/// </summary>
	public QueryPlanExplanation? Explanation
	{
		get;
		set;
	}
}