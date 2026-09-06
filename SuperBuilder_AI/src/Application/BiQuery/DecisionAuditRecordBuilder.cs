using System.Linq;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 从 <see cref="QueryPlanPipelineContext"/> 提取决策全过程审计字段（M5-09）。
///
/// 纯静态工厂，不持有状态、不依赖 DI，便于独立测试。
/// 对上下文不完整（如 EarlyResponse 短路于早期阶段，Plan/Decision 等尚为空）
/// 的情况优雅降级：缺失字段记为 null/0/空，不抛异常。
/// </summary>
public static class DecisionAuditRecordBuilder
{
	/// <summary>
	/// 构建一条审计记录。
	/// </summary>
	/// <param name="ctx">管线运行上下文。</param>
	/// <param name="outcome">本次运行最终结果分类。</param>
	/// <param name="correlationId">请求级关联标识。</param>
	/// <param name="errorMessage">EarlyResponse / Rejected 携带的错误信息（可选）。</param>
	/// <param name="model">生成计划的 LLM 模型标识（预留，可选）。</param>
	/// <returns>完整审计记录。</returns>
	public static QueryPlanDecisionAuditRecord Build(
		QueryPlanPipelineContext ctx,
		AuditOutcome outcome,
		string? correlationId,
		string? errorMessage = null,
		string? model = null)
	{
		var plan = ctx.Plan;
		var confidence = ctx.Confidence;
		var decision = ctx.Decision;
		var repairTrace = ctx.SemanticValidation?.RepairTrace;

		var record = new QueryPlanDecisionAuditRecord
		{
			// 标识
			CorrelationId = correlationId,
			Outcome = outcome,
			ErrorMessage = errorMessage,

			// 1. 问题
			Question = ctx.Question,

			// 2. 意图
			IntentType = ctx.Intent?.IntentType,
			IntentSummary = BuildIntentSummary(ctx.Intent),

			// 3. 计划（SQL 可重建源）
			PlanTableNames = plan?.Tables?
				.Select(t => t.TableName)
				.Where(t => !string.IsNullOrWhiteSpace(t))
				.Select(t => t!)
				.ToList() ?? new List<string>(),
			PlanFields = plan?.Fields?
				.Select(f => f.ColumnName)
				.Where(f => !string.IsNullOrWhiteSpace(f))
				.Select(f => f!)
				.ToList() ?? new List<string>(),
			PlanFilterCount = plan?.Filters?.Count ?? 0,
			PlanOrderCount = plan?.Orders?.Count ?? 0,
			PlanJoinCount = plan?.Joins?.Count ?? 0,
			PlanLimit = plan?.Limit,
			PlanIsAggregate = plan?.IsAggregate ?? false,

			// 4. SQL（预留，管线不生成 SQL 文本）
			Sql = null,

			// 5. 修复
			HasRepair = (repairTrace?.TotalAttempts ?? 0) > 0,
			RepairStatus = repairTrace?.Status,
			RepairAttempts = repairTrace?.TotalAttempts ?? 0,
			RepairChangedPlanCount = repairTrace?.ChangedPlanCount ?? 0,
			RepairStopReason = repairTrace?.StopReason,
			RepairHistoryCount = repairTrace?.History?.Count ?? 0,

			// 6. 置信度
			ConfidenceScore = confidence?.Score,
			ConfidenceLevel = confidence?.Level,
			CanProceed = confidence?.CanProceed,
			IsExecutableDetailQuery = confidence?.IsExecutableDetailQuery,
			ValidationErrorCount = confidence?.Evidence?.ValidationErrorCount,
			RepairCount = confidence?.Evidence?.RepairCount,

			// 7. 决策
			DecisionType = decision?.Decision,
			DecisionReason = decision?.Reason,
			ShouldExecute = decision?.ShouldExecute,
			RequiresConfirmation = decision?.RequiresConfirmation,
			DecisionTraceSummary = BuildTraceSummary(decision, confidence),

			// 8. 模型（预留）
			Model = model
		};

		return record;
	}

	private static string? BuildIntentSummary(QueryIntent? intent)
	{
		if (intent is null)
			return null;

		var metricCount = intent.Metrics?.Count ?? 0;
		var dimCount = intent.Dimensions?.Count ?? 0;
		var filterCount = intent.Filters?.Count ?? 0;
		var limit = intent.Limit?.ToString() ?? "none";

		return $"type={intent.IntentType}; metrics={metricCount}; dims={dimCount}; " +
		       $"filters={filterCount}; limit={limit}";
	}

	private static string? BuildTraceSummary(
		QueryPlanDecision? decision,
		QueryPlanConfidence? confidence)
	{
		if (decision is null && confidence is null)
			return null;

		var evidence = confidence?.Evidence;

		return $"conf={confidence?.Score:F3}({confidence?.Level}); " +
		       $"valErr={evidence?.ValidationErrorCount ?? 0}; " +
		       $"repairCnt={evidence?.RepairCount ?? 0}; " +
		       $"repairStalled={evidence?.RepairStalled}; " +
		       $"repairLoop={evidence?.RepairLoopDetected}; " +
		       $"reason={decision?.Reason}";
	}
}
