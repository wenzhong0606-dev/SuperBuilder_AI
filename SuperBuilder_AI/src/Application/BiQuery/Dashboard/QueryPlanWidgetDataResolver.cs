using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.BI.Dashboard;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Interfaces.Database;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Application.BiQuery;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Dashboard;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Infrastructure.Security;

namespace SuperBuilder_AI.Services.BI.Dashboard;

/// <summary>
/// 组件取数解析器默认实现（P6.3）。对照 QueryPlanPipeline 接入：
/// 把 <see cref="WidgetQueryDsl"/> 转换为 QueryIntent → 过 QueryPlanPipeline（含 Decision Gate）
/// → ISqlQueryBuilder 生成 SQL → IQueryExecutionService 执行，得到结构化结果行。
///
/// 与 <c>BIConversationService</c> 的 Step 2~7 完全一致，仅把"用户问句"替换为 DSL 取数定义，
/// 因此不触碰任何既有 Golden 路径、不引入新的语义漂移。
/// </summary>
public sealed class QueryPlanWidgetDataResolver : IWidgetDataResolver
{
	private readonly IQueryUnderstandingService _understanding;
	private readonly IQueryPlanPipeline _pipeline;
	private readonly SuperBIContext _db;
	private readonly ISqlDialectResolver _dialectResolver;
	private readonly ISqlQueryBuilder _sqlBuilder;
	private readonly IQueryExecutionService _exec;
	private readonly IDataSourceAuthorizationService? _dataSourceAuthorization;
	private readonly IRowLevelSecurityService? _rowSecurity;
	private readonly IDataSourceExecutionIdentityAccessor? _executionIdentity;
	private readonly IQueryPlanSecurityGate? _securityGate;
	private readonly ISecretStore? _secrets;

	public QueryPlanWidgetDataResolver(
		IQueryUnderstandingService understanding,
		IQueryPlanPipeline pipeline,
		SuperBIContext db,
		ISqlDialectResolver dialectResolver,
		ISqlQueryBuilder sqlBuilder,
		IQueryExecutionService exec,
		IDataSourceAuthorizationService? dataSourceAuthorization = null,
		IRowLevelSecurityService? rowSecurity = null,
		IDataSourceExecutionIdentityAccessor? executionIdentity = null,
		IQueryPlanSecurityGate? securityGate = null,
		ISecretStore? secrets = null)
	{
		_understanding = understanding ?? throw new ArgumentNullException(nameof(understanding));
		_pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
		_db = db ?? throw new ArgumentNullException(nameof(db));
		_dialectResolver = dialectResolver ?? throw new ArgumentNullException(nameof(dialectResolver));
		_sqlBuilder = sqlBuilder ?? throw new ArgumentNullException(nameof(sqlBuilder));
		_exec = exec ?? throw new ArgumentNullException(nameof(exec));
		_dataSourceAuthorization = dataSourceAuthorization;
		_rowSecurity = rowSecurity;
		_executionIdentity = executionIdentity;
		_securityGate = securityGate;
		_secrets = secrets;
	}

	/// <inheritdoc />
	public async Task<WidgetDataResult> ResolveAsync(
		WidgetQueryDsl? query,
		PlatformContext context,
		IReadOnlyList<FilterDsl> effectiveFilters,
		CancellationToken cancellationToken = default)
	{
		var result = new WidgetDataResult();

		if (query is null)
		{
			result.Decision = "NoQuery";
			return result;
		}

		// 1. 解析允许的数据源集合（P0）。
		//    必须早于「查询理解」：理解阶段的 Metadata 上下文按该作用域收敛，
		//    否则全局 top-K 召回会把其他数据源的列写进提示词，污染维度解析。
		//    同时把「无授权 / 无执行身份」的阻断提前到 LLM 调用之前，省掉一次无效推理。
		IReadOnlyCollection<long>? allowedSources = null;
		var caller = _executionIdentity?.Current;
		if (_dataSourceAuthorization is not null)
		{
			if (caller is null || caller.TenantId != context.Tenant.TenantId)
			{
				result.Decision = "Blocked";
				result.Error = "当前账号缺少有效的数据源执行身份。";
				return result;
			}
			allowedSources = await _dataSourceAuthorization.GetAuthorizedDataSourceIdsAsync(context.Tenant.TenantId, caller.UserId, cancellationToken);
			if (allowedSources.Count == 0)
			{
				result.Decision = "Blocked";
				result.Error = "当前账号无权访问仪表盘数据源。";
				return result;
			}
		}

		// 2. 构造意图：优先自然语言问句；否则由显式指标/维度合成问句交给理解服务解析。
		QueryIntent intent;
		if (!string.IsNullOrWhiteSpace(query.Question))
		{
			intent = await _understanding.UnderstandAsync(query.Question, context, allowedSources);
		}
		else
		{
			intent = await _understanding.UnderstandAsync(BuildQuestion(query), context, allowedSources);
		}

		// 3. 全局/组件级筛选器下推到意图（P6.3 下推语义）。
		foreach (var f in effectiveFilters)
		{
			intent.Filters.Add(new QueryFilter
			{
				SemanticText = f.Field,
				Field = f.Field,
				Operator = f.Operator,
				Value = f.Value ?? string.Empty,
			});
		}

		// 4. 过 QueryPlanPipeline（语义验证 + 自动修复 + Confidence + Decision Gate）。
		var pipelineResult = await _pipeline.RunAsync(intent.OriginalQuestion, intent, authorizedDataSourceIds: allowedSources);
		if (pipelineResult.EarlyResponse is not null)
		{
			result.Decision = "Blocked";
			result.Resolved = false;
			result.Error = pipelineResult.EarlyResponse.ErrorMessage;
			return result;
		}

		var plan = pipelineResult.Plan;
		if (_rowSecurity is not null && allowedSources is not null && caller is not null)
		{
			plan.EffectiveTenantId = context.Tenant.TenantId;
			await _rowSecurity.ApplyAsync(plan, context.Tenant.TenantId, caller.UserId, cancellationToken);
			if (_securityGate is not null)
				await _securityGate.ValidateAsync(plan, context.Tenant.TenantId, caller.UserId, cancellationToken);
		}

		// 5. 数据源 → 方言 → SQL → 执行。
		try
		{
			var dataSource = await _db.DataSources
				.FirstAsync(x => x.Id == plan.DataSourceId &&
					(allowedSources == null || (x.TenantId == context.Tenant.TenantId && x.Enabled == true)), cancellationToken);
			var dialect = _dialectResolver.Resolve(dataSource.DbType ?? "sqlserver");
			// §10.5 #5：执行前跨 catalog 守卫（仅 PostgreSQL 生效；连接串无法解析时跳过）。
			if (_secrets is not null)
				QueryCatalogGuard.Assert(plan, dialect, _secrets.ResolvePlaintext(dataSource.ConnectionString));
			var sql = await _sqlBuilder.BuildAsync(plan, dialect);
			var data = await _exec.ExecuteAsync(sql, plan.DataSourceId);

			result.Resolved = data.Success;
			result.Error = data.ErrorMessage;
			result.Decision = "Proceed";
			if (data.Rows.Count > 0)
			{
				result.Columns = data.Rows[0].Keys.ToList();
			}

			result.Rows = data.Rows;
		}
		catch (Exception ex)
		{
			result.Decision = "Error";
			result.Resolved = false;
			result.Error = ex.Message;
		}

		return result;
	}

	private static string BuildQuestion(WidgetQueryDsl query)
	{
		var metrics = string.Join("、", query.Metrics.Select(m => m.Field));
		var dimensions = query.Dimensions.Count > 0
			? "，按 " + string.Join("、", query.Dimensions)
			: string.Empty;
		return string.IsNullOrWhiteSpace(metrics) ? "查询数据" : $"统计{metrics}{dimensions}";
	}
}
