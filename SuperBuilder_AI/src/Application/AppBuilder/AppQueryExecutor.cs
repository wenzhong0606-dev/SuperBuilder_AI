using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.AppBuilder;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Interfaces.Database;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.AppBuilder;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.AppBuilder;

/// <summary>
/// M7-11：应用组件确定性取数执行器（C2）。
///
/// <para>与 <see cref="BIConversationService"/> 的差别：</para>
/// <list type="bullet">
/// <item>直接由 <see cref="AppDataSourceBinding"/> 构造 <see cref="QueryIntent"/>，<strong>绕过 NLU 理解</strong>，
/// 调用 <see cref="IQueryPlanPipeline"/>（不调用 QueryUnderstanding）。</item>
/// <item>以 binding.DataSourceId 作为 <c>requestedDataSourceId</c> 硬约束，单源在选表前生效。</item>
/// <item>复用当前访问者安全链路：数据源授权 → 执行身份租户校验 → RLS → SecurityGate，
/// 且这些依赖为<strong>必需</strong>（App 路径缺依赖即拒绝执行）。</item>
/// <item>Pipeline 产出后做<strong>一致性断言</strong>（数据源/实体/字段/聚合/筛选/排序/Limit/模式），不符即 422，
/// 防止运行时选错表或丢失原查询条件。</item>
/// </list>
///
/// <para>安全红线：绑定源未授权即使访问者有其他授权源也必须拒绝；安全拒绝后 SQL 构建/执行调用次数为零。</para>
/// </summary>
public sealed class AppQueryExecutor : IAppQueryExecutor
{
	private readonly IQueryPlanPipeline _queryPlanPipeline;
	private readonly ISqlQueryBuilder _sqlQueryBuilder;
	private readonly ISqlDialectResolver _sqlDialectResolver;
	private readonly IQueryExecutionService _queryExecutionService;
	private readonly SuperBIContext _db;
	private readonly IDataSourceAuthorizationService _dataSourceAuth;
	private readonly IRowLevelSecurityService _rowSecurity;
	private readonly IDataSourceExecutionIdentityAccessor _executionIdentity;
	private readonly IQueryPlanSecurityGate _securityGate;

	public AppQueryExecutor(
		IQueryPlanPipeline queryPlanPipeline,
		ISqlQueryBuilder sqlQueryBuilder,
		ISqlDialectResolver sqlDialectResolver,
		IQueryExecutionService queryExecutionService,
		SuperBIContext db,
		IDataSourceAuthorizationService dataSourceAuth,
		IRowLevelSecurityService rowSecurity,
		IDataSourceExecutionIdentityAccessor executionIdentity,
		IQueryPlanSecurityGate securityGate)
	{
		_queryPlanPipeline = queryPlanPipeline;
		_sqlQueryBuilder = sqlQueryBuilder;
		_sqlDialectResolver = sqlDialectResolver;
		_queryExecutionService = queryExecutionService;
		_db = db;
		_dataSourceAuth = dataSourceAuth;
		_rowSecurity = rowSecurity;
		_executionIdentity = executionIdentity;
		_securityGate = securityGate;
	}

	public async Task<AppComponentRender> ExecuteComponentAsync(
		AppDataSourceBinding binding, long tenantId, long userId, CancellationToken cancellationToken = default)
	{
		var authorized = await _dataSourceAuth.GetAuthorizedDataSourceIdsAsync(tenantId, userId, cancellationToken);
		if (authorized.Count == 0)
			throw SuperBuilderException.FromCode(ErrorCodes.RowPolicyForbidden, 403);
		if (binding.DataSourceId is not { } requestedDs || !authorized.Contains(requestedDs))
			throw SuperBuilderException.FromCode(ErrorCodes.DataSourceForbidden, 403);

		// 设置当前访问者执行身份，供 RLS / SecurityGate 使用。
		_executionIdentity.Current = new DataSourceExecutionIdentity(tenantId, userId);

		var intent = BuildIntent(binding);
		var result = await _queryPlanPipeline.RunAsync(intent.OriginalQuestion, intent, requestedDs, authorized);
		if (result.EarlyResponse is not null)
		{
			return new AppComponentRender
			{
				Succeeded = false,
				ErrorCode = ErrorCodes.AppBindingNotSupported,
				ErrorMessage = result.EarlyResponse.ErrorMessage ?? "查询未通过校验或决策门禁。",
			};
		}

		var plan = result.Plan;
		AssertConsistentWithBinding(plan, binding);

		plan.EffectiveTenantId = tenantId;
		await _rowSecurity.ApplyAsync(plan, tenantId, userId);
		await _securityGate.ValidateAsync(plan, tenantId, userId);

		var dataSource = await _db.DataSources
			.FirstAsync(x => x.Id == plan.DataSourceId && x.TenantId == tenantId && x.Enabled == true, cancellationToken);
		var dialect = _sqlDialectResolver.Resolve(dataSource.DbType);
		var sql = await _sqlQueryBuilder.BuildAsync(plan, dialect);
		var data = await _queryExecutionService.ExecuteAsync(sql, plan.DataSourceId);

		return ToComponent(binding, plan, data);
	}

	/// <summary>由 binding 构造结构化 QueryIntent（绕过 NLU）。</summary>
	private static QueryIntent BuildIntent(AppDataSourceBinding binding)
	{
		var intent = new QueryIntent
		{
			OriginalQuestion = binding.Entity,
			IntentType = binding.Metrics.Count > 0 ? "Aggregate" : "Detail",
			Limit = binding.Limit,
		};

		intent.BusinessEntityHints.Add(new BusinessEntityHint { Name = binding.Entity, Confidence = 1 });
		foreach (var m in binding.Metrics)
			intent.Metrics.Add(new QueryMetric { Name = m.Field, Field = m.Field, Aggregation = ToQueryAggregation(m.Aggregation) });
		foreach (var d in binding.Dimensions)
			intent.Dimensions.Add(d);
		foreach (var f in binding.Filters)
			intent.Filters.Add(new QueryFilter
			{
				Field = f.Field,
				Operator = ToQueryOperator(f.Operator),
				Value = f.Value ?? string.Empty,
			});
		if (binding.Sort.Count > 0)
		{
			var s = binding.Sort[0];
			intent.OrderBy = s.Field;
			intent.OrderDirection = string.Equals(s.Direction, "DESC", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
		}
		return intent;
	}

	/// <summary>一致性断言：Pipeline 产出必须与 binding 描述一致，否则 422。</summary>
	private static void AssertConsistentWithBinding(QueryPlan plan, AppDataSourceBinding binding)
	{
		if (plan.DataSourceId != binding.DataSourceId)
			throw SuperBuilderException.FromCode(ErrorCodes.AppBindingNotSupported, 422);
		if (plan.Distinct)
			throw SuperBuilderException.FromCode(ErrorCodes.AppBindingNotSupported, 422);

		var mainTable = plan.Tables.FirstOrDefault()
			?? throw SuperBuilderException.FromCode(ErrorCodes.AppBindingNotSupported, 422);
		var entityMatch = string.Equals(mainTable.SemanticText, binding.Entity, StringComparison.OrdinalIgnoreCase)
		                  || string.Equals(mainTable.TableName, binding.Entity, StringComparison.OrdinalIgnoreCase);
		if (!entityMatch)
			throw SuperBuilderException.FromCode(ErrorCodes.AppBindingNotSupported, 422);

		if (plan.Limit != binding.Limit)
			throw SuperBuilderException.FromCode(ErrorCodes.AppBindingNotSupported, 422);

		// 聚合模式一致。
		var expectAggregate = binding.Metrics.Count > 0;
		if (plan.IsAggregate != expectAggregate)
			throw SuperBuilderException.FromCode(ErrorCodes.AppBindingNotSupported, 422);

		// 指标一致（字段 + 聚合）。
		if (plan.Metrics.Count != binding.Metrics.Count)
			throw SuperBuilderException.FromCode(ErrorCodes.AppBindingNotSupported, 422);
		for (var i = 0; i < binding.Metrics.Count; i++)
		{
			if (!string.Equals(plan.Metrics[i].Field, binding.Metrics[i].Field, StringComparison.OrdinalIgnoreCase)
			    || !string.Equals(plan.Metrics[i].Aggregation, ToQueryAggregation(binding.Metrics[i].Aggregation), StringComparison.OrdinalIgnoreCase))
				throw SuperBuilderException.FromCode(ErrorCodes.AppBindingNotSupported, 422);
		}

		// 维度集合一致（语义名，无序）。
		var planDims = plan.Dimensions.Select(d => d.SemanticText).ToHashSet(StringComparer.OrdinalIgnoreCase);
		if (planDims.Count != binding.Dimensions.Count || !binding.Dimensions.All(planDims.Contains))
			throw SuperBuilderException.FromCode(ErrorCodes.AppBindingNotSupported, 422);

		// 筛选一致（字段 + 操作符 + 值）。
		if (plan.Filters.Count != binding.Filters.Count)
			throw SuperBuilderException.FromCode(ErrorCodes.AppBindingNotSupported, 422);
		for (var i = 0; i < binding.Filters.Count; i++)
		{
			var bf = binding.Filters[i];
			var pf = plan.Filters[i];
			if (!string.Equals(pf.Field, bf.Field, StringComparison.OrdinalIgnoreCase)
			    || !string.Equals(pf.Operator, ToQueryOperator(bf.Operator), StringComparison.OrdinalIgnoreCase)
			    || !string.Equals(pf.Value, bf.Value, StringComparison.OrdinalIgnoreCase))
				throw SuperBuilderException.FromCode(ErrorCodes.AppBindingNotSupported, 422);
		}

		// 排序一致（单字段）。
		if (plan.Orders.Count != binding.Sort.Count)
			throw SuperBuilderException.FromCode(ErrorCodes.AppBindingNotSupported, 422);
		if (binding.Sort.Count == 1)
		{
			var bo = binding.Sort[0];
			var po = plan.Orders[0];
			if (!string.Equals(po.Field, bo.Field, StringComparison.OrdinalIgnoreCase)
			    || !string.Equals(po.Direction, bo.Direction, StringComparison.OrdinalIgnoreCase))
				throw SuperBuilderException.FromCode(ErrorCodes.AppBindingNotSupported, 422);
		}
	}

	private static AppComponentRender ToComponent(AppDataSourceBinding binding, QueryPlan plan, QueryResult data)
	{
		var component = new AppComponentRender
		{
			Succeeded = data.Success,
			Data = data.Rows,
		};
		if (!data.Success)
		{
			component.ErrorCode = ErrorCodes.AppQueryFailed;
			component.ErrorMessage = ErrorCodes.Message(ErrorCodes.AppQueryFailed);
			return component;
		}

		// 列元数据：明细用 Fields；聚合用 维度 + 指标。
		if (plan.IsAggregate)
		{
			foreach (var d in plan.Dimensions)
				component.Columns.Add(new AppColumnSpec { Name = d.ColumnName ?? d.SemanticText, DisplayName = d.SemanticText });
			foreach (var m in plan.Metrics)
			{
				component.Columns.Add(new AppColumnSpec
				{
					Name = m.Alias ?? m.Field,
					DisplayName = m.SemanticText,
					Type = "number",
				});
				component.Series.Add(new AppSeriesSpec { Name = m.Alias ?? m.Field, DisplayName = m.SemanticText });
			}
		}
		else
		{
			foreach (var f in plan.Fields)
				component.Columns.Add(new AppColumnSpec { Name = f.ColumnName ?? string.Empty, DisplayName = f.ColumnName, Type = f.DataType });
		}
		return component;
	}

	private static string ToQueryAggregation(string agg) =>
		agg.Trim().ToLowerInvariant() switch
		{
			"sum" => "SUM",
			"count" => "COUNT",
			"avg" => "AVG",
			"max" => "MAX",
			"min" => "MIN",
			_ => "NONE",
		};

	private static string ToQueryOperator(string op) =>
		op.Trim().ToLowerInvariant() switch
		{
			"eq" => "=",
			"neq" => "!=",
			"gt" => ">",
			"lt" => "<",
			"in" => "IN",
			"like" => "LIKE",
			_ => "=",
		};
}
