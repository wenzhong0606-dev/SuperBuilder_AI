using SuperBulider_AI.Interfaces.BI;
using SuperBulider_AI.Models.BI;

namespace SuperBulider_AI.Services.BI;

/// <summary>
/// QueryPlan 验证与自动修复流水线。
///
/// Phase 2.3.1
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
/// Re-Validation
///      ↓
/// ┌───────────────┐
/// │               │
/// PASS            FAIL
/// │               │
/// ↓               ↓
/// Return      Retry Repair
///
/// 注意：
///
/// Repair 直接修改当前 QueryPlan。
///
/// 不再：
///
/// Validation
///      ↓
/// Repair QueryIntent
///      ↓
/// QueryPlanBuilder
///      ↓
/// Rebuild QueryPlan
///
/// QueryPlanBuilder 只负责第一次生成 QueryPlan，
/// 不参与 QueryPlan Repair Loop。
/// </summary>
public class QueryPlanValidationPipeline :
	IQueryPlanValidationPipeline
{
	private readonly QuerySemanticValidator _validator;

	private readonly IQueryPlanRepairService _repairService;


	/// <summary>
	/// 最大自动修复次数。
	///
	/// 防止：
	///
	/// Validation
	///      ↓
	/// Repair
	///      ↓
	/// Validation
	///      ↓
	/// Repair
	///      ↓
	/// 无限循环
	/// </summary>
	private const int MaxRepairAttempts = 3;


	/// <summary>
	/// 构造函数。
	/// </summary>
	public QueryPlanValidationPipeline(
		QuerySemanticValidator validator,
		IQueryPlanRepairService repairService)
	{
		_validator = validator;

		_repairService = repairService;
	}


	/// <summary>
	/// 执行 QueryPlan Validation + Repair Loop。
	///
	/// 流程：
	///
	/// 1. 第一次 Validation
	///
	/// 2. 如果通过
	///    → 直接返回
	///
	/// 3. 如果失败
	///    → 构造 QueryPlanRepairRequest
	///
	/// 4. QueryPlanRepairService
	///    → 直接修改 QueryPlan
	///
	/// 5. Re-Validation
	///
	/// 6. 如果仍失败
	///    → 继续 Repair
	///
	/// 7. 达到 MaxRepairAttempts
	///    → 返回最终失败结果
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


		QuerySemanticValidationResult
			currentValidationResult =
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
		// QueryPlan Repair Loop
		//
		for (
			var attempt = 1;
			attempt <= MaxRepairAttempts;
			attempt++)
		{
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
			// 执行 QueryPlan Repair
			//
			var repairResult =
				await _repairService
					.RepairAsync(
						repairRequest);


			//
			// Repair 没有成功
			//
			if (!repairResult.Success
				||
				repairResult.RepairedPlan == null)
			{
				//
				// 当前 Plan 没有可继续修复的方案。
				//
				return new QueryPlanValidationPipelineResult
				{
					Plan = currentPlan,

					ValidationResult =
						currentValidationResult
				};
			}


			//
			// 使用 Repair 后的 QueryPlan
			//
			currentPlan =
				repairResult.RepairedPlan;


			//
			// 重新 Validation
			//
			currentValidationResult =
				_validator.Validate(
					currentPlan,
					context);


			//
			// Repair 后验证通过
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
			// 如果还失败：
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
		// 达到最大 Repair 次数。
		//
		// 返回最后一次 Repair 后的 Plan
		// 以及最终 ValidationResult。
		//
		return new QueryPlanValidationPipelineResult
		{
			Plan = currentPlan,

			ValidationResult =
				currentValidationResult
		};
	}
}