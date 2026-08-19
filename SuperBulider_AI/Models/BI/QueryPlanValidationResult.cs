using System.Text;

namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// 查询计划验证结果。
///
/// 用于描述 QueryPlan 是否满足后续 SQL 构建和执行的基本要求。
///
/// 注意：
///
/// QueryPlanValidationResult 只描述验证结果，
/// 不负责修改 QueryPlan。
/// </summary>
public class QueryPlanValidationResult
{
	/// <summary>
	/// 是否验证通过。
	///
	/// 只要存在 Error，
	/// IsValid 就为 false。
	/// </summary>
	public bool IsValid
	{
		get
		{
			return Issues.All(
				x => x.IsError == false);
		}
	}

	/// <summary>
	/// 验证问题集合。
	/// </summary>
	public List<QueryPlanValidationIssue> Issues { get; set; }
		= new();

	/// <summary>
	/// 错误数量。
	/// </summary>
	public int ErrorCount
	{
		get
		{
			return Issues.Count(
				x => x.IsError);
		}
	}

	/// <summary>
	/// 警告数量。
	/// </summary>
	public int WarningCount
	{
		get
		{
			return Issues.Count(
				x => x.IsError == false);
		}
	}

	/// <summary>
	/// 添加错误。
	/// </summary>
	public void AddError(
		string code,
		string message,
		string? property = null)
	{
		Issues.Add(
			QueryPlanValidationIssue.Error(
				code,
				message,
				property));
	}

	/// <summary>
	/// 添加警告。
	/// </summary>
	public void AddWarning(
		string code,
		string message,
		string? property = null)
	{
		Issues.Add(
			QueryPlanValidationIssue.Warning(
				code,
				message,
				property));
	}

	/// <summary>
	/// 将验证问题转换为异常消息。
	///
	/// 主要用于当前 Phase 2.1：
	///
	/// QueryPlanBuilder
	///     ↓
	/// Validation
	///     ↓
	/// Invalid
	///     ↓
	/// InvalidOperationException
	///
	/// 后续 Phase 2.3 建立自动修正后，
	/// BIConversationService 可以直接使用 Issues，
	/// 不再依赖异常作为控制流程。
	/// </summary>
	public string ToErrorMessage()
	{
		if (IsValid)
		{
			return "QueryPlan验证通过。";
		}

		var builder =
			new StringBuilder();

		builder.Append(
			"QueryPlan验证失败。");

		foreach (var issue in Issues.Where(
			x => x.IsError))
		{
			builder.AppendLine();

			builder.Append(
				$"[{issue.Code}] ");

			builder.Append(
				issue.Message);
		}

		return builder.ToString();
	}
}