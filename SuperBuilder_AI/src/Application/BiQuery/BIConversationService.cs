using SuperBuilder_AI.Data;
using SuperBuilder_AI.Infrastructure.Database;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Interfaces.Database;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Interfaces.Platform;
using SuperBuilder_AI.Models.Organization;

namespace SuperBuilder_AI.Services.BI;

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
/// QueryPlan Explainability
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
///
/// Phase 2.5:
/// QueryPlan Explainability
/// </summary>
public class BIConversationService
	: IBIConversationService
{
	private readonly IQueryUnderstandingService
		_queryUnderstandingService;

	private readonly IQueryPlanPipeline
		_queryPlanPipeline;

	private readonly SuperBIContext
		_superBIContext;

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

	private readonly IPlatformContextAccessor?
		_platformContextAccessor;


	public BIConversationService(
		IQueryUnderstandingService queryUnderstandingService,
		IQueryPlanPipeline queryPlanPipeline,
		ISqlQueryBuilder sqlQueryBuilder,
		ISqlDialectResolver sqlDialectResolver,
		SuperBIContext superBIContext,
		IQueryExecutionService queryExecutionService,
		IResultUnderstandingService resultUnderstandingService,
		IPlatformContextAccessor? platformContextAccessor = null)
	{
		_queryUnderstandingService =
			queryUnderstandingService;

		_queryPlanPipeline =
			queryPlanPipeline;

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

		_platformContextAccessor =
			platformContextAccessor;
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
	///
	/// Phase 2.5:
	///
	/// Explainability
	///     ↓
	/// SQL Builder
	/// </summary>
	public async Task<BIResponse>
		ExecuteAsync(
			string question,
			long tenantId)
	{
		/*
		 * Step 0（P4）
		 *
		 * 建立平台运行时上下文：把传入的 tenantId 收敛为 PlatformContext，
		 * 写入 IPlatformContextAccessor 供下游（含 P4.3 的 SuperBIContext 全局租户过滤）读取。
		 * 不修改下游 tenantId 透传，Golden 无租户路径不受影响。
		 */
		var platformContext = tenantId > 0
			? PlatformContext.FromTenant(tenantId)
			: PlatformContext.System;
		_platformContextAccessor?.Current = platformContext;

		// P4.3：开启全局租户过滤（tenantId <= 0 时 ApplyTenantScope 内部保持关闭，等价于 no-op）。
		_superBIContext.ApplyTenantScope(tenantId);

		/*
         * Step 1
         *
         * 用户问题理解
         */
		var intent =
			await _queryUnderstandingService
				.UnderstandAsync(question, tenantId);


		/*
         * Step 2 ~ Step 5.4
         *
         * 构建 QueryPlan
         *     ↓
         * Metadata 关系完整性验证
         *     ↓
         * 语义验证 + 自动修复
         *     ↓
         * Confidence
         *     ↓
         * Decision Gate
         *     ↓
         * Explainability
         *
         * 上述编排已抽取到 QueryPlanPipeline，
         * 行为与重构前完全一致；提前结束（验证失败 / Decision Gate 阻断）时直接返回 EarlyResponse。
         */
		var pipelineResult =
			await _queryPlanPipeline
				.RunAsync(
					question,
					intent);

		if (pipelineResult.EarlyResponse != null)
		{
			return pipelineResult.EarlyResponse;
		}

		var plan =
			pipelineResult.Plan;

		var explanation =
			pipelineResult.Explanation;


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


		/*
         * Step 9
         *
         * BI最终响应
         *
         * Phase 2.5:
         *
         * 将 QueryPlan Explainability
         * 一并返回给上层。
         */
		return new BIResponse
		{
			Success = true,

			Question = question,

			Sql =
				sql.Sql,

			Answer =
				answer,

			Data =
				data,

			Explanation =
				explanation
		};
	}
}