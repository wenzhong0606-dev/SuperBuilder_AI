using Microsoft.EntityFrameworkCore;
using SuperBulider_AI.Data;
using SuperBulider_AI.Infrastructure.Database;
using SuperBulider_AI.Interfaces;
using SuperBulider_AI.Models.AI;

namespace SuperBulider_AI.Services.BI;

/// <summary>
/// AI BI会话编排服务。
///
/// 负责将:
///
/// 用户问题
///     ↓
/// QueryUnderstanding
///     ↓
/// QueryPlan
///     ↓
/// SQL生成
///     ↓
/// SQL执行
///     ↓
/// AI结果分析
///
/// 串联成完整查询流程。
///
/// 注意:
///
/// 本服务只负责流程编排，
/// 不负责具体业务逻辑实现。
/// </summary>
public class BIConversationService
	: IBIConversationService
{
	private readonly IQueryUnderstandingService _queryUnderstandingService;

	private readonly IQueryPlanBuilder _queryPlanBuilder;

	private readonly ISqlQueryBuilder _sqlQueryBuilder;

	private readonly IQueryExecutionService _queryExecutionService;

	private readonly IResultUnderstandingService _resultUnderstandingService;

	private readonly SqlDialectResolver _sqlDialectResolver;

	private readonly SuperBIContext _context;

	/// <summary>
	/// 创建AI BI会话编排服务。
	/// </summary>
	public BIConversationService(
		IQueryUnderstandingService queryUnderstandingService,
		IQueryPlanBuilder queryPlanBuilder,
		ISqlQueryBuilder sqlQueryBuilder,
		IQueryExecutionService queryExecutionService,
		IResultUnderstandingService resultUnderstandingService,
		SqlDialectResolver sqlDialectResolver,
		SuperBIContext context)
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
	}

	/// <summary>
	/// 执行完整的AI BI查询流程。
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
				Question = question ?? string.Empty,
				ErrorMessage = "用户问题不能为空。"
			};
		}

		try
		{
			/*
			 * ============================================================
			 * Phase 1
			 *
			 * 用户问题
			 * ↓
			 * QueryIntent
			 * ============================================================
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
					ErrorMessage = "AI无法理解用户查询意图。"
				};
			}

			/*
			 * ============================================================
			 * Phase 2
			 *
			 * QueryIntent
			 * ↓
			 * QueryPlan
			 * ============================================================
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
					ErrorMessage = "无法创建查询执行计划。"
				};
			}

			/*
			 * ============================================================
			 * Phase 3
			 *
			 * 根据DataSource获取数据库类型。
			 *
			 * QueryPlan中的DataSourceId
			 * 目前由QueryPlanBuilder负责生成。
			 *
			 * 但本次AskAsync同时接收dataSourceId，
			 * 因此这里优先使用调用方指定的数据源。
			 * ============================================================
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

			if (dataSource.Enabled == false)
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
			 * ============================================================
			 * Phase 4
			 *
			 * DataSource.DbType
			 * ↓
			 * ISqlDialect
			 * ============================================================
			 */

			var dialect =
				_sqlDialectResolver.Resolve(
					dataSource.DbType);

			/*
			 * ============================================================
			 * Phase 5
			 *
			 * QueryPlan
			 * +
			 * SQL Dialect
			 *
			 * ↓
			 *
			 * SqlQuery
			 * ============================================================
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
					ErrorMessage = "SQL查询构建失败。"
				};
			}

			/*
			 * ============================================================
			 * Phase 6
			 *
			 * SqlQuery
			 * ↓
			 * QueryExecutionService
			 * ↓
			 * QueryResult
			 * ============================================================
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
					Sql = sqlQuery.Sql,
					ErrorMessage = "数据库查询没有返回结果。"
				};
			}

			/*
			 * ============================================================
			 * Phase 7
			 *
			 * QueryResult
			 * ↓
			 * ResultUnderstandingService
			 * ↓
			 * QueryAnswer
			 * ============================================================
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
			 * ============================================================
			 * Phase 8
			 *
			 * 组装最终BIResponse
			 * ============================================================
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