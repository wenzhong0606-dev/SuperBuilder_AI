using SuperBulider_AI.Interfaces;
using SuperBulider_AI.Interfaces.BI;
using SuperBulider_AI.Models.AI;
using SuperBulider_AI.Models.BI;
using SuperBulider_AI.Models.Metadata;


namespace SuperBulider_AI.Services.BI;

/// <summary>
/// QueryPlan 自动修复服务。
///
/// Phase 2.2.5
///
/// Metadata Semantic Repair Engine
///
/// 职责:
///
/// 1. 接收 QuerySemanticValidator 验证错误。
///
/// 2. 使用 MetadataSemanticSearchService
///    查找候选业务字段。
///
/// 3. 修复 QueryPlan:
///
///    QueryMetric.Field
///    QueryField.ColumnName
///    QueryField.MetadataColumnId
///    QueryFilter.Field
///
///
/// 当前版本:
///
/// 支持:
///
/// - Metric字段修复
/// - QueryField字段修复
/// - Filter字段修复
///
/// 不负责:
///
/// - SQL生成
/// - SQL执行
/// - QueryIntent重新生成
/// - Qwen推理
///
/// </summary>
public class QueryPlanRepairService
	: IQueryPlanRepairService
{


	private readonly IMetadataSemanticSearchService
		_metadataSemanticSearchService;



	public QueryPlanRepairService(
		IMetadataSemanticSearchService metadataSemanticSearchService)
	{
		_metadataSemanticSearchService =
			metadataSemanticSearchService;
	}




	/// <summary>
	/// 自动修复 QueryPlan。
	///
	/// 流程:
	///
	/// Validation Error
	///
	/// ↓
	///
	/// Semantic Search
	///
	/// ↓
	///
	/// MetadataColumn
	///
	/// ↓
	///
	/// Update QueryPlan
	///
	/// </summary>
	public async Task<QueryPlanRepairResult>
		RepairAsync(
			QueryPlanRepairRequest request)
	{

		if (request.Plan == null)
		{
			return new QueryPlanRepairResult
			{
				Success = false,

				Reason =
					"QueryPlan不能为空。"
			};
		}



		if (request.Errors == null
			||
			request.Errors.Count == 0)
		{
			return new QueryPlanRepairResult
			{
				Success = true,

				Plan =
					request.Plan,

				Reason =
					"没有需要修复的错误。"
			};
		}



		var repaired =
			false;



		foreach (var error in request.Errors)
		{

			/*
             * =====================================================
             *
             * 构造Semantic Search文本
             *
             * =====================================================
             */


			var searchText =
				BuildSearchText(
					request.Question,
					error);



			var candidates =
				await _metadataSemanticSearchService
					.SearchAsync(
						searchText,
						5);



			var best =
				candidates
					.Where(x =>
						x.Column != null)
					.OrderByDescending(
						x => x.Score)
					.FirstOrDefault();



			if (best?.Column == null)
			{
				continue;
			}



			var replaced =
				ReplaceQueryPlanField(
					request.Plan,
					error,
					best.Column);



			if (replaced)
			{
				repaired = true;
			}
		}



		return new QueryPlanRepairResult
		{
			Success =
				repaired,

			Plan =
				repaired
					? request.Plan
					: null,

			Reason =
				repaired
					? "QueryPlan修复成功。"
					: "没有找到匹配字段。"
		};
	}





	/// <summary>
	/// 构造 Metadata Semantic Search 查询。
	///
	/// 例如:
	///
	/// 用户:
	///
	/// 查询销售金额
	///
	/// 错误:
	///
	/// SalesAmount
	///
	///
	/// 生成:
	///
	/// 查询销售金额 SalesAmount
	///
	/// </summary>
	private string BuildSearchText(
		string question,
		SemanticValidationError error)
	{

		return
			$"""
            用户问题:
            {question}

            错误字段:
            {error.Field}

            错误信息:
            {error.Message}

            请寻找最匹配的业务字段。
            """;
	}





	/// <summary>
	/// 修改 QueryPlan 字段。
	///
	/// 顺序:
	///
	/// Metric
	///
	/// Field
	///
	/// Filter
	///
	/// </summary>
	private bool ReplaceQueryPlanField(
		QueryPlan plan,
		SemanticValidationError error,
		MetadataColumn column)
	{

		var replaced =
			false;



		/*
         * =====================================================
         *
         * 1. QueryMetric 修复
         *
         * Field:
         *
         * 业务真实字段
         *
         * =====================================================
         */


		if (plan.Metrics != null)
		{

			foreach (var metric in plan.Metrics)
			{

				if (string.Equals(
					metric.Field,
					error.Field,
					StringComparison.OrdinalIgnoreCase))
				{

					metric.Field =
						column.ColumnName;


					replaced =
						true;
				}

			}
		}





		/*
         * =====================================================
         *
         * 2. QueryField 修复
         *
         * 同时更新:
         *
         * ColumnName
         *
         * MetadataColumnId
         *
         * =====================================================
         */


		if (plan.Fields != null)
		{

			foreach (var field in plan.Fields)
			{

				if (string.Equals(
					field.ColumnName,
					error.Field,
					StringComparison.OrdinalIgnoreCase))
				{

					field.ColumnName =
						column.ColumnName;


					field.MetadataColumnId =
						column.Id;


					replaced =
						true;
				}

			}
		}





		/*
         * =====================================================
         *
         * 3. QueryFilter 修复
         *
         * =====================================================
         */


		if (plan.Filters != null)
		{

			foreach (var filter in plan.Filters)
			{

				if (string.Equals(
					filter.Field,
					error.Field,
					StringComparison.OrdinalIgnoreCase))
				{

					filter.Field =
						column.ColumnName;


					replaced =
						true;
				}

			}
		}



		return replaced;
	}

}