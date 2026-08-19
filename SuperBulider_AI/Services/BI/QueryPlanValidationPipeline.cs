using System.Security.Cryptography;
using System.Text;
using SuperBulider_AI.Interfaces.BI;
using SuperBulider_AI.Models.BI;

namespace SuperBulider_AI.Services.BI;

/// <summary>
/// QueryPlan 验证与自动修复流水线。
///
/// Phase 2.3.3
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
/// 本阶段增加：
///
/// 1. QueryPlan Progress Detection
/// 2. Repair History
/// 3. Stall Detection
/// 4. Duplicate Plan Detection
/// 5. Validation Fingerprint Detection
/// 6. Repair Attempt Control
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
	/// 如果连续出现相同 Plan Fingerprint，
	/// 说明 Repair 没有产生新的 Plan。
	///
	/// 此时立即停止，避免无意义 Retry。
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
	/// Phase 2.3.3：
	///
	/// 1. Initial Validation
	/// 2. Repair
	/// 3. Detect Plan Progress
	/// 4. Re-Validation
	/// 5. Detect Validation Progress
	/// 6. Continue / Stall / Pass
	/// 7. Max Retry Control
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


		var currentPlan = plan;


		//
		// Repair History
		//
		// 当前阶段暂存在 Pipeline 内部。
		//
		// 后续 Phase 可以将其提升为：
		//
		// QueryPlanRepairHistory
		//
		var repairHistory =
			new List<RepairHistoryEntry>();


		//
		// 用于检测：
		//
		// Plan A
		//   ↓
		// Plan B
		//   ↓
		// Plan B
		//
		// 第二次出现相同 Fingerprint
		// 即认为进入 Stall。
		//
		var planFingerprintHistory =
			new Dictionary<string, int>(
				StringComparer.Ordinal);


		//
		// Initial Validation
		//
		var currentValidationResult =
			_validator.Validate(
				currentPlan,
				context);


		//
		// 第一次 Validation 已经通过
		//
		if (currentValidationResult.IsValid)
		{
			return new QueryPlanValidationPipelineResult
			{
				Plan = currentPlan,

				ValidationResult =
					currentValidationResult
			};
		}


		//
		// 记录初始 Plan Fingerprint
		//
		var initialFingerprint =
			CreatePlanFingerprint(
				currentPlan);

		planFingerprintHistory[
			initialFingerprint] = 1;


		//
		// QueryPlan Repair Loop
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
			// 构造 Repair Request
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
			// 执行 Repair
			//
			var repairResult =
				await _repairService
					.RepairAsync(
						repairRequest);


			//
			// Repair History
			//
			var repairEntry =
				new RepairHistoryEntry
				{
					Attempt =
						attempt,

					ValidationFingerprint =
						validationFingerprint,

					RepairSuccess =
						repairResult.Success,

					RepairActions =
						repairResult.RepairActions
							.ToList(),

					FailureReason =
						repairResult.FailureReason
				};


			repairHistory.Add(
				repairEntry);


			//
			// Repair 没有成功
			//
			if (!repairResult.Success
				||
				repairResult.RepairedPlan == null)
			{
				//
				// Stall：
				//
				// 当前 Validation 失败
				// 且 Repair 无法产生新的 Plan。
				//
				return new QueryPlanValidationPipelineResult
				{
					Plan = currentPlan,

					ValidationResult =
						currentValidationResult
				};
			}


			//
			// Repair 后 Plan
			//
			var repairedPlan =
				repairResult.RepairedPlan;


			//
			// Progress Detection
			//
			var beforeFingerprint =
				CreatePlanFingerprint(
					currentPlan);

			var afterFingerprint =
				CreatePlanFingerprint(
					repairedPlan);


			//
			// 判断 Plan 是否真正发生变化
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
			// Repair 返回成功，
			// 但是 Plan 完全没有变化。
			//
			if (!planChanged)
			{
				repairEntry.StallReason =
					"Repair reported success but QueryPlan fingerprint did not change.";

				return new QueryPlanValidationPipelineResult
				{
					Plan = currentPlan,

					ValidationResult =
						currentValidationResult
				};
			}


			//
			// =====================================================
			// Duplicate Plan Detection
			// =====================================================
			//
			// 即使当前 Plan 与上一轮不同，
			// 也可能回到了之前出现过的 Plan。
			//
			// 例如：
			//
			// A → B → A
			//
			// 这种情况说明 Repair Loop 出现循环。
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
					return new QueryPlanValidationPipelineResult
					{
						Plan = repairedPlan,

						ValidationResult =
							currentValidationResult
					};
				}
			}
			else
			{
				planFingerprintHistory[
					afterFingerprint] = 1;
			}


			//
			// 使用 Repair 后的 QueryPlan
			//
			currentPlan =
				repairedPlan;


			//
			// Re-Validation
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

				return new QueryPlanValidationPipelineResult
				{
					Plan = currentPlan,

					ValidationResult =
						currentValidationResult
				};
			}


			//
			// =====================================================
			// Validation Progress Detection
			// =====================================================
			//
			var currentErrorCount =
				currentValidationResult.ErrorItems.Count();

		


			//
			// 当前阶段不单纯依赖 ErrorCount 判断进展。
			//
			// 真正可靠的判断依据是：
			//
			// 1. Plan Fingerprint 是否变化
			// 2. Validation Fingerprint 是否变化
			//
			var newValidationFingerprint =
				CreateValidationFingerprint(
					currentValidationResult);


			repairEntry.AfterValidationFingerprint =
				newValidationFingerprint;


			//
			// 如果 Validation Fingerprint 与本轮之前完全一致，
			// 说明虽然 Plan 发生了变化，
			// 但 Validation 状态没有发生任何变化。
			//
			if (string.Equals(
					validationFingerprint,
					newValidationFingerprint,
					StringComparison.Ordinal))
			{
				repairEntry.StallReason =
					"QueryPlan changed, but ValidationResult remained unchanged.";

				return new QueryPlanValidationPipelineResult
				{
					Plan = currentPlan,

					ValidationResult =
						currentValidationResult
				};
			}


			//
			// 如果错误数量没有下降，
			// 不立即停止。
			//
			// 原因：
			//
			// 一个 Repair 可能：
			//
			// Error A → Error B
			//
			// 数量相同，
			// 但实际上已经产生语义进展。
			//
			// 所以只记录，不作为 Stall 的唯一依据。
			//
			repairEntry.ValidationErrorCount =
				currentErrorCount;


			//
			// 如果还有错误：
			//
			// 下一轮继续：
			//
			// Validation
			//      ↓
			// Repair
			//      ↓
			// Validation
			//
		}


		//
		// =========================================================
		// Max Repair Attempts
		// =========================================================
		//
		// 达到最大自动修复次数。
		//
		// 返回最后一次 Repair 后的 Plan
		// 和最终 ValidationResult。
		//
		return new QueryPlanValidationPipelineResult
		{
			Plan = currentPlan,

			ValidationResult =
				currentValidationResult
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
		// Intent
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
		// Tables
		//
		foreach (
			var table in plan.Tables
				.OrderBy(
					x => x.TableName,
					StringComparer.OrdinalIgnoreCase))
		{
			Append(
				builder,
				table.TableName);

			Append(
				builder,
				table.TableComment);
		}


		//
		// Metrics
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
		// Dimensions
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
		// Fields
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
		// Filters
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
	/// Plan 虽然发生变化，
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
	// Hash
	// =============================================================

	/// <summary>
	/// SHA256 Fingerprint。
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
	// Helpers
	// =============================================================

	private static void Append(
		StringBuilder builder,
		string? value)
	{
		builder
			.Append(
				value ?? string.Empty)
			.Append('\u001F');
	}


	// =============================================================
	// Internal Repair History
	// =============================================================

	/// <summary>
	/// Pipeline 内部 Repair History。
	///
	/// 当前阶段不直接暴露到
	/// QueryPlanValidationPipelineResult。
	///
	/// 用于：
	///
	/// - Progress Detection
	/// - Stall Detection
	/// - Loop Detection
	/// - Debugging
	///
	/// 后续 Phase 可以提升为正式 Domain Model。
	/// </summary>
	private sealed class RepairHistoryEntry
	{
		public int Attempt
		{
			get;
			init;
		}

		public string ValidationFingerprint
		{
			get;
			init;
		} = string.Empty;

		public string BeforePlanFingerprint
		{
			get;
			set;
		} = string.Empty;

		public string AfterPlanFingerprint
		{
			get;
			set;
		} = string.Empty;

		public string AfterValidationFingerprint
		{
			get;
			set;
		} = string.Empty;

		public bool RepairSuccess
		{
			get;
			init;
		}

		public bool PlanChanged
		{
			get;
			set;
		}

		public bool ValidationPassed
		{
			get;
			set;
		}

		public int ValidationErrorCount
		{
			get;
			set;
		}

		public List<string> RepairActions
		{
			get;
			init;
		} = new();

		public string? FailureReason
		{
			get;
			init;
		}

		public string? StallReason
		{
			get;
			set;
		}
	}
}