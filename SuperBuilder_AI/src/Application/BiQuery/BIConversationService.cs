using SuperBuilder_AI.Data;
using SuperBuilder_AI.Infrastructure.Database;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Interfaces.Database;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Interfaces.Platform;
using SuperBuilder_AI.Interfaces.Identity;
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
	private readonly IRowLevelSecurityService? _rowSecurity;
	private readonly IDataSourceExecutionIdentityAccessor? _executionIdentity;
	private readonly IQueryPlanSecurityGate? _securityGate;


	public BIConversationService(
		IQueryUnderstandingService queryUnderstandingService,
		IQueryPlanPipeline queryPlanPipeline,
		ISqlQueryBuilder sqlQueryBuilder,
		ISqlDialectResolver sqlDialectResolver,
		SuperBIContext superBIContext,
		IQueryExecutionService queryExecutionService,
		IResultUnderstandingService resultUnderstandingService,
		IPlatformContextAccessor? platformContextAccessor = null,
		IRowLevelSecurityService? rowSecurity = null,
		IDataSourceExecutionIdentityAccessor? executionIdentity = null,
		IQueryPlanSecurityGate? securityGate = null)
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
		_rowSecurity = rowSecurity;
		_executionIdentity = executionIdentity;
		_securityGate = securityGate;
	}


	public async Task<BIResponse> AskAsync(
		string question,
		long tenantId,
		long? requestedDataSourceId = null,
		IReadOnlyCollection<long>? authorizedDataSourceIds = null)
	{
		return await ExecuteAsync(
			question,
			tenantId,
			requestedDataSourceId,
			authorizedDataSourceIds);
	}


	/// <summary>
	/// 执行一次 BI 查询。
	///
	/// Phase 2.2.5：
	///
	/// Validate
	///     ↓
	/// Repair
	///     ↓
	/// ReValidate
	///
	/// Phase 2.4：
	///
	/// Confidence
	///     ↓
	/// Decision Gate
	///
	/// Phase 2.5：
	///
	/// Explainability
	///     ↓
	/// SQL Builder
	/// </summary>
	public async Task<BIResponse>
		ExecuteAsync(
			string question,
			long tenantId,
			long? requestedDataSourceId = null,
			IReadOnlyCollection<long>? authorizedDataSourceIds = null)
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
				.UnderstandAsync(question, platformContext);


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
					intent,
					requestedDataSourceId,
					authorizedDataSourceIds);

		if (pipelineResult.EarlyResponse != null)
		{
			return pipelineResult.EarlyResponse;
		}

		var plan =
			pipelineResult.Plan;

		var explanation =
			pipelineResult.Explanation;

		// P0-05 最终闸门：即使计划被伪造或前置过滤回归，也不能进入 SQL 生成与执行。
		if (authorizedDataSourceIds is not null &&
			!authorizedDataSourceIds.Contains(plan.DataSourceId))
		{
			throw SuperBuilder_AI.Api.Errors.SuperBuilderException.FromCode(
				SuperBuilder_AI.Api.Errors.ErrorCodes.DataSourceForbidden, 403);
		}

		// P0-06 固定落点：Plan 已成型、SQL Builder 尚未调用。
		// 认证 API 必须具备执行身份；Golden/内部兼容路径的授权集合为 null，不进入 RLS。
		if (authorizedDataSourceIds is not null && _rowSecurity is not null)
		{
			var caller = _executionIdentity?.Current;
			if (caller is null || caller.TenantId != tenantId)
				throw SuperBuilder_AI.Api.Errors.SuperBuilderException.FromCode(
					SuperBuilder_AI.Api.Errors.ErrorCodes.RowPolicyForbidden, 403);
			plan.EffectiveTenantId = tenantId;
			await _rowSecurity.ApplyAsync(plan, tenantId, caller.UserId);
			if (_securityGate is not null)
				await _securityGate.ValidateAsync(plan, tenantId, caller.UserId);
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
					x => x.Id == plan.DataSourceId && x.TenantId == tenantId && x.Enabled == true);


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
					data,
					plan);


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
