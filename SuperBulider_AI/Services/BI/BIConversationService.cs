using SuperBulider_AI.Data;
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
/// QueryPlan Confidence
///     ↓
/// QueryPlan Decision Gate
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
///
/// Phase 2.4:
/// QueryPlan Confidence & Decision Gate
/// </summary>
public class BIConversationService
	: IBIConversationService
{
	private readonly IQueryUnderstandingService
		_queryUnderstandingService;

	private readonly IQueryPlanBuilder
		_queryPlanBuilder;

	private readonly IQueryPlanContextBuilder
		_queryPlanContextBuilder;

	private readonly QueryPlanMetadataValidator
		_queryPlanMetadataValidator;

	private readonly SuperBIContext
		_superBIContext;

	/// <summary>
	/// Phase 2.2.5
	///
	/// QueryPlan验证+自动修复Pipeline
	/// </summary>
	private readonly IQueryPlanValidationPipeline
		_validationPipeline;

	/// <summary>
	/// Phase 2.4
	///
	/// QueryPlan Confidence 评估。
	/// </summary>
	private readonly IQueryPlanConfidenceService
		_queryPlanConfidenceService;

	/// <summary>
	/// Phase 2.4
	///
	/// QueryPlan Decision Gate。
	/// </summary>
	private readonly IQueryPlanDecisionGate
		_queryPlanDecisionGate;

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
		IQueryPlanConfidenceService queryPlanConfidenceService,
		IQueryPlanDecisionGate queryPlanDecisionGate,
		ISqlQueryBuilder sqlQueryBuilder,
		ISqlDialectResolver sqlDialectResolver,
		SuperBIContext superBIContext,
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

		_queryPlanConfidenceService =
			queryPlanConfidenceService;

		_queryPlanDecisionGate =
			queryPlanDecisionGate;

		_sqlQueryBuilder =
			sqlQueryBuilder;

		_sqlDialectResolver =
			sqlDialectResolver;

		_queryExecutionService =
			queryExecutionService;

		_resultUnderstandingService =
			resultUnderstandingService;

		_superBIContext =
			superBIContext;
	}


	public async Task<BIResponse> AskAsync(
		string question,
		long tenantId)
	{
		return await ExecuteAsync(
			question,
			tenantId);
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
	/// Phase 2.4:
	///
	/// Confidence
	///     ↓
	/// Decision Gate
	///     ↓
	/// SQL Builder
	/// </summary>
	public async Task<BIResponse>
		ExecuteAsync(
			string question,
			long tenantId)
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

				Question = question,

				ErrorMessage =
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

		plan =
			semanticValidation.Plan;


		/*
         * Step 5.1
         *
         * Validation 最终失败。
         *
         * Phase 2.4:
         *
         * Confidence / Decision Gate
         *
         * 必须位于 Validation Pipeline 之后。
         */
		if (!semanticValidation.ValidationResult.IsValid)
		{
			return new BIResponse
			{
				Success = false,

				Question = question,

				ErrorMessage =
					string.Join(
						"\n",
						semanticValidation.ValidationResult.Errors
							.Select(x => x.Message))
			};
		}


		/*
         * Step 5.2
         *
         * QueryPlan Confidence
         *
         * Phase 2.4
         *
         * Validation
         *     ↓
         * Repair
         *     ↓
         * RepairTrace
         *     ↓
         * Confidence
         */
		var confidence =
			await _queryPlanConfidenceService
				.EvaluateAsync(
					plan,
					semanticValidation,
					semanticValidation.RepairTrace,
					question);


		/*
         * Step 5.3
         *
         * QueryPlan Decision Gate
         *
         * Phase 2.4
         *
         * High
         *     ↓
         * Proceed
         *
         * Medium
         *     ↓
         * Confirm
         *
         * Low
         *     ↓
         * Reject
         */
		var decision =
			_queryPlanDecisionGate
				.Evaluate(
					confidence);


		/*
         * Step 5.4
         *
         * Decision Gate 阻断。
         *
         * 注意：
         *
         * 此处不能继续 SQL Builder。
         */
		if (!decision.ShouldExecute)
		{
			return new BIResponse
			{
				Success = false,

				Question = question,

				ErrorMessage =
					decision.Reason
					??
					"QueryPlan 未通过 Decision Gate，禁止进入 SQL Builder。"
			};
		}


		/*
         * Step 6
         *
         * SQL生成
         *
         * Phase 1
         *
         * 只有：
         *
         * Decision = Proceed
         *
         * 才允许进入这里。
         */
		var dataSource =
			await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
				.FirstAsync(
					_superBIContext.DataSources,
					x => x.Id == plan.DataSourceId);


		var dialect =
			_sqlDialectResolver
				.Resolve(
					dataSource.DbType);


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
				.ExecuteAsync(
					sql,
					plan.DataSourceId);


		/*
         * Step 8
         *
         * Result理解
         *
         * Phase 1
         */
		var answer =
			await _resultUnderstandingService
				.AnalyzeAsync(
					question,
					data);


		return new BIResponse
		{
			Success = true,

			Question = question,

			Sql =
				sql.Sql,

			Answer =
				answer,

			Data =
				data
		};
	}
}