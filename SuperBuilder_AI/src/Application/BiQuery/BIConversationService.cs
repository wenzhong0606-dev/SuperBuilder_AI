using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Infrastructure.Database;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.AppBuilder;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Interfaces.Database;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.AppBuilder;
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
	private readonly IAskQuerySnapshotStore? _snapshotStore;

	private readonly IPipelineMetricsSink? _metrics;

	/// <summary>
	/// 结果字段译码服务（code→text）。可空：未注册时为 no-op，Golden/内部路径零影响。
	/// </summary>
	private readonly IDisplayResolutionService? _displayResolution;

	/// <summary>
	/// 自主学习纠错服务（显式纠正回放）。可空：未注册时为 no-op。
	/// </summary>
	private readonly ICorrectionLearningService? _correctionLearning;

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
		IQueryPlanSecurityGate? securityGate = null,
		IAskQuerySnapshotStore? snapshotStore = null,
		IPipelineMetricsSink? metrics = null,
		IDisplayResolutionService? displayResolution = null,
		ICorrectionLearningService? correctionLearning = null)
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
		_snapshotStore = snapshotStore;
		_metrics = metrics;
		_displayResolution = displayResolution;
		_correctionLearning = correctionLearning;
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
	/// 解析「理解阶段」Metadata 上下文的数据源作用域（P0）。
	///
	/// 与下游 <c>QueryPlanBuilder</c> 的候选收敛口径保持一致：
	///
	///   - <paramref name="authorizedDataSourceIds"/> 非 <c>null</c>：以授权集合为基；
	///   - <paramref name="requestedDataSourceId"/> &gt; 0：进一步收敛到该数据源；
	///   - 两者皆无（Golden / 评估器 / 内部兼容路径）：返回 <c>null</c> —— 不限定作用域，
	///     语义检索行为与新增本机制之前逐字节一致。
	///
	/// 注意：授权集合为空时返回<strong>空集合而非 null</strong>。空集合表示
	/// 「确实没有可用数据源」，理解阶段据此产出空上下文；随后
	/// <c>QueryPlanDataSourceScope</c> 会以 403（DataSourceForbidden）终止请求，
	/// 与既有行为一致。若在此处把空集合降级为 null，反而会让未授权数据源
	/// 重新进入提示词。
	/// </summary>
	private static IReadOnlyCollection<long>? ResolveMetadataSearchScope(
		long? requestedDataSourceId,
		IReadOnlyCollection<long>? authorizedDataSourceIds)
	{
		var scope = authorizedDataSourceIds;

		if (requestedDataSourceId is { } requestedId && requestedId > 0)
		{
			scope = scope is null
				? new[] { requestedId }
				: scope.Where(id => id == requestedId).ToArray();
		}

		return scope;
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
		try
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

		// M6-05：总耗时计时（旁路，仅读取，不影响主流程结果）。
		var swTotal = Stopwatch.StartNew();
		var sw = Stopwatch.StartNew();

		/*
		 * Step 0.5（自主学习回放）
		 *
		 * 若当前用户历史上对该问句做过显式纠正，则自动把纠正合成进问题，
		 * 使「下次查询按修正后的结果执行」无需用户重复纠正。
		 * 仅 (tenant, user, 归一化问句) 命中才生效；未命中时 question 逐字节不变。
		 */
		CorrectionResolution? learnedCorrections = null;
		var callerUserId = _executionIdentity?.Current is { } identity && identity.TenantId == tenantId
			? identity.UserId
			: (long?)null;

		if (_correctionLearning is not null)
		{
			try
			{
				learnedCorrections = await _correctionLearning
					.ResolveAsync(tenantId, callerUserId, question);

				if (learnedCorrections.Any)
					question = _correctionLearning.ComposeLearnedQuestion(question, learnedCorrections);
			}
			catch
			{
				// 学习回放是增值能力：任何故障仅降级为「按原问句执行」。
				learnedCorrections = null;
			}
		}

		/*
         * Step 1
         *
         * 用户问题理解
         *
         * P0：理解阶段按数据源作用域收敛 Metadata 上下文。
         * 语义检索是全局 top-K，若不限定作用域，其他数据源的同名列会进提示词，
         * 把 Metric / Dimension 解析带偏（详见 IMetadataSemanticSearchService 的
         * 四参重载注释）。作用域与下游 QueryPlanBuilder 的收敛口径保持一致：
         *   - 显式请求数据源 > 0  => 收敛到该数据源；
         *   - 否则收敛到「已授权数据源集合」；
         *   - 两者皆无（Golden / 内部兼容路径）=> null，行为不变。
         */
		var searchScope =
			ResolveMetadataSearchScope(
				requestedDataSourceId,
				authorizedDataSourceIds);

		var intent =
			await _queryUnderstandingService
				.UnderstandAsync(
					question,
					platformContext,
					searchScope);

		var metadataUnderstandMs = sw.ElapsedMilliseconds;
		// M9-05：管线分段延迟埋点（异常静默，不影响主流程）。
		_metrics?.RecordStage(IPipelineMetricsSink.StageUnderstand, metadataUnderstandMs);
		sw.Restart();


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

		var planMs = sw.ElapsedMilliseconds;
		// M9-05：管线分段延迟埋点 + 结果分类计数。
		_metrics?.RecordStage(IPipelineMetricsSink.StagePlan, planMs);
		_metrics?.RecordOutcome(IPipelineMetricsSink.OutcomeReject, pipelineResult.WasRejected);
		_metrics?.RecordOutcome(IPipelineMetricsSink.OutcomeRepair, pipelineResult.WasRepaired);
		_metrics?.RecordOutcome(IPipelineMetricsSink.OutcomeEarlyReturn, pipelineResult.EarlyResponse != null);
		sw.Restart();

		if (pipelineResult.EarlyResponse != null)
		{
			// M6-05：提前返回也填充耗时（仅 Plan 段有效，SQL/DB/Result 段为 0）。
			var early = pipelineResult.EarlyResponse;
			early.DurationMs = swTotal.ElapsedMilliseconds;
			early.SegmentTimings = new AskSegmentTimings(metadataUnderstandMs, planMs, 0, 0, 0);
			return early;
		}

		var plan =
			pipelineResult.Plan;

		var explanation =
			pipelineResult.Explanation;

		// M7-11：在 RLS 注入前深拷贝允许查询的语义（不含发布者行级条件），供后续成功落库快照。
		// 仅在已注册快照存储且为已认证访问者路径（authorizedDataSourceIds 非 null）时截取。
		string? snapshotPlanJson = null;
		long? snapshotCallerUserId = null;
		string? snapshotTurnId = null;
		if (_snapshotStore is not null && authorizedDataSourceIds is not null)
			snapshotPlanJson = JsonSerializer.Serialize(plan);

		// P0-05 最终闸门：即使计划被伪造或前置过滤回归，也不能进入 SQL 生成与执行。
		if (authorizedDataSourceIds is not null &&
			!authorizedDataSourceIds.Contains(plan.DataSourceId))
		{
			throw SuperBuilder_AI.Api.Errors.SuperBuilderException.FromCode(
				SuperBuilder_AI.Api.Errors.ErrorCodes.DataSourceForbidden, 403);
		}

		// P0-06 固定落点：Plan 已成型、SQL Builder 尚未调用。
		// 认证 API 必须具备执行身份；Golden/内部兼容路径的授权集合为 null，不进入 RLS。
		if (authorizedDataSourceIds is not null)
		{
			if (_rowSecurity is not null)
			{
				var caller = _executionIdentity?.Current;
				if (caller is null || caller.TenantId != tenantId)
					throw SuperBuilder_AI.Api.Errors.SuperBuilderException.FromCode(
						SuperBuilder_AI.Api.Errors.ErrorCodes.RowPolicyForbidden, 403);
				snapshotCallerUserId = caller.UserId;
				plan.EffectiveTenantId = tenantId;
				await _rowSecurity.ApplyAsync(plan, tenantId, caller.UserId);
				if (_securityGate is not null)
					await _securityGate.ValidateAsync(plan, tenantId, caller.UserId);
			}
			else
			{
				// 极少数内部兼容路径（无 RLS 服务）：仍尝试取访问者身份用于快照归属。
				snapshotCallerUserId = _executionIdentity?.Current?.UserId;
			}
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

		var sqlBuildMs = sw.ElapsedMilliseconds;
		// M9-05：管线分段延迟埋点。
		_metrics?.RecordStage(IPipelineMetricsSink.StageSql, sqlBuildMs);
		sw.Restart();


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

		var dbExecMs = sw.ElapsedMilliseconds;
		// M9-05：管线分段延迟埋点。
		_metrics?.RecordStage(IPipelineMetricsSink.StageDb, dbExecMs);
		sw.Restart();

		// M7-11：查询成功 → 落库快照并返回 turnId（仅已认证访问者路径）。
		// 快照在 RLS 注入前截取，不含发布者行级条件；运行时重新应用当前访问者策略。
		if (_snapshotStore is not null && snapshotCallerUserId is { } uid && snapshotPlanJson is not null)
		{
			snapshotTurnId = Guid.NewGuid().ToString("N");
			var hash = Convert.ToHexString(SHA256.HashData(
				Encoding.UTF8.GetBytes($"{tenantId}|{uid}|{snapshotPlanJson}")));
			await _snapshotStore.SaveAsync(new AskQuerySnapshot
			{
				TurnId = snapshotTurnId,
				TenantId = tenantId,
				UserId = uid,
				DataSourceId = plan.DataSourceId,
				EntityCode = plan.Tables.FirstOrDefault()?.SemanticText,
				QueryPlanJson = snapshotPlanJson,
				RequestHash = hash,
				ExpiresAt = DateTime.UtcNow.AddHours(24),
			}, CancellationToken.None);
		}


		/*
		 * Step 7.5（结果字段译码）
		 *
		 * 对结果逐列做 code→text 富化：跨源字典（PMIS）→ 同源外键 → 学习规则 → 列内嵌值映射。
		 * 不改用户 SQL、不做跨库 JOIN；未授权/失配一律降级保留原值。
		 * 译码失败不影响主流程返回。
		 */
		if (_displayResolution is not null && data.Success && data.Rows.Count > 0)
		{
			try
			{
				await _displayResolution.EnrichAsync(
					plan,
					data,
					tenantId,
					callerUserId,
					authorizedDataSourceIds,
					learnedCorrections);
			}
			catch
			{
				// 译码是增值能力：失败保持原始结果。
			}
		}

		// 学习规则命中统计（旁路；失败静默）。
		if (_correctionLearning is not null && learnedCorrections is { } matched && matched.Any)
		{
			try
			{
				await _correctionLearning.MarkMatchedAsync(matched);
			}
			catch
			{
				// 统计回写失败不影响结果。
			}
		}

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

		var resultUnderstandMs = sw.ElapsedMilliseconds;
		// M9-05：管线分段延迟埋点。
		_metrics?.RecordStage(IPipelineMetricsSink.StageResult, resultUnderstandMs);
		sw.Stop();
		swTotal.Stop();


		/*
         * Step 9
         *
         * BI最终响应
         *
         * Phase 2.5：
         *
         * 将 QueryPlan Explainability
         * 一并返回给上层。
         */
		// OBS-01：Ask 成功产出结果埋点（成功 = 真实返回答案，区别于 EarlyReturn/Reject）。
		_metrics?.RecordOutcome(IPipelineMetricsSink.OutcomeSuccess, true);
		if (swTotal.ElapsedMilliseconds > AskTimeoutMs)
			_metrics?.RecordOutcome(IPipelineMetricsSink.OutcomeTimeout, true);

		return new BIResponse
		{
			Success = true,

			Question = question,

			TurnId = snapshotTurnId,

			Sql =
				sql.Sql,

			Answer =
				answer,

			Data =
				data,

			Explanation =
				explanation,

			// M6-05：填充总耗时与分段耗时（仅读取，不改变响应其它语义）。
			DurationMs = swTotal.ElapsedMilliseconds,
			SegmentTimings = new AskSegmentTimings(metadataUnderstandMs, planMs, sqlBuildMs, dbExecMs, resultUnderstandMs)
		};
		}
		catch (Exception ex)
		{
			// OBS-01：Ask 执行异常分类埋点。异常仍向上传播，由 UnifiedExceptionMiddleware 处理。
			RecordAskFailure(ex);
			throw;
		}
	}

	/// <summary>OBS-01：Ask 整体耗时的「慢查询」阈值（毫秒）。超过即额外标记一次 Timeout 结果，便于告警。</summary>
	private const long AskTimeoutMs = 30000;

	/// <summary>OBS-01：Ask 失败分类埋点（异常静默，不影响主链路）。</summary>
	private void RecordAskFailure(Exception ex)
	{
		try
		{
			_metrics?.RecordOutcome(IPipelineMetricsSink.OutcomeFailure, true);
			if (ex is TimeoutException or TaskCanceledException or OperationCanceledException)
				_metrics?.RecordOutcome(IPipelineMetricsSink.OutcomeTimeout, true);
			else if (IsDbException(ex))
				_metrics?.RecordOutcome(IPipelineMetricsSink.OutcomeDbError, true);
			else
				_metrics?.RecordOutcome(IPipelineMetricsSink.OutcomeLlmError, true);
		}
		catch
		{
			// 指标采集失败不影响主链路
		}
	}

	/// <summary>OBS-01：判断异常是否源自数据访问层（EF / ADO.NET / SQL 驱动）。</summary>
	private static bool IsDbException(Exception ex)
	{
		for (var e = ex; e is not null; e = e.InnerException)
		{
			if (e is System.Data.Common.DbException or Microsoft.EntityFrameworkCore.DbUpdateException)
				return true;
			var name = e.GetType().FullName ?? string.Empty;
			if (name.Contains("EntityFramework", StringComparison.OrdinalIgnoreCase)
				|| name.Contains("SqlClient", StringComparison.OrdinalIgnoreCase)
				|| name.Contains("System.Data", StringComparison.OrdinalIgnoreCase))
				return true;
		}
		return false;
	}
}
