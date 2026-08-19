using SuperBulider_AI.Interfaces.BI;
using SuperBulider_AI.Models.BI;

namespace SuperBulider_AI.Services.BI;

/// <summary>
/// QueryPlan Explainability Service。
///
/// Phase 2.5。
///
/// 将 QueryPlan Pipeline 已产生的结果
/// 聚合为 QueryPlanExplanation。
/// </summary>
public sealed class QueryPlanExplainabilityService
	: IQueryPlanExplainabilityService
{
	/// <inheritdoc />
	public QueryPlanExplanation Explain(
		string? question,
		QueryPlan? plan,
		QuerySemanticValidationResult? validationResult,
		QueryPlanRepairTrace? repairTrace,
		QueryPlanConfidence? confidence,
		QueryPlanDecision? decision)
	{
		var explanation = new QueryPlanExplanation
		{
			Question = question,
			Plan = plan,
			ValidationResult = validationResult,
			RepairTrace = repairTrace,
			Confidence = confidence,
			Decision = decision
		};

		BuildSummary(explanation);

		return explanation;
	}


	/// <summary>
	/// 从已有 Pipeline 结果构建 Summary。
	///
	/// 不重新计算任何核心决策。
	/// </summary>
	private static void BuildSummary(
		QueryPlanExplanation explanation)
	{
		var summary = explanation.Summary;


		// -----------------------------
		// Validation
		// -----------------------------

		var validation =
			explanation.ValidationResult;

		if (validation is not null)
		{
			summary.IsValid =
				validation.IsValid;

			summary.ValidationErrorCount =
				validation.ErrorItems.Count();

			summary.ValidationWarningCount =
				validation.WarningItems.Count();
		}


		// -----------------------------
		// Repair
		// -----------------------------

		var repair =
			explanation.RepairTrace;

		if (repair is not null)
		{
			summary.HasRepair =
				repair.TotalAttempts > 0;

			summary.RepairCount =
				repair.TotalAttempts;

			summary.PlanChangedByRepair =
				repair.ChangedPlanCount > 0;
		}


		// -----------------------------
		// Confidence
		// -----------------------------

		var confidence =
			explanation.Confidence;

		if (confidence is not null)
		{
			summary.ConfidenceScore =
				confidence.Score;

			summary.Reasons =
				new List<string>(
					confidence.Reasons);

			summary.BlockingReasons =
				new List<string>(
					confidence.BlockingReasons);
		}


		// -----------------------------
		// Decision
		// -----------------------------

		var decision =
			explanation.Decision;

		if (decision is not null)
		{
			summary.ShouldExecute =
				decision.ShouldExecute;

			summary.RequiresConfirmation =
				decision.RequiresConfirmation;

			summary.DecisionReason =
				decision.Reason;
		}


		// -----------------------------
		// Explainability Availability
		// -----------------------------

		summary.IsExplainable =
			explanation.Plan is not null
			||
			explanation.ValidationResult is not null
			||
			explanation.RepairTrace is not null
			||
			explanation.Confidence is not null
			||
			explanation.Decision is not null;
	}
}