using SuperBulider_AI.Interfaces.BI;
using SuperBulider_AI.Models.BI;


namespace SuperBulider_AI.Services.BI;

/// <summary>
/// Query计划修复服务。
///
/// Phase 2.2.5
///
/// 负责:
///
/// Semantic Validation失败
///          ↓
/// 分析错误
///          ↓
/// 修正 QueryIntent
///
/// </summary>
public class QueryPlanRepairService :
	IQueryPlanRepairService
{

	/// <summary>
	/// 修复查询意图。
	/// </summary>
	public Task<QueryIntent> RepairAsync(
		QueryIntent intent,
		QuerySemanticValidationResult validationResult)
	{

		if (intent == null)
		{
			throw new ArgumentNullException(
				nameof(intent));
		}


		if (validationResult == null)
		{
			throw new ArgumentNullException(
				nameof(validationResult));
		}


		foreach (var error in validationResult.Errors)
		{
			RepairIntent(
				intent,
				error);
		}


		return Task.FromResult(intent);
	}



	/// <summary>
	/// 根据错误修复Intent。
	/// </summary>
	private static void RepairIntent(
		QueryIntent intent,
		SemanticValidationError error)
	{

		switch (error.Code)
		{

			case "METRIC_FIELD_NOT_FOUND":

				RepairMetric(
					intent);

				break;


			case "FILTER_FIELD_NOT_FOUND":

				RepairFilter(
					intent);

				break;

		}

	}



	/// <summary>
	/// Metric修复。
	/// </summary>
	private static void RepairMetric(
		QueryIntent intent)
	{

		foreach (var metric in intent.Metrics)
		{
			if (string.IsNullOrWhiteSpace(
				metric.Field))
			{
				metric.Field =
					metric.Name;
			}
		}

	}



	/// <summary>
	/// Filter修复。
	/// </summary>
	private static void RepairFilter(
		QueryIntent intent)
	{

		intent.Filters =
			intent.Filters
				.Where(x =>
					!string.IsNullOrWhiteSpace(
						x.Field))
				.ToList();

	}

}