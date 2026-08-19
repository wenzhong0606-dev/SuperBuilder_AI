using System.Security.Cryptography;
using System.Text;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// QueryPlan 验证与自动修复流水线。
///
/// Phase 2.3.4
///
/// 核心闭环：
///
/// QueryPlan
///      ↓
/// Semantic Validation
///      ↓
/// ValidationResult
///      ↓
/// QueryPlan Repair
///      ↓
/// Progress Detection
///      ↓
/// Re-Validation
///      ↓
/// ┌───────────────────────┐
/// │                       │
/// PASS                    FAIL
/// │                       │
/// ↓                       ↓
/// Return              Continue Repair
///
/// Phase 2.3.4 增加：
///
/// 1. QueryPlan Progress Detection
/// 2. Repair History
/// 3. Repair Trace
/// 4. Stall Detection
/// 5. Duplicate Plan Detection
/// 6. Validation Fingerprint Detection
/// 7. Repair Attempt Control
/// 8. Repair Explainability
///
/// Repair 仍然直接修改当前 QueryPlan。
///
/// QueryPlanBuilder 不参与 Repair Loop。
///
/// 不修改：
///
/// - SQL Builder
/// - Execution
/// - MetadataSemanticSearch
/// - QuerySemanticValidator
/// </summary>
public class QueryPlanValidationPipeline :
	IQueryPlanValidationPipeline
{
	private readonly QuerySemanticValidator _validator;

	private readonly IQueryPlanRepairService _repairService;


	/// <summary>
	/// 最大自动修复次数。
	/// </summary>
	private const int MaxRepairAttempts = 3;


	/// <summary>
	/// 如果 QueryPlan Fingerprint 再次出现，
	/// 说明 Repair Loop 可能进入循环。
	///
	/// 例如：
	///
	/// A → B → A
	///
	/// 此时立即停止。
	/// </summary>
	private const int MaxRepeatedPlanFingerprint = 1;


	/// <summary>
	/// 构造函数。
	/// </summary>
	public QueryPlanValidationPipeline(
		QuerySemanticValidator validator,
		IQueryPlanRepairService repairService)
	{
		ArgumentNullException.ThrowIfNull(validator);

		ArgumentNullException.ThrowIfNull(repairService);

		_validator = validator;

		_repairService = repairService;
	}


	/// <summary>
	/// 执行 QueryPlan Validation + Repair Loop。
	///
	/// Phase 2.3.4：
	///
	/// 1. Initial Validation
	/// 2. Repair
	/// 3. Detect Plan Progress
	/// 4. Detect Duplicate Plan
	/// 5. Re-Validation
	/// 6. Detect Validation Progress
	/// 7. Continue / Stall / Pass
	/// 8. Max Retry Control
	/// 9. Generate Repair Trace
	/// </summary>
	public async Task<QueryPlanValidationPipelineResult>
		ValidateAsync(
			QueryPlan plan,
			QueryPlanValidationContext context,
			string question)
	{
		ArgumentNullException.ThrowIfNull(plan);

		ArgumentNullException.ThrowIfNull(context);

		question ??= string.Empty;


		//
		// 当前正在处理的 QueryPlan
		//
		var currentPlan = plan;


		//
		// =========================================================
		// Repair Trace
		// =========================================================
		//
		// Repair Trace 不参与 Repair 决策。
		//
		// 它只负责：
		//
		// 1. Explainability
		// 2. Debugging
		// 3. Observability
		// 4. 后续 AI Feedback
		//
		var repairTrace =
			new QueryPlanRepairTrace
			{
				Status =
					QueryPlanRepairTraceStatus.NotRequired
			};


		//
		// =========================================================
		// Plan Fingerprint History
		// =========================================================
		//
		// 用于检测：
		//
		// A → B → A
		//
		// 或：
		//
		// A → B → B
		//
		var planFingerprintHistory =
			new Dictionary<string, int>(
				StringComparer.Ordinal);


		//
		// =========================================================
		// Initial Validation
		// =========================================================
		//
		var currentValidationResult =
			_validator.Validate(
				currentPlan,
				context);


		//
		// =========================================================
		// Initial Validation PASS
		// =========================================================
		//
		if (currentValidationResult.IsValid)
		{
			repairTrace.Success = true;

			repairTrace.Status =
				QueryPlanRepairTraceStatus.NotRequired;

			repairTrace.TotalAttempts = 0;

			repairTrace.ChangedPlanCount = 0;

			repairTrace.StopReason =
				"Initial QueryPlan validation passed.";

			return new QueryPlanValidationPipelineResult
			{
				Plan = currentPlan,

				ValidationResult =
					currentValidationResult,

				RepairTrace =
					repairTrace
			};
		}


		//
		// =========================================================
		// Initial Plan Fingerprint
		// =========================================================
		//
		var initialFingerprint =
			CreatePlanFingerprint(
				currentPlan);

		planFingerprintHistory[
			initialFingerprint] = 1;


		//
		// =========================================================
		// QueryPlan Repair Loop
		// =========================================================
		//
		for (
			var attempt = 1;
			attempt <= MaxRepairAttempts;
			attempt++)
		{
			//
			// 当前 Validation Fingerprint
			//
			var validationFingerprint =
				CreateValidationFingerprint(
					currentValidationResult);


			//
			// =====================================================
			// Repair Request
			// =====================================================
			//
			var repairRequest =
				new QueryPlanRepairRequest
				{
					QueryPlan =
						currentPlan,

					ValidationResult =
						currentValidationResult,

					ValidationContext =
						context,

					OriginalQuestion =
						question,

					RetryCount =
						attempt
				};


			//
			// =====================================================
			// Execute Repair
			// =====================================================
			//
			var repairResult =
				await _repairService
					.RepairAsync(
						repairRequest);


			//
			// =====================================================
			// Create Repair Trace Entry
			// =====================================================
			//
			var repairEntry =
				new QueryPlanRepairTraceEntry
				{
					Attempt =
						attempt,

					BeforeValidationFingerprint =
						validationFingerprint,

					RepairSuccess =
						repairResult.Success,

					RepairActions =
						repairResult.RepairActions
							.ToList(),

					FailureReason =
						repairResult.FailureReason,

					Explanation =
						repairResult.Explanation
				};


			repairTrace.History.Add(
				repairEntry);


			repairTrace.TotalAttempts =
				attempt;


			//
			// =====================================================
			// Repair Failed
			// =====================================================
			//
			if (!repairResult.Success
				||
				repairResult.RepairedPlan == null)
			{
				repairTrace.Success = false;

				repairTrace.Status =
					QueryPlanRepairTraceStatus.Failed;

				repairTrace.StopReason =
					repairResult.FailureReason
					??
					"QueryPlan repair failed.";

				repairTrace.ChangedPlanCount =
					repairTrace.History.Count(
						x => x.PlanChanged);


				return new QueryPlanValidationPipelineResult
				{
					Plan = currentPlan,

					ValidationResult =
						currentValidationResult,

					RepairTrace =
						repairTrace
				};
			}


			//
			// =====================================================
			// Repaired QueryPlan
			// =====================================================
			//
			var repairedPlan =
				repairResult.RepairedPlan;


			//
			// =====================================================
			// Plan Progress Detection
			// =====================================================
			//
			var beforeFingerprint =
				CreatePlanFingerprint(
					currentPlan);

			var afterFingerprint =
				CreatePlanFingerprint(
					repairedPlan);


			//
			// 判断 Repair 是否真正修改了 QueryPlan。
			//
			var planChanged =
				!string.Equals(
					beforeFingerprint,
					afterFingerprint,
					StringComparison.Ordinal);


			repairEntry.PlanChanged =
				planChanged;

			repairEntry.BeforePlanFingerprint =
				beforeFingerprint;

			repairEntry.AfterPlanFingerprint =
				afterFingerprint;


			//
			// =====================================================
			// Stall Detection #1
			// =====================================================
			//
			// RepairService 返回 Success，
			// 但是 QueryPlan 实际没有任何变化。
			//
			if (!planChanged)
			{
				repairEntry.StallReason =
					"Repair reported success but QueryPlan fingerprint did not change.";

				repairTrace.Success = false;

				repairTrace.Status =
					QueryPlanRepairTraceStatus.Stalled;

				repairTrace.StopReason =
					repairEntry.StallReason;

				repairTrace.ChangedPlanCount =
					repairTrace.History.Count(
						x => x.PlanChanged);


				return new QueryPlanValidationPipelineResult
				{
					Plan = currentPlan,

					ValidationResult =
						currentValidationResult,

					RepairTrace =
						repairTrace
				};
			}


			//
			// =====================================================
			// Duplicate Plan Detection
			// =====================================================
			//
			// 检测：
			//
			// A → B → A
			//
			// 如果回到了之前已经出现过的 QueryPlan，
			// 说明 Repair Loop 发生循环。
			//
			if (planFingerprintHistory.TryGetValue(
					afterFingerprint,
					out var occurrence))
			{
				occurrence++;

				planFingerprintHistory[
					afterFingerprint] =
					occurrence;

				repairEntry.StallReason =
					"QueryPlan fingerprint repeated; repair loop detected.";


				if (occurrence >
					MaxRepeatedPlanFingerprint)
				{
					repairTrace.Success = false;

					repairTrace.Status =
						QueryPlanRepairTraceStatus.LoopDetected;

					repairTrace.StopReason =
						repairEntry.StallReason;

					repairTrace.ChangedPlanCount =
						repairTrace.History.Count(
							x => x.PlanChanged);


					//
					// 注意：
					//
					// 返回当前检测到循环的 repairedPlan。
					//
					// 这样调用方能够看到最后一次 Repair
					// 产生的 QueryPlan。
					//
					return new QueryPlanValidationPipelineResult
					{
						Plan = repairedPlan,

						ValidationResult =
							currentValidationResult,

						RepairTrace =
							repairTrace
					};
				}
			}
			else
			{
				planFingerprintHistory[
					afterFingerprint] = 1;
			}


			//
			// =====================================================
			// 使用 Repair 后的 QueryPlan
			// =====================================================
			//
			currentPlan =
				repairedPlan;


			//
			// =====================================================
			// Re-Validation
			// =====================================================
			//
			currentValidationResult =
				_validator.Validate(
					currentPlan,
					context);


			//
			// =====================================================
			// Validation PASS
			// =====================================================
			//
			if (currentValidationResult.IsValid)
			{
				repairEntry.ValidationPassed =
					true;


				repairTrace.Success =
					true;


				repairTrace.Status =
					QueryPlanRepairTraceStatus.Repaired;


				repairTrace.ChangedPlanCount =
					repairTrace.History.Count(
						x => x.PlanChanged);


				repairTrace.StopReason =
					"QueryPlan repaired and validation passed.";


				return new QueryPlanValidationPipelineResult
				{
					Plan = currentPlan,

					ValidationResult =
						currentValidationResult,

					RepairTrace =
						repairTrace
				};
			}


			//
			// =====================================================
			// Validation Progress Detection
			// =====================================================
			//
			// 注意：
			//
			// 不能简单使用：
			//
			// ErrorCount 下降 = Progress
			//
			// 因为可能出现：
			//
			// Error A
			//     ↓
			// Error B
			//
			// 数量相同，
			// 但是语义问题已经发生变化。
			//
			var currentErrorCount =
				currentValidationResult
					.ErrorItems
					.Count();


			repairEntry.ValidationErrorCount =
				currentErrorCount;


			//
			// =====================================================
			// New Validation Fingerprint
			// =====================================================
			//
			var newValidationFingerprint =
				CreateValidationFingerprint(
					currentValidationResult);


			repairEntry.AfterValidationFingerprint =
				newValidationFingerprint;


			//
			// =====================================================
			// Stall Detection #2
			// =====================================================
			//
			// QueryPlan 发生了变化，
			// 但是 ValidationResult 完全没有变化。
			//
			// 说明当前 Repair 对 Validation
			// 没有产生任何实际效果。
			//
			if (string.Equals(
					validationFingerprint,
					newValidationFingerprint,
					StringComparison.Ordinal))
			{
				repairEntry.StallReason =
					"QueryPlan changed, but ValidationResult remained unchanged.";

				repairTrace.Success = false;

				repairTrace.Status =
					QueryPlanRepairTraceStatus.Stalled;

				repairTrace.StopReason =
					repairEntry.StallReason;

				repairTrace.ChangedPlanCount =
					repairTrace.History.Count(
						x => x.PlanChanged);


				return new QueryPlanValidationPipelineResult
				{
					Plan = currentPlan,

					ValidationResult =
						currentValidationResult,

					RepairTrace =
						repairTrace
				};
			}


			//
			// =====================================================
			// Continue Repair Loop
			// =====================================================
			//
			// 当前 Repair 已经：
			//
			// 1. 修改了 QueryPlan
			// 2. Validation 状态发生变化
			//
			// 因此说明产生了实际进展。
			//
			// 下一轮继续。
		}


		//
		// =========================================================
		// Max Repair Attempts
		// =========================================================
		//
		// 达到最大 Repair 次数，
		// 仍然没有通过 Validation。
		//
		repairTrace.Success = false;

		repairTrace.Status =
			QueryPlanRepairTraceStatus.MaxAttemptsReached;

		repairTrace.TotalAttempts =
			MaxRepairAttempts;

		repairTrace.ChangedPlanCount =
			repairTrace.History.Count(
				x => x.PlanChanged);

		repairTrace.StopReason =
			$"Maximum repair attempts reached: {MaxRepairAttempts}.";


		return new QueryPlanValidationPipelineResult
		{
			Plan = currentPlan,

			ValidationResult =
				currentValidationResult,

			RepairTrace =
				repairTrace
		};
	}


	// =============================================================
	// Plan Fingerprint
	// =============================================================

	/// <summary>
	/// 创建 QueryPlan Fingerprint。
	///
	/// Fingerprint 用于判断：
	///
	/// 1. Repair 是否真正修改了 Plan
	/// 2. 是否重复进入旧 Plan
	/// 3. 是否形成 Repair Loop
	///
	/// 注意：
	///
	/// Fingerprint 只用于检测，
	/// 不参与业务语义判断。
	/// </summary>
	private static string CreatePlanFingerprint(
		QueryPlan plan)
	{
		var builder =
			new StringBuilder();


		//
		// =========================================================
		// Intent
		// =========================================================
		//
		if (plan.Intent != null)
		{
			Append(
				builder,
				plan.Intent.OriginalQuestion);

			Append(
				builder,
				plan.Intent.IntentType);
		}


		//
		// =========================================================
		// Tables
		// =========================================================
		//
		foreach (
			var table in plan.Tables
				.OrderBy(
					x => x.TableName,
					StringComparer.OrdinalIgnoreCase))
		{
			//
			// QueryTable 当前真实字段：
			//
			// MetadataTableId
			// DataSourceId
			// TableName
			// TableComment
			//
			Append(
				builder,
				table.MetadataTableId.ToString());

			Append(
				builder,
				table.DataSourceId.ToString());

			Append(
				builder,
				table.TableName);

			Append(
				builder,
				table.TableComment);
		}


		//
		// =========================================================
		// Metrics
		// =========================================================
		//
		foreach (
			var metric in plan.Metrics
				.OrderBy(
					x => x.Name,
					StringComparer.OrdinalIgnoreCase))
		{
			Append(
				builder,
				metric.Name);

			Append(
				builder,
				metric.Field);

			Append(
				builder,
				metric.Aggregation);

			Append(
				builder,
				metric.SemanticType);
		}


		//
		// =========================================================
		// Dimensions
		// =========================================================
		//
		foreach (
			var dimension in plan.Dimensions
				.OrderBy(
					x => x.ColumnName,
					StringComparer.OrdinalIgnoreCase))
		{
			Append(
				builder,
				dimension.ColumnName);

			Append(
				builder,
				dimension.SemanticType);
		}


		//
		// =========================================================
		// Fields
		// =========================================================
		//
		foreach (
			var field in plan.Fields
				.OrderBy(
					x => x.ColumnName,
					StringComparer.OrdinalIgnoreCase))
		{
			Append(
				builder,
				field.ColumnName);

			Append(
				builder,
				field.DataType);

			Append(
				builder,
				field.Aggregation);
		}


		//
		// =========================================================
		// Filters
		// =========================================================
		//
		foreach (
			var filter in plan.Filters
				.OrderBy(
					x => x.Field,
					StringComparer.OrdinalIgnoreCase))
		{
			Append(
				builder,
				filter.Field);

			Append(
				builder,
				filter.Operator);

			Append(
				builder,
				filter.Value);
		}


		return ComputeHash(
			builder.ToString());
	}


	// =============================================================
	// Validation Fingerprint
	// =============================================================

	/// <summary>
	/// 创建 ValidationResult Fingerprint。
	///
	/// 用于判断：
	///
	/// QueryPlan 虽然发生变化，
	/// 但 Validation 状态是否仍然完全相同。
	/// </summary>
	private static string CreateValidationFingerprint(
		QuerySemanticValidationResult result)
	{
		var builder =
			new StringBuilder();


		Append(
			builder,
			result.IsValid.ToString());


		foreach (
			var error in result.ErrorItems
				.OrderBy(
					x => x.Code ?? string.Empty,
					StringComparer.OrdinalIgnoreCase)
				.ThenBy(
					x => x.Type ?? string.Empty,
					StringComparer.OrdinalIgnoreCase)
				.ThenBy(
					x => x.Field ?? string.Empty,
					StringComparer.OrdinalIgnoreCase)
				.ThenBy(
					x => x.Message ?? string.Empty,
					StringComparer.OrdinalIgnoreCase))
		{
			Append(
				builder,
				error.Code);

			Append(
				builder,
				error.Type);

			Append(
				builder,
				error.Field);

			Append(
				builder,
				error.Message);

			Append(
				builder,
				error.MetadataColumnId?.ToString());

			Append(
				builder,
				error.MetadataTableId?.ToString());
		}


		return ComputeHash(
			builder.ToString());
	}


	// =============================================================
	// SHA-256
	// =============================================================

	/// <summary>
	/// 计算 SHA-256 Fingerprint。
	/// </summary>
	private static string ComputeHash(
		string value)
	{
		var bytes =
			Encoding.UTF8.GetBytes(
				value);

		var hash =
			SHA256.HashData(
				bytes);

		return Convert.ToHexString(
			hash);
	}


	// =============================================================
	// Fingerprint Append
	// =============================================================

	/// <summary>
	/// 向 Fingerprint Builder 添加字段。
	///
	/// 使用 Unit Separator 避免字段拼接产生歧义。
	/// </summary>
	private static void Append(
		StringBuilder builder,
		string? value)
	{
		builder
			.Append(
				value ?? string.Empty)
			.Append(
				'\u001F');
	}
}