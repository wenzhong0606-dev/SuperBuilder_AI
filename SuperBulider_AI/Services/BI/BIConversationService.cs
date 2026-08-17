using Microsoft.EntityFrameworkCore;
using SuperBulider_AI.Data;
using SuperBulider_AI.Infrastructure.Database;
using SuperBulider_AI.Interfaces.BI;
using SuperBulider_AI.Interfaces.Database;
using SuperBulider_AI.Models.BI;


namespace SuperBulider_AI.Services.BI;

/// <summary>
/// AI BI会话编排服务。
///
/// 完整流程:
///
/// 用户问题
///     ↓
/// QueryUnderstanding
///     ↓
/// QueryIntent
///     ↓
/// QueryPlan
///     ↓
/// QueryPlan Validation
///     ↓
/// SQL生成
///     ↓
/// SQL执行
///     ↓
/// AI结果分析
///
/// Phase 2.2.4:
///
/// 增加 QueryPlan 验证链。
/// </summary>
public class BIConversationService :
	IBIConversationService
{
	private readonly IQueryUnderstandingService
		_queryUnderstandingService;


	private readonly IQueryPlanBuilder
		_queryPlanBuilder;


	private readonly ISqlQueryBuilder
		_sqlQueryBuilder;


	private readonly IQueryExecutionService
		_queryExecutionService;


	private readonly IResultUnderstandingService
		_resultUnderstandingService;


	private readonly SqlDialectResolver
		_sqlDialectResolver;


	private readonly SuperBIContext
		_context;


	/*
     * Phase 2.2.4
     *
     * QueryPlan Validation Chain
     */


	private readonly IQueryPlanContextBuilder
		_queryPlanContextBuilder;


	private readonly QueryPlanMetadataValidator
		_queryPlanMetadataValidator;


	private readonly QuerySemanticValidator
		_querySemanticValidator;



	public BIConversationService(
		IQueryUnderstandingService queryUnderstandingService,
		IQueryPlanBuilder queryPlanBuilder,
		ISqlQueryBuilder sqlQueryBuilder,
		IQueryExecutionService queryExecutionService,
		IResultUnderstandingService resultUnderstandingService,
		SqlDialectResolver sqlDialectResolver,
		SuperBIContext context,

		IQueryPlanContextBuilder queryPlanContextBuilder,
		QueryPlanMetadataValidator queryPlanMetadataValidator,
		QuerySemanticValidator querySemanticValidator)
	{
		_queryUnderstandingService =
			queryUnderstandingService;


		_queryPlanBuilder =
			queryPlanBuilder;


		_sqlQueryBuilder =
			sqlQueryBuilder;


		_queryExecutionService =
			queryExecutionService;


		_resultUnderstandingService =
			resultUnderstandingService;


		_sqlDialectResolver =
			sqlDialectResolver;


		_context =
			context;


		_queryPlanContextBuilder =
			queryPlanContextBuilder;


		_queryPlanMetadataValidator =
			queryPlanMetadataValidator;


		_querySemanticValidator =
			querySemanticValidator;
	}



	/// <summary>
	/// 执行完整AI BI查询。
	/// </summary>
	public async Task<BIResponse> AskAsync(
		string question,
		long dataSourceId)
	{
		if (string.IsNullOrWhiteSpace(question))
		{
			return new BIResponse
			{
				Success = false,

				Question =
					question ?? string.Empty,

				ErrorMessage =
					"用户问题不能为空。"
			};
		}


		try
		{
			/*
             * Phase 1
             *
             * Question
             *
             * ↓
             *
             * QueryIntent
             */

			var intent =
				await _queryUnderstandingService
					.UnderstandAsync(question);



			if (intent == null)
			{
				return new BIResponse
				{
					Success = false,

					Question = question,

					ErrorMessage =
						"AI无法理解用户查询意图。"
				};
			}



			/*
             * Phase 2
             *
             * QueryIntent
             *
             * ↓
             *
             * QueryPlan
             */


			var plan =
				await _queryPlanBuilder
					.BuildAsync(intent);



			if (plan == null)
			{
				return new BIResponse
				{
					Success = false,

					Question = question,

					ErrorMessage =
						"无法创建查询执行计划。"
				};
			}



			/*
             * ====================================================
             *
             * Phase 2.2.4
             *
             * QueryPlan Validation Chain
             *
             * QueryPlan
             *      ↓
             * ContextBuilder
             *      ↓
             * MetadataValidator
             *      ↓
             * SemanticValidator
             *
             * ====================================================
             */


			var validationContext =
				await _queryPlanContextBuilder
					.BuildAsync(plan);



			_queryPlanMetadataValidator
				.Validate(
					plan,
					validationContext);



			var semanticResult =
				_querySemanticValidator
					.Validate(
						plan,
						validationContext);



			if (!semanticResult.IsValid)
			{
				return new BIResponse
				{
					Success = false,

					Question = question,

					ErrorMessage =
						string.Join(
							"\n",
							semanticResult.ErrorItems
								.Select(
									x => x.Message))
				};
			}



			/*
             * Phase 3
             *
             * DataSource
             */


			plan.DataSourceId =
				dataSourceId;



			var dataSource =
				await _context.DataSources
					.AsNoTracking()
					.FirstOrDefaultAsync(
						x =>
							x.Id == dataSourceId);



			if (dataSource == null)
			{
				return new BIResponse
				{
					Success = false,

					Question = question,

					ErrorMessage =
						$"数据源不存在，DataSourceId={dataSourceId}。"
				};
			}



			if (dataSource.Enabled != true)
			{
				return new BIResponse
				{
					Success = false,

					Question = question,

					ErrorMessage =
						$"数据源已禁用，DataSourceId={dataSourceId}。"
				};
			}



			if (string.IsNullOrWhiteSpace(dataSource.DbType))
			{
				return new BIResponse
				{
					Success = false,

					Question = question,

					ErrorMessage =
						$"数据源未配置数据库类型，DataSourceId={dataSourceId}。"
				};
			}



			/*
             * Phase 4
             *
             * DbType
             *
             * ↓
             *
             * Dialect
             */


			var dialect =
				_sqlDialectResolver
					.Resolve(
						dataSource.DbType);



			/*
             * Phase 5
             *
             * QueryPlan
             *
             * ↓
             *
             * SqlQuery
             */


			var sqlQuery =
				await _sqlQueryBuilder
					.BuildAsync(
						plan,
						dialect);



			if (sqlQuery == null)
			{
				return new BIResponse
				{
					Success = false,

					Question = question,

					ErrorMessage =
						"SQL查询构建失败。"
				};
			}



			/*
             * Phase 6
             *
             * SQL执行
             */


			var queryResult =
				await _queryExecutionService
					.ExecuteAsync(
						sqlQuery,
						dataSourceId);



			if (queryResult == null)
			{
				return new BIResponse
				{
					Success = false,

					Question = question,

					Sql =
						sqlQuery.Sql,

					ErrorMessage =
						"数据库查询没有返回结果。"
				};
			}



			/*
             * Phase 7
             *
             * Result Understanding
             */


			QueryAnswer? answer = null;



			if (queryResult.Success)
			{
				answer =
					await _resultUnderstandingService
						.AnalyzeAsync(
							question,
							queryResult);
			}



			/*
             * Phase 8
             *
             * Response
             */


			return new BIResponse
			{
				Success =
					queryResult.Success,


				Question =
					question,


				Sql =
					sqlQuery.Sql,


				Data =
					queryResult,


				Answer =
					answer,


				ErrorMessage =
					queryResult.Success
						? null
						: queryResult.ErrorMessage
			};
		}
		catch (Exception ex)
		{
			return new BIResponse
			{
				Success = false,

				Question = question,

				ErrorMessage =
					ex.Message
			};
		}
	}
}