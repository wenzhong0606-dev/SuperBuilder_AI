using SuperBulider_AI.Infrastructure.Database;
using SuperBulider_AI.Interfaces;
using SuperBulider_AI.Interfaces.BI;
using SuperBulider_AI.Interfaces.Database;
using SuperBulider_AI.Models.AI;
using SuperBulider_AI.Models.BI;


namespace SuperBulider_AI.Services.BI;

/// <summary>
/// AI BI 对话编排服务。
///
/// 核心职责:
///
/// 用户问题
///     ↓
/// Query理解
///     ↓
/// QueryPlan生成
///     ↓
/// QueryPlan验证
///     ↓
/// QueryPlan自动修复
///     ↓
/// SQL生成
///     ↓
/// 数据执行
///     ↓
/// 结果理解
///
/// Phase 1:
/// AI BI 查询核心链路
///
/// Phase 2.2.5:
/// QueryPlan自动修复链（AI Repair Loop）
/// </summary>
public class BIConversationService
	:
	IBIConversationService
{

	private readonly IQueryUnderstandingService
		_queryUnderstandingService;


	private readonly IQueryPlanBuilder
		_queryPlanBuilder;


	private readonly IQueryPlanContextBuilder
		_queryPlanContextBuilder;


	private readonly QueryPlanMetadataValidator
		_queryPlanMetadataValidator;


	/// <summary>
	/// Phase 2.2.5
	///
	/// QueryPlan验证+自动修复Pipeline
	/// </summary>
	private readonly IQueryPlanValidationPipeline
		_validationPipeline;


	private readonly ISqlQueryBuilder
		_sqlQueryBuilder;


	/// <summary>
	/// SQL方言解析器。
	///
	/// 根据数据源类型:
	///
	/// SQLServer
	/// MySQL
	/// PostgreSQL
	///
	/// 返回对应Dialect。
	/// </summary>
	private readonly ISqlDialectResolver
		_sqlDialectResolver;


	private readonly IQueryExecutionService
		_queryExecutionService;


	private readonly IResultUnderstandingService
		_resultUnderstandingService;



	public BIConversationService(
		IQueryUnderstandingService queryUnderstandingService,
		IQueryPlanBuilder queryPlanBuilder,
		IQueryPlanContextBuilder queryPlanContextBuilder,
		QueryPlanMetadataValidator queryPlanMetadataValidator,
		IQueryPlanValidationPipeline validationPipeline,
		ISqlQueryBuilder sqlQueryBuilder,
		ISqlDialectResolver sqlDialectResolver,
		IQueryExecutionService queryExecutionService,
		IResultUnderstandingService resultUnderstandingService)
	{
		_queryUnderstandingService =
			queryUnderstandingService;


		_queryPlanBuilder =
			queryPlanBuilder;


		_queryPlanContextBuilder =
			queryPlanContextBuilder;


		_queryPlanMetadataValidator =
			queryPlanMetadataValidator;


		_validationPipeline =
			validationPipeline;


		_sqlQueryBuilder =
			sqlQueryBuilder;


		_sqlDialectResolver =
			sqlDialectResolver;


		_queryExecutionService =
			queryExecutionService;


		_resultUnderstandingService =
			resultUnderstandingService;
	}



	/// <summary>
	/// 执行一次 BI 查询。
	///
	/// Phase 2.2.5:
	///
	/// Validate
	///     ↓
	/// Repair
	///     ↓
	/// ReValidate
	///
	/// </summary>
	public async Task<BIResponse>
		ExecuteAsync(
			string question)
	{

		/*
         * Step 1
         *
         * 用户问题理解
         */
		var intent =
			await _queryUnderstandingService
				.UnderstandAsync(question);



		/*
         * Step 2
         *
         * 构建 QueryPlan
         */
		var plan =
			await _queryPlanBuilder
				.BuildAsync(intent);



		/*
         * Step 3
         *
         * 构建 QueryPlan Validation Context
         */
		var validationContext =
			await _queryPlanContextBuilder
				.BuildAsync(plan);



		/*
         * Step 4
         *
         * Metadata关系完整性验证
         *
         * 注意:
         *
         * 当前 Validator:
         *
         * void Validate()
         *
         * 失败通过异常表达。
         */
		try
		{

			_queryPlanMetadataValidator
				.Validate(
					plan,
					validationContext);

		}
		catch (Exception ex)
		{

			return new BIResponse
			{
				Success = false,

				Message =
					ex.Message
			};

		}



		/*
         * Step 5
         *
         * QueryPlan语义验证+自动修复
         *
         * Phase 2.2.5
         *
         * Validate
         *
         * ↓
         *
         * Repair
         *
         * ↓
         *
         * ReValidate
         */
		var semanticValidation =
			await _validationPipeline
				.ValidateAsync(
					plan,
					validationContext,
					question);



		if (!semanticValidation.IsValid)
		{
			return new BIResponse
			{
				Success = false,

				Message =
					string.Join(
						"\n",
						semanticValidation.Errors)
			};
		}



		/*
         * Step 6
         *
         * SQL生成
         *
         * Phase 1
         */
		var dialect =
			_sqlDialectResolver
				.Resolve(
					plan.DataSource.DbType);



		var sql =
			await _sqlQueryBuilder
				.BuildAsync(
					plan,
					dialect);



		/*
         * Step 7
         *
         * 执行SQL
         *
         * Phase 1
         */
		var data =
			await _queryExecutionService
				.ExecuteAsync(sql);



		/*
         * Step 8
         *
         * Result理解
         *
         * Phase 1
         */
		var answer =
			await _resultUnderstandingService
				.UnderstandAsync(
					question,
					data);



		return new BIResponse
		{
			Success = true,

			Answer =
				answer.Answer,

			Data =
				data
		};

	}

}