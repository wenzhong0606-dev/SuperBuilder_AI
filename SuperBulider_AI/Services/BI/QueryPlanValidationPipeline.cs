using SuperBulider_AI.Interfaces.BI;
using SuperBulider_AI.Models.AI;
using SuperBulider_AI.Models.BI;


namespace SuperBulider_AI.Services.BI;

/// <summary>
/// QueryPlan验证自动修复流水线。
///
/// Phase 2.2.5
///
/// 流程:
///
/// QueryPlan
///     ↓
/// Semantic Validate
///     ↓
/// Repair
///     ↓
/// Re Validate
///
/// 最大修复次数:
/// 3
///
/// </summary>
public class QueryPlanValidationPipeline
	:
	IQueryPlanValidationPipeline
{


	private readonly QuerySemanticValidator
		_validator;



	private readonly IQueryPlanRepairService
		_repairService;



	private const int MaxRepairCount = 3;



	public QueryPlanValidationPipeline(
		QuerySemanticValidator validator,
		IQueryPlanRepairService repairService)
	{
		_validator =
			validator;


		_repairService =
			repairService;
	}



	/// <summary>
	/// 执行QueryPlan验证与自动修复。
	/// </summary>
	public async Task<QuerySemanticValidationResult>
		ValidateAsync(
			QueryPlan plan,
			QueryPlanValidationContext context,
			string question)
	{

		QuerySemanticValidationResult result;



		for (
			int count = 0;
			count < MaxRepairCount;
			count++)
		{


			/*
             * Step 1
             *
             * Semantic Validation
             */
			result =
				_validator.Validate(
					plan,
					context);



			if (result.IsValid)
			{
				return result;
			}



			/*
             * Step 2
             *
             * Repair
             */
			var repairResult =
				await _repairService
					.RepairAsync(
						new QueryPlanRepairRequest
						{
							Plan = plan,

							Context = context,

							Errors =
								result.Errors
									.ToList(),

							Question =
								question
						});



			if (!repairResult.Success ||
			   repairResult.Plan == null)
			{
				return result;
			}



			/*
             * 使用修复后的Plan继续验证
             */
			plan =
				repairResult.Plan;

		}



		/*
         * 达到最大次数后最终验证
         */
		return _validator.Validate(
			plan,
			context);

	}

}