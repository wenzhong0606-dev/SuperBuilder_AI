using SuperBulider_AI.Interfaces.BI;
using SuperBulider_AI.Models.BI;


namespace SuperBulider_AI.Services.BI;

/// <summary>
/// QueryPlan验证流水线。
///
/// Phase 2.2.5
///
/// 职责:
///
/// QueryPlan
///      ↓
/// Semantic Validation
///      ↓
/// Repair QueryIntent
///      ↓
/// Rebuild QueryPlan
///      ↓
/// ReValidate
///
/// 注意:
///
/// Repair 不直接修改 QueryPlan。
///
/// AI 修复的是 QueryIntent。
///
/// QueryPlan 永远由 QueryPlanBuilder生成。
/// </summary>
public class QueryPlanValidationPipeline :
	IQueryPlanValidationPipeline
{

	private readonly QuerySemanticValidator _validator;

	private readonly IQueryPlanRepairService _repairService;

	private readonly IQueryPlanBuilder _queryPlanBuilder;



	/// <summary>
	/// 构造函数。
	/// </summary>
	public QueryPlanValidationPipeline(
		QuerySemanticValidator validator,
		IQueryPlanRepairService repairService,
		IQueryPlanBuilder queryPlanBuilder)
	{
		_validator = validator;

		_repairService = repairService;

		_queryPlanBuilder = queryPlanBuilder;
	}



	/// <summary>
	/// 执行 QueryPlan 验证。
	///
	/// 如果验证失败:
	///
	/// 1. AI Repair QueryIntent
	/// 2. 重新生成 QueryPlan
	/// 3. 再次验证
	///
	/// </summary>
	public async Task<QuerySemanticValidationResult> ValidateAsync(
		QueryPlan plan,
		QueryPlanValidationContext context,
		string question)
	{
		ArgumentNullException.ThrowIfNull(plan);

		ArgumentNullException.ThrowIfNull(context);



		//
		// 第一次验证
		//
		var result =
			_validator.Validate(
				plan,
				context);



		//
		// 验证通过
		//
		if (result.IsValid)
		{
			return result;
		}



		//
		// Phase 2.2.5
		//
		// Repair QueryIntent
		//
		var repairedIntent =
			await _repairService
				.RepairAsync(
					plan.Intent,
					result);



		//
		// 根据新的 Intent
		// 重新生成 QueryPlan
		//
		var repairedPlan =
			await _queryPlanBuilder
				.BuildAsync(
					repairedIntent);



		//
		// 第二次验证
		//
		var repairedResult =
			_validator.Validate(
				repairedPlan,
				context);



		return repairedResult;
	}
}