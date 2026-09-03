using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// QueryPlan Decision Gate。
///
/// Phase 2.4
/// QueryPlan Confidence & Decision Gate
///
/// 职责：
///
/// QueryPlanConfidence
///        ↓
/// Decision Gate
///        ↓
/// Proceed / Confirm / Reject
///
/// 本服务不负责：
///
/// - QueryPlan 构建
/// - QueryPlan Validation
/// - QueryPlan Repair
/// - Confidence 计算
/// - SQL Builder
/// - SQL Execution
///
/// 它只负责根据已经计算完成的 Confidence
/// 决定 QueryPlan 是否可以进入下一阶段。
///
/// Phase 2.4 Completion Patch：
///
/// 在原有 Decision 基础上增加：
///
/// QueryPlanDecisionTrace
///
/// 用于记录 Decision Gate 为什么做出：
///
/// Proceed
/// Confirm
/// Reject
///
/// 的完整决策证据。
/// </summary>
public sealed class QueryPlanDecisionGate
	: IQueryPlanDecisionGate
{
	/// <summary>
	/// High Confidence 最低分数。
	///
	/// 与 QueryPlanConfidenceService 保持一致。
	/// </summary>
	private const double HighConfidenceThreshold = 0.80;

	/// <summary>
	/// Medium Confidence 最低分数。
	///
	/// 与 QueryPlanConfidenceService 保持一致。
	/// </summary>
	private const double MediumConfidenceThreshold = 0.60;


	/// <summary>
	/// 根据 QueryPlan Confidence 生成最终 Decision。
	///
	/// Decision Flow：
	///
	/// Confidence
	///     ↓
	/// Hard Safety Check
	///     ↓
	/// High / Medium / Low
	///     ↓
	/// QueryPlanDecision
	///     ↓
	/// QueryPlanDecisionTrace
	/// </summary>
	public QueryPlanDecision Evaluate(
		QueryPlanConfidence confidence)
	{
		ArgumentNullException.ThrowIfNull(confidence);


		// =========================================================
		// 1. Hard Safety Block
		// =========================================================
		//
		// 即使 Score 很高，只要存在这些情况，
		// 也绝对不能自动进入 SQL Builder。
		//

		if (HasHardBlockingCondition(confidence))
		{
			return AttachTrace(
				CreateRejectDecision(
					confidence));
		}


		// =========================================================
		// 2. Confidence Level
		// =========================================================

		switch (confidence.Level)
		{
			// -----------------------------------------------------
			// High
			// -----------------------------------------------------

			case QueryPlanConfidenceLevel.High:

				return AttachTrace(
					EvaluateHighConfidence(
						confidence));


			// -----------------------------------------------------
			// Medium
			// -----------------------------------------------------

			case QueryPlanConfidenceLevel.Medium:

				return AttachTrace(
					EvaluateMediumConfidence(
						confidence));


			// -----------------------------------------------------
			// Low
			// -----------------------------------------------------

			case QueryPlanConfidenceLevel.Low:

				return AttachTrace(
					CreateRejectDecision(
						confidence));


			// -----------------------------------------------------
			// Unknown
			// -----------------------------------------------------

			default:

				return AttachTrace(
					new QueryPlanDecision
					{
						Decision =
							QueryPlanDecisionType.Reject,

						Confidence =
							confidence,

						ShouldExecute =
							false,

						RequiresConfirmation =
							false,

						Reason =
							"QueryPlan Confidence Level 无法识别，拒绝进入 SQL Builder。"
					});
		}
	}


	// =============================================================
	// High Confidence
	// =============================================================

	/// <summary>
	/// 处理 High Confidence。
	/// </summary>
	private static QueryPlanDecision
		EvaluateHighConfidence(
			QueryPlanConfidence confidence)
	{
		// ---------------------------------------------------------
		// Confidence Score 二次安全校验
		// ---------------------------------------------------------

		if (confidence.Score <
			HighConfidenceThreshold)
		{
			return new QueryPlanDecision
			{
				Decision =
					QueryPlanDecisionType.Confirm,

				Confidence =
					confidence,

				ShouldExecute =
					false,

				RequiresConfirmation =
					true,

				Reason =
					$"Confidence Level 为 High，但 Score " +
					$"({confidence.Score:F3}) 低于 High 阈值 " +
					$"({HighConfidenceThreshold:F2})，进入受控确认。"
			};
		}


		// ---------------------------------------------------------
		// CanProceed 二次安全校验
		// ---------------------------------------------------------

		if (!confidence.CanProceed)
		{
			return new QueryPlanDecision
			{
				Decision =
					QueryPlanDecisionType.Confirm,

				Confidence =
					confidence,

				ShouldExecute =
					false,

				RequiresConfirmation =
					true,

				Reason =
					"Confidence Level 为 High，但 Confidence Service " +
					"未将该 QueryPlan 标记为 CanProceed，进入受控确认。"
			};
		}


		// ---------------------------------------------------------
		// High + Safe
		// ---------------------------------------------------------

		return new QueryPlanDecision
		{
			Decision =
				QueryPlanDecisionType.Proceed,

			Confidence =
				confidence,

			ShouldExecute =
				true,

			RequiresConfirmation =
				false,

			Reason =
				$"QueryPlan Confidence 为 High，" +
				$"Score = {confidence.Score:F3}，" +
				"允许进入 SQL Builder。"
		};
	}


	// =============================================================
	// Medium Confidence
	// =============================================================

	/// <summary>
	/// 处理 Medium Confidence。
	///
	/// Medium 永远不能自动进入 SQL Builder。
	/// </summary>
	private static QueryPlanDecision
		EvaluateMediumConfidence(
			QueryPlanConfidence confidence)
	{
		if (confidence.Score <
			MediumConfidenceThreshold)
		{
			return CreateRejectDecision(
				confidence);
		}


		// ---------------------------------------------------------
		// 合法明细列表：Medium 亦可进入 SQL Builder
		// ---------------------------------------------------------
		//
		// 明细列表（目标实体已解析、含 Limit/Order、无指标/维度）
		// 语义明确，不需要指标/维度确认。即使置信度为 Medium，
		// 也应直接进入 SQL Builder，避免「列出最近十张入库单」这类
		// 请求被无限期卡在 Confirmation。
		//
		// 安全前提：Hard Blocking 已在 Evaluate() 入口统一拦截
		// （校验错误 / Repair 异常 / BlockingReasons 均会先 Reject）。

		if (confidence.IsExecutableDetailQuery)
		{
			return new QueryPlanDecision
			{
				Decision =
					QueryPlanDecisionType.Proceed,

				Confidence =
					confidence,

				ShouldExecute =
					true,

				RequiresConfirmation =
					false,

				Reason =
					$"QueryPlan 为合法明细列表（目标实体已解析、含 Limit/Order、无指标/维度），" +
					$"Score = {confidence.Score:F3}，" +
					"允许直接进入 SQL Builder。"
			};
		}


		return new QueryPlanDecision
		{
			Decision =
				QueryPlanDecisionType.Confirm,

			Confidence =
				confidence,

			ShouldExecute =
				false,

			RequiresConfirmation =
				true,

			Reason =
				$"QueryPlan Confidence 为 Medium，" +
				$"Score = {confidence.Score:F3}，" +
				"需要进一步确认后才能进入 SQL Builder。"
		};
	}


	// =============================================================
	// Hard Blocking
	// =============================================================

	/// <summary>
	/// 检查是否存在绝对阻断条件。
	///
	/// Hard Blocking 优先级高于 Confidence Level。
	///
	/// 即：
	///
	/// High Confidence
	/// +
	/// Hard Blocking
	/// =
	/// Reject
	/// </summary>
	private static bool HasHardBlockingCondition(
		QueryPlanConfidence confidence)
	{
		if (confidence.BlockingReasons.Count > 0)
		{
			return true;
		}


		var evidence =
			confidence.Evidence;


		if (evidence.ValidationErrorCount > 0)
		{
			return true;
		}


		if (evidence.RepairStalled)
		{
			return true;
		}


		if (evidence.RepairLoopDetected)
		{
			return true;
		}


		if (evidence.RepairFailed)
		{
			return true;
		}


		if (evidence.MaxRepairAttemptsReached)
		{
			return true;
		}


		return false;
	}


	// =============================================================
	// Reject
	// =============================================================

	/// <summary>
	/// 创建 Reject Decision。
	/// </summary>
	private static QueryPlanDecision
		CreateRejectDecision(
			QueryPlanConfidence confidence)
	{
		var reason =
			BuildRejectReason(
				confidence);


		return new QueryPlanDecision
		{
			Decision =
				QueryPlanDecisionType.Reject,

			Confidence =
				confidence,

			ShouldExecute =
				false,

			RequiresConfirmation =
				false,

			Reason =
				reason
		};
	}


	// =============================================================
	// Reject Reason
	// =============================================================

	/// <summary>
	/// 构建 Reject 原因。
	/// </summary>
	private static string BuildRejectReason(
		QueryPlanConfidence confidence)
	{
		if (confidence.BlockingReasons.Count > 0)
		{
			return
				"QueryPlan Decision Gate 拒绝执行：" +
				string.Join(
					"；",
					confidence.BlockingReasons);
		}


		if (confidence.Evidence.ValidationErrorCount > 0)
		{
			return
				"QueryPlan 存在 Validation Error，" +
				"禁止进入 SQL Builder。";
		}


		if (confidence.Evidence.RepairStalled)
		{
			return
				"QueryPlan Repair 发生 Stall，" +
				"禁止进入 SQL Builder。";
		}


		if (confidence.Evidence.RepairLoopDetected)
		{
			return
				"QueryPlan Repair 检测到 Loop，" +
				"禁止进入 SQL Builder。";
		}


		if (confidence.Evidence.RepairFailed)
		{
			return
				"QueryPlan Repair Failed，" +
				"禁止进入 SQL Builder。";
		}


		if (confidence.Evidence.MaxRepairAttemptsReached)
		{
			return
				"QueryPlan Repair 达到最大尝试次数，" +
				"禁止进入 SQL Builder。";
		}


		return
			$"QueryPlan Confidence 为 " +
			$"{confidence.Level}，" +
			$"Score = {confidence.Score:F3}，" +
			"不足以进入 SQL Builder。";
	}


	// =============================================================
	// Decision Trace
	// =============================================================

	/// <summary>
	/// 将 Decision Gate 的最终结果转换为完整的 Decision Trace。
	///
	/// 注意：
	///
	/// QueryPlanDecision
	///     = 最终决策结果
	///
	/// QueryPlanDecisionTrace
	///     = 决策依据与安全证据
	///
	/// 两者职责不同。
	/// </summary>
	private static QueryPlanDecision
		AttachTrace(
			QueryPlanDecision decision)
	{
		ArgumentNullException.ThrowIfNull(
			decision);

		ArgumentNullException.ThrowIfNull(
			decision.Confidence);


		var confidence =
			decision.Confidence;

		ArgumentNullException.ThrowIfNull(
			confidence.Evidence);


		var evidence =
			confidence.Evidence;


		decision.Trace =
			new QueryPlanDecisionTrace
			{
				// -------------------------------------------------
				// Confidence
				// -------------------------------------------------

				ConfidenceScore =
					confidence.Score,

				ConfidenceLevel =
					confidence.Level,


				// -------------------------------------------------
				// Decision
				// -------------------------------------------------

				Decision =
					decision.Decision,

				ShouldExecute =
					decision.ShouldExecute,

				RequiresConfirmation =
					decision.RequiresConfirmation,


				// -------------------------------------------------
				// Threshold
				// -------------------------------------------------

				HighThreshold =
					HighConfidenceThreshold,

				MediumThreshold =
					MediumConfidenceThreshold,


				// -------------------------------------------------
				// Validation
				// -------------------------------------------------

				ValidationErrorCount =
					evidence.ValidationErrorCount,


				// -------------------------------------------------
				// Repair
				// -------------------------------------------------

				RepairCount =
					evidence.RepairCount,

				RepairProgressScore =
					evidence.RepairProgressScore,

				RepairStalled =
					evidence.RepairStalled,

				RepairLoopDetected =
					evidence.RepairLoopDetected,

				RepairFailed =
					evidence.RepairFailed,

				MaxRepairAttemptsReached =
					evidence.MaxRepairAttemptsReached,

				RepairStatus =
					evidence.RepairStatus,


				// -------------------------------------------------
				// Decision Reason
				// -------------------------------------------------

				Reason =
					decision.Reason,


				// -------------------------------------------------
				// Blocking Reasons
				// -------------------------------------------------

				BlockingReasons =
					confidence.BlockingReasons
						.ToList(),


				// -------------------------------------------------
				// Timestamp
				// -------------------------------------------------

				EvaluatedAt =
					DateTimeOffset.UtcNow
			};


		return decision;
	}
}