using SuperBulider_AI.Interfaces;
using SuperBulider_AI.Interfaces.BI;
using SuperBulider_AI.Models.AI;
using SuperBulider_AI.Models.BI;

namespace SuperBulider_AI.Services.BI;

/// <summary>
/// QueryPlan Confidence 评估服务。
///
/// Phase 2.4
/// QueryPlan Confidence & Decision Gate
///
/// 核心职责：
///
/// QueryPlan
///     +
/// ValidationResult
///     +
/// RepairTrace
///     +
/// Metadata Semantic Search
///     ↓
/// Confidence Evidence
///     ↓
/// QueryPlanConfidence
///
/// 注意：
///
/// 本服务不负责：
///
/// - QueryPlan 构建
/// - QueryPlan Validation
/// - QueryPlan Repair
/// - SQL Builder
/// - SQL Execution
/// - Decision Gate
///
/// 本服务只负责：
///
/// 1. 收集 Confidence Evidence
/// 2. 计算 Confidence Score
/// 3. 生成 Confidence Level
/// 4. 生成可解释 Reasons
/// 5. 生成 BlockingReasons
/// </summary>
public sealed class QueryPlanConfidenceService
	: IQueryPlanConfidenceService
{
	private readonly IMetadataSemanticSearchService
		_metadataSemanticSearchService;


	/// <summary>
	/// Semantic Evidence 权重。
	/// </summary>
	private const double SemanticWeight = 0.20;


	/// <summary>
	/// Table Evidence 权重。
	/// </summary>
	private const double TableWeight = 0.15;


	/// <summary>
	/// Field Evidence 权重。
	/// </summary>
	private const double FieldWeight = 0.15;


	/// <summary>
	/// Metric Evidence 权重。
	/// </summary>
	private const double MetricWeight = 0.15;


	/// <summary>
	/// Dimension Evidence 权重。
	/// </summary>
	private const double DimensionWeight = 0.10;


	/// <summary>
	/// Filter Evidence 权重。
	/// </summary>
	private const double FilterWeight = 0.10;


	/// <summary>
	/// Validation Evidence 权重。
	/// </summary>
	private const double ValidationWeight = 0.10;


	/// <summary>
	/// Repair Stability 权重。
	/// </summary>
	private const double RepairStabilityWeight = 0.05;


	/// <summary>
	/// High Confidence 阈值。
	/// </summary>
	private const double HighConfidenceThreshold = 0.80;


	/// <summary>
	/// Medium Confidence 阈值。
	/// </summary>
	private const double MediumConfidenceThreshold = 0.60;


	/// <summary>
	/// 构造函数。
	/// </summary>
	public QueryPlanConfidenceService(
		IMetadataSemanticSearchService
			metadataSemanticSearchService)
	{
		ArgumentNullException.ThrowIfNull(
			metadataSemanticSearchService);

		_metadataSemanticSearchService =
			metadataSemanticSearchService;
	}


	/// <summary>
	/// 评估 QueryPlan Confidence。
	/// </summary>
	public async Task<QueryPlanConfidence> EvaluateAsync(
		QueryPlan plan,
		QueryPlanValidationPipelineResult validationResult,
		QueryPlanRepairTrace? repairTrace,
		string question,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(plan);

		ArgumentNullException.ThrowIfNull(
			validationResult);

		question ??= string.Empty;


		cancellationToken.ThrowIfCancellationRequested();


		// =========================================================
		// 1. Validation Evidence
		// =========================================================

		var validation =
			validationResult.ValidationResult;

		var validationErrorCount =
			validation?.ErrorItems.Count()
			?? 0;

		var validationWarningCount =
			validation?.WarningItems.Count()
			?? 0;

		var validationScore =
			CalculateValidationScore(
				validation);


		// =========================================================
		// 2. Repair Evidence
		// =========================================================

		var trace =
			repairTrace
			?? validationResult.RepairTrace
			?? new QueryPlanRepairTrace
			{
				Status =
					QueryPlanRepairTraceStatus.NotRequired
			};


		var repairCount =
			trace.TotalAttempts;

		var changedPlanCount =
			trace.ChangedPlanCount;

		var repairStalled =
			trace.Status ==
			QueryPlanRepairTraceStatus.Stalled;

		var repairLoopDetected =
			trace.Status ==
			QueryPlanRepairTraceStatus.LoopDetected;

		var repairFailed =
			trace.Status ==
				QueryPlanRepairTraceStatus.Failed;

		var maxRepairAttemptsReached =
			trace.Status ==
			QueryPlanRepairTraceStatus.MaxAttemptsReached;


		// =========================================================
		// 3. Semantic Search Evidence
		// =========================================================

		cancellationToken.ThrowIfCancellationRequested();


		var semanticResults =
			await _metadataSemanticSearchService
				.SearchAsync(
					question,
					10);


		cancellationToken.ThrowIfCancellationRequested();


		semanticResults ??=
			new List<MetadataSemanticSearchResult>();


		var evidence =
			BuildEvidence(
				plan,
				validation,
				trace,
				semanticResults);


		// =========================================================
		// 4. Override calculated counters with actual pipeline data
		// =========================================================

		evidence.ValidationErrorCount =
			validationErrorCount;

		evidence.ValidationWarningCount =
			validationWarningCount;

		evidence.ValidationScore =
			validationScore;

		evidence.RepairCount =
			repairCount;

		evidence.ChangedPlanCount =
			changedPlanCount;

		evidence.RepairStalled =
			repairStalled;

		evidence.RepairLoopDetected =
			repairLoopDetected;

		evidence.RepairFailed =
			repairFailed;

		evidence.MaxRepairAttemptsReached =
			maxRepairAttemptsReached;

		evidence.RepairStatus =
			trace.Status;


		// =========================================================
		// 5. Confidence Score
		// =========================================================

		var score =
			CalculateConfidenceScore(
				evidence);


		// =========================================================
		// 6. Confidence Level
		// =========================================================

		var level =
			DetermineConfidenceLevel(
				score,
				evidence);


		// =========================================================
		// 7. Explainability
		// =========================================================

		var reasons =
			BuildReasons(
				evidence,
				score);


		var blockingReasons =
			BuildBlockingReasons(
				evidence);


		// =========================================================
		// 8. Basic CanProceed
		// =========================================================
		//
		// 注意：
		//
		// CanProceed 不等于最终 Decision。
		//
		// 最终 Decision 必须由：
		//
		// IQueryPlanDecisionGate
		//
		// 决定。
		//

		var canProceed =
			score >= HighConfidenceThreshold
			&&
			validationErrorCount == 0
			&&
			!repairStalled
			&&
			!repairLoopDetected
			&&
			!repairFailed
			&&
			!maxRepairAttemptsReached;


		return new QueryPlanConfidence
		{
			Score =
				score,

			Level =
				level,

			CanProceed =
				canProceed,

			Evidence =
				evidence,

			Reasons =
				reasons,

			BlockingReasons =
				blockingReasons
		};
	}


	// =============================================================
	// Evidence Builder
	// =============================================================

	/// <summary>
	/// 根据当前 QueryPlan 和真实 Metadata Semantic Search
	/// 生成 Confidence Evidence。
	/// </summary>
	private static QueryPlanConfidenceEvidence BuildEvidence(
		QueryPlan plan,
		QuerySemanticValidationResult? validation,
		QueryPlanRepairTrace trace,
		IReadOnlyList<MetadataSemanticSearchResult>
			semanticResults)
	{
		var evidence =
			new QueryPlanConfidenceEvidence();


		// ---------------------------------------------------------
		// Candidate Ranking
		// ---------------------------------------------------------

		var orderedResults =
			semanticResults
				.OrderByDescending(
					x => x.Score)
				.ToList();


		if (orderedResults.Count > 0)
		{
			evidence.CandidateRankingScore =
				ClampScore(
					orderedResults[0].Score);

			evidence.SemanticEvidenceAvailable =
				true;
		}


		if (orderedResults.Count > 1)
		{
			evidence.CandidateRankingGap =
				ClampScore(
					orderedResults[0].Score
					-
					orderedResults[1].Score);
		}


		// ---------------------------------------------------------
		// Semantic Match
		// ---------------------------------------------------------

		var semanticVectorResults =
			semanticResults
				.Where(
					x => x.IsSemanticVector)
				.ToList();


		if (semanticVectorResults.Count > 0)
		{
			evidence.SemanticMatchScore =
				ClampScore(
					semanticVectorResults
						.Max(x => x.Score));

			evidence.SemanticEvidenceAvailable =
				true;
		}


		// ---------------------------------------------------------
		// Table Match
		// ---------------------------------------------------------

		var planTableIds =
			plan.Tables
				.Select(
					x => x.MetadataTableId)
				.Where(
					x => x > 0)
				.ToHashSet();


		var matchingTableResults =
			semanticResults
				.Where(
					x =>
						x.Table != null
						&&
						planTableIds.Contains(
							x.Table.Id))
				.ToList();


		if (matchingTableResults.Count > 0)
		{
			evidence.TableMatchScore =
				ClampScore(
					matchingTableResults
						.Max(x => x.Score));

			evidence.TableEvidenceAvailable =
				true;
		}


		// ---------------------------------------------------------
		// Field Match
		// ---------------------------------------------------------

		var planFieldIds =
			plan.Fields
				.Select(
					x => x.MetadataColumnId)
				.Where(
					x => x > 0)
				.ToHashSet();


		var planDimensionIds =
			plan.Dimensions
				.Select(
					x => x.MetadataColumnId)
				.Where(
					x => x > 0)
				.ToHashSet();


		var allPlanColumnIds =
			planFieldIds
				.Concat(planDimensionIds)
				.ToHashSet();


		var matchingFieldResults =
			semanticResults
				.Where(
					x =>
						x.Column != null
						&&
						allPlanColumnIds.Contains(
							x.Column.Id))
				.ToList();


		if (matchingFieldResults.Count > 0)
		{
			evidence.FieldMatchScore =
				ClampScore(
					matchingFieldResults
						.Max(x => x.Score));

			evidence.FieldEvidenceAvailable =
				true;
		}


		// ---------------------------------------------------------
		// Metric Match
		// ---------------------------------------------------------

		var metricFieldNames =
			plan.Metrics
				.Select(
					x => x.Field)
				.Where(
					x => !string.IsNullOrWhiteSpace(x))
				.ToHashSet(
					StringComparer.OrdinalIgnoreCase);


		var metricResults =
			semanticResults
				.Where(
					x =>
						x.Column != null
						&&
						!string.IsNullOrWhiteSpace(
							x.Column.ColumnName)
						&&
						metricFieldNames.Contains(
							x.Column.ColumnName))
				.ToList();


		if (metricResults.Count > 0)
		{
			evidence.MetricMatchScore =
				ClampScore(
					metricResults
						.Max(x => x.Score));

			evidence.MetricEvidenceAvailable =
				true;
		}


		// ---------------------------------------------------------
		// Dimension Match
		// ---------------------------------------------------------

		var dimensionResults =
			semanticResults
				.Where(
					x =>
						x.Column != null
						&&
						planDimensionIds.Contains(
							x.Column.Id))
				.ToList();


		if (dimensionResults.Count > 0)
		{
			evidence.DimensionMatchScore =
				ClampScore(
					dimensionResults
						.Max(x => x.Score));

			evidence.DimensionEvidenceAvailable =
				true;
		}


		// ---------------------------------------------------------
		// Filter Match
		// ---------------------------------------------------------

		var filterFieldNames =
			plan.Filters
				.Select(
					x => x.Field)
				.Where(
					x => !string.IsNullOrWhiteSpace(x))
				.ToHashSet(
					StringComparer.OrdinalIgnoreCase);


		var filterResults =
			semanticResults
				.Where(
					x =>
						x.Column != null
						&&
						!string.IsNullOrWhiteSpace(
							x.Column.ColumnName)
						&&
						filterFieldNames.Contains(
							x.Column.ColumnName))
				.ToList();


		if (filterResults.Count > 0)
		{
			evidence.FilterMatchScore =
				ClampScore(
					filterResults
						.Max(x => x.Score));

			evidence.FilterEvidenceAvailable =
				true;
		}


		// ---------------------------------------------------------
		// Validation
		// ---------------------------------------------------------

		evidence.ValidationScore =
			CalculateValidationScore(
				validation);


		evidence.ValidationErrorCount =
			validation?.ErrorItems.Count()
			?? 0;


		evidence.ValidationWarningCount =
			validation?.WarningItems.Count()
			?? 0;


		// ---------------------------------------------------------
		// Repair
		// ---------------------------------------------------------

		evidence.RepairCount =
			trace.TotalAttempts;

		evidence.ChangedPlanCount =
			trace.ChangedPlanCount;

		evidence.RepairStalled =
			trace.Status ==
			QueryPlanRepairTraceStatus.Stalled;

		evidence.RepairLoopDetected =
			trace.Status ==
			QueryPlanRepairTraceStatus.LoopDetected;

		evidence.RepairFailed =
			trace.Status ==
			QueryPlanRepairTraceStatus.Failed;

		evidence.MaxRepairAttemptsReached =
			trace.Status ==
			QueryPlanRepairTraceStatus.MaxAttemptsReached;

		evidence.RepairStatus =
			trace.Status;


		return evidence;
	}


	// =============================================================
	// Validation Score
	// =============================================================

	/// <summary>
	/// 计算 Validation Score。
	///
	/// 规则：
	///
	/// Error = 0
	/// Warning = 轻微扣分
	/// PASS = 1
	/// </summary>
	private static double CalculateValidationScore(
		QuerySemanticValidationResult? validation)
	{
		if (validation == null)
		{
			return 0;
		}


		var errorCount =
			validation.ErrorItems.Count();


		if (errorCount > 0)
		{
			return 0;
		}


		var warningCount =
			validation.WarningItems.Count();


		if (warningCount == 0)
		{
			return 1;
		}


		// 每个 Warning 扣 0.05，
		// 最大扣除 0.20。
		var penalty =
			Math.Min(
				0.20,
				warningCount * 0.05);


		return ClampScore(
			1 - penalty);
	}


	// =============================================================
	// Confidence Score
	// =============================================================

	/// <summary>
	/// 根据当前可用 Evidence 计算 Confidence。
	///
	/// 注意：
	///
	/// Evidence 不可用时不直接当成 0。
	///
	/// 只对“真实存在的 Evidence”进行加权归一化。
	///
	/// 但是：
	///
	/// 如果核心 Semantic Evidence 完全不存在，
	/// 则最终 Confidence 不允许进入 High。
	/// </summary>
	private static double CalculateConfidenceScore(
		QueryPlanConfidenceEvidence evidence)
	{
		var weightedScore = 0.0;

		var availableWeight = 0.0;


		// ---------------------------------------------------------
		// Semantic
		// ---------------------------------------------------------

		if (evidence.SemanticEvidenceAvailable)
		{
			weightedScore +=
				evidence.SemanticMatchScore
				*
				SemanticWeight;

			availableWeight +=
				SemanticWeight;
		}


		// ---------------------------------------------------------
		// Table
		// ---------------------------------------------------------

		if (evidence.TableEvidenceAvailable)
		{
			weightedScore +=
				evidence.TableMatchScore
				*
				TableWeight;

			availableWeight +=
				TableWeight;
		}


		// ---------------------------------------------------------
		// Field
		// ---------------------------------------------------------

		if (evidence.FieldEvidenceAvailable)
		{
			weightedScore +=
				evidence.FieldMatchScore
				*
				FieldWeight;

			availableWeight +=
				FieldWeight;
		}


		// ---------------------------------------------------------
		// Metric
		// ---------------------------------------------------------

		if (evidence.MetricEvidenceAvailable)
		{
			weightedScore +=
				evidence.MetricMatchScore
				*
				MetricWeight;

			availableWeight +=
				MetricWeight;
		}


		// ---------------------------------------------------------
		// Dimension
		// ---------------------------------------------------------

		if (evidence.DimensionEvidenceAvailable)
		{
			weightedScore +=
				evidence.DimensionMatchScore
				*
				DimensionWeight;

			availableWeight +=
				DimensionWeight;
		}


		// ---------------------------------------------------------
		// Filter
		// ---------------------------------------------------------

		if (evidence.FilterEvidenceAvailable)
		{
			weightedScore +=
				evidence.FilterMatchScore
				*
				FilterWeight;

			availableWeight +=
				FilterWeight;
		}


		// ---------------------------------------------------------
		// Validation
		// ---------------------------------------------------------

		weightedScore +=
			evidence.ValidationScore
			*
			ValidationWeight;

		availableWeight +=
			ValidationWeight;


		// ---------------------------------------------------------
		// Repair Stability
		// ---------------------------------------------------------

		var repairStabilityScore =
			CalculateRepairStabilityScore(
				evidence);


		weightedScore +=
			repairStabilityScore
			*
			RepairStabilityWeight;

		availableWeight +=
			RepairStabilityWeight;


		if (availableWeight <= 0)
		{
			return 0;
		}


		var normalizedScore =
			weightedScore
			/
			availableWeight;


		return ClampScore(
			normalizedScore);
	}


	// =============================================================
	// Repair Stability
	// =============================================================

	/// <summary>
	/// 计算 Repair Stability。
	///
	/// NotRequired / Repaired：
	///     高稳定性
	///
	/// Stalled / Failed / Loop / MaxAttempts：
	///     低稳定性
	/// </summary>
	private static double CalculateRepairStabilityScore(
		QueryPlanConfidenceEvidence evidence)
	{
		if (evidence.RepairLoopDetected)
		{
			return 0;
		}


		if (evidence.RepairFailed)
		{
			return 0;
		}


		if (evidence.RepairStalled)
		{
			return 0;
		}


		if (evidence.MaxRepairAttemptsReached)
		{
			return 0.10;
		}


		if (evidence.RepairStatus ==
			QueryPlanRepairTraceStatus.Repaired)
		{
			return 0.90;
		}


		if (evidence.RepairStatus ==
			QueryPlanRepairTraceStatus.NotRequired)
		{
			return 1.00;
		}


		return 0.50;
	}


	// =============================================================
	// Confidence Level
	// =============================================================

	/// <summary>
	/// 根据 Score 和安全条件确定 Confidence Level。
	/// </summary>
	private static QueryPlanConfidenceLevel
		DetermineConfidenceLevel(
			double score,
			QueryPlanConfidenceEvidence evidence)
	{
		// ---------------------------------------------------------
		// Hard Safety Block
		// ---------------------------------------------------------

		if (evidence.ValidationErrorCount > 0
			||
			evidence.RepairStalled
			||
			evidence.RepairLoopDetected
			||
			evidence.RepairFailed
			||
			evidence.MaxRepairAttemptsReached)
		{
			return QueryPlanConfidenceLevel.Low;
		}


		// ---------------------------------------------------------
		// Semantic Evidence 不存在时：
		//
		// 不允许 High。
		// ---------------------------------------------------------

		if (!evidence.SemanticEvidenceAvailable)
		{
			if (score >= MediumConfidenceThreshold)
			{
				return QueryPlanConfidenceLevel.Medium;
			}


			return QueryPlanConfidenceLevel.Low;
		}


		// ---------------------------------------------------------
		// High
		// ---------------------------------------------------------

		if (score >= HighConfidenceThreshold)
		{
			return QueryPlanConfidenceLevel.High;
		}


		// ---------------------------------------------------------
		// Medium
		// ---------------------------------------------------------

		if (score >= MediumConfidenceThreshold)
		{
			return QueryPlanConfidenceLevel.Medium;
		}


		// ---------------------------------------------------------
		// Low
		// ---------------------------------------------------------

		return QueryPlanConfidenceLevel.Low;
	}


	// =============================================================
	// Reasons
	// =============================================================

	/// <summary>
	/// 构建 Confidence 解释原因。
	/// </summary>
	private static List<string> BuildReasons(
		QueryPlanConfidenceEvidence evidence,
		double score)
	{
		var reasons =
			new List<string>();


		reasons.Add(
			$"QueryPlan Confidence Score = {score:F3}.");


		if (evidence.SemanticEvidenceAvailable)
		{
			reasons.Add(
				$"Semantic Match Score = {evidence.SemanticMatchScore:F3}.");
		}
		else
		{
			reasons.Add(
				"Semantic Match Evidence 不可用，当前 Confidence 不允许达到 High。");
		}


		if (evidence.TableEvidenceAvailable)
		{
			reasons.Add(
				$"Table Match Score = {evidence.TableMatchScore:F3}.");
		}


		if (evidence.FieldEvidenceAvailable)
		{
			reasons.Add(
				$"Field Match Score = {evidence.FieldMatchScore:F3}.");
		}


		if (evidence.MetricEvidenceAvailable)
		{
			reasons.Add(
				$"Metric Match Score = {evidence.MetricMatchScore:F3}.");
		}


		if (evidence.DimensionEvidenceAvailable)
		{
			reasons.Add(
				$"Dimension Match Score = {evidence.DimensionMatchScore:F3}.");
		}


		if (evidence.FilterEvidenceAvailable)
		{
			reasons.Add(
				$"Filter Match Score = {evidence.FilterMatchScore:F3}.");
		}


		reasons.Add(
			$"Validation Score = {evidence.ValidationScore:F3}.");


		reasons.Add(
			$"Repair Status = {evidence.RepairStatus}.");


		reasons.Add(
			$"Repair Count = {evidence.RepairCount}.");


		reasons.Add(
			$"Changed Plan Count = {evidence.ChangedPlanCount}.");


		if (evidence.CandidateRankingScore > 0)
		{
			reasons.Add(
				$"Top Candidate Ranking Score = {evidence.CandidateRankingScore:F3}.");
		}


		if (evidence.CandidateRankingGap > 0)
		{
			reasons.Add(
				$"Candidate Ranking Gap = {evidence.CandidateRankingGap:F3}.");
		}


		return reasons;
	}


	// =============================================================
	// Blocking Reasons
	// =============================================================

	/// <summary>
	/// 构建阻断原因。
	/// </summary>
	private static List<string> BuildBlockingReasons(
		QueryPlanConfidenceEvidence evidence)
	{
		var reasons =
			new List<string>();


		if (evidence.ValidationErrorCount > 0)
		{
			reasons.Add(
				$"存在 {evidence.ValidationErrorCount} 个 Validation Error。");
		}


		if (evidence.RepairStalled)
		{
			reasons.Add(
				"Repair Pipeline 发生 Stall。");
		}


		if (evidence.RepairLoopDetected)
		{
			reasons.Add(
				"Repair Pipeline 检测到 QueryPlan Loop。");
		}


		if (evidence.RepairFailed)
		{
			reasons.Add(
				"Repair Pipeline 执行失败。");
		}


		if (evidence.MaxRepairAttemptsReached)
		{
			reasons.Add(
				"Repair Pipeline 达到最大自动修复次数。");
		}


		if (!evidence.SemanticEvidenceAvailable)
		{
			reasons.Add(
				"当前没有可用的 Metadata Semantic Match Evidence。");
		}


		return reasons;
	}


	// =============================================================
	// Score Utility
	// =============================================================

	/// <summary>
	/// 将 Score 限制在 0~1。
	/// </summary>
	private static double ClampScore(
		double score)
	{
		if (double.IsNaN(score)
			||
			double.IsInfinity(score))
		{
			return 0;
		}


		return Math.Clamp(
			score,
			0.0,
			1.0);
	}
}