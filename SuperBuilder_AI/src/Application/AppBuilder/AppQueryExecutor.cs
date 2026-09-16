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

		// ---------------------------------------------------------
		// 表锁定（M7-11 契约：应用是已固化的查询，表在导出时即确定）
		// ---------------------------------------------------------
		//
		// 语义检索是概率性的：对中文表注释稀疏的数据源会召回无关表，
		// 使 Semantic/Table 证据归零、Confidence 跌至 Low，最终被 Decision Gate 拒绝。
		// 应用运行必须确定性取数，因此当 binding 固化了 TableId 时，
		// 强制把 plan 收敛到该表（并剔除不属于它的字段/排序/筛选引用）。
		if (binding.TableId is { } lockedTableId && lockedTableId > 0)
		{
			await LockPlanToTableAsync(plan, lockedTableId, binding, cancellationToken);
		}

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

	/// <summary>
	/// 把 plan 收敛到 <paramref name="tableId"/> 指定的单张表，并以 binding 为权威重写列引用。
	///
	/// <para>M7-11：应用运行是确定性取数，表与字段在导出时已固化。语义检索可能召回无关表，
	/// 进而把列也落到那些表的同义列上（实测：排序被解析成旁表的 create_time 而非主表的 come_time）。
	/// 本方法据此把 plan 归还到正确表与正确列：</para>
	/// <list type="number">
	/// <item>保留锁定表（plan 中不存在则按元数据补齐），移除其余表及其 Join。</item>
	/// <item>把 Fields / Orders / Metrics / Dimensions 的列引用重映射到锁定表的同名列。</item>
	/// <item><strong>以 binding 为权威</strong>：binding 已固化的排序字段名 / 筛选字段名
	///       直接回写到 plan（它们来自导出时的真实 QueryPlan，是确定性事实）；
	///       plan 侧因选错表而产生的同义替换（如 create_time）在此被纠正。</item>
	/// </list>
	/// <para>无法在锁定表中找到的字段<strong>不静默丢弃</strong>，保留原值交由一致性断言拒绝。</para>
	/// </summary>
	private async Task LockPlanToTableAsync(
		QueryPlan plan, long tableId, AppDataSourceBinding binding, CancellationToken cancellationToken)
	{
		var lockedTable = plan.Tables.FirstOrDefault(t => t.MetadataTableId == tableId);
		if (lockedTable is null)
		{
			// plan 里没有该表：从元数据补齐（导出时已校验其属于绑定数据源）。
			var meta = await _db.MetadataTables.AsNoTracking()
				.FirstOrDefaultAsync(t => t.Id == tableId, cancellationToken);
			if (meta is null)
				throw SuperBuilderException.FromCode(ErrorCodes.AppBindingNotSupported, 422);

			lockedTable = new QueryTable
			{
				MetadataTableId = meta.Id,
				DataSourceId = meta.DataSourceId,
				TableName = meta.TableName,
				TableComment = meta.TableComment,
			};
		}

		// 1. 收敛表集合：只保留锁定表；多表（JOIN）本就是 §9 支持矩阵拒绝的形态。
		plan.Tables.Clear();
		plan.Tables.Add(lockedTable);
		plan.Joins.Clear();
		plan.DataSourceId = lockedTable.DataSourceId > 0 ? lockedTable.DataSourceId : plan.DataSourceId;

		// 2. 锁定表的列名 → 列 Id 映射。
		var lockedColumns = await _db.MetadataColumns.AsNoTracking()
			.Where(c => c.MetadataTableId == tableId)
			.Select(c => new { c.Id, c.ColumnName })
			.ToListAsync(cancellationToken);
		var columnIdByName = lockedColumns
			.GroupBy(c => c.ColumnName, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

		// 3. 字段投影：只保留锁定表确实存在的列（origin 表残留列不得进入 SQL）。
		plan.Fields.RemoveAll(f =>
			string.IsNullOrWhiteSpace(f.ColumnName)
			|| !columnIdByName.ContainsKey(f.ColumnName!));
		foreach (var f in plan.Fields)
		{
			f.MetadataColumnId = columnIdByName[f.ColumnName!];
			f.MetadataTableId = lockedTable.MetadataTableId;
			f.TableName = lockedTable.TableName;
		}

		// 4. 以 binding 为权威回写排序（binding 的字段名来自导出时的真实计划）。
		if (binding.Sort.Count > 0 && plan.Orders.Count > 0)
		{
			var bo = binding.Sort[0];
			if (columnIdByName.TryGetValue(bo.Field, out var sortColId))
			{
				plan.Orders[0].Field = bo.Field;
				plan.Orders[0].MetadataColumnId = sortColId;
				plan.Orders[0].MetadataTableId = lockedTable.MetadataTableId;
				plan.Orders[0].TableName = lockedTable.TableName;
				plan.Orders[0].Direction = string.Equals(bo.Direction, "DESC", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
			}
		}
		else
		{
			// 无 binding 排序约束时，剔除落到旁表的排序。
			plan.Orders.RemoveAll(o =>
				string.IsNullOrWhiteSpace(o.Field)
				|| !columnIdByName.ContainsKey(o.Field!));
		}

		// 5. 以 binding 为权威回写筛选字段名（值/操作符保持一致，仅纠正表归属）。
		for (var i = 0; i < plan.Filters.Count && i < binding.Filters.Count; i++)
		{
			var bf = binding.Filters[i];
			if (columnIdByName.ContainsKey(bf.Field))
			{
				plan.Filters[i].Field = bf.Field;
			}
		}
		plan.Filters.RemoveAll(f =>
			string.IsNullOrWhiteSpace(f.Field)
			|| !columnIdByName.ContainsKey(f.Field!));

		// 6. 维度：剔除不属于锁定表的维度（应用为单表明细/聚合，维度应属主表）。
		plan.Dimensions.RemoveAll(d =>
			!string.IsNullOrWhiteSpace(d.ColumnName)
			&& !columnIdByName.ContainsKey(d.ColumnName!));
		foreach (var d in plan.Dimensions)
		{
			if (!string.IsNullOrWhiteSpace(d.ColumnName)
				&& columnIdByName.TryGetValue(d.ColumnName!, out var dimColId))
			{
				d.MetadataColumnId = dimColId;
			}
		}
	}

	/// <summary>由 binding 构造结构化 QueryIntent（绕过 NLU）。</summary>
	private static QueryIntent BuildIntent(AppDataSourceBinding binding)
	{
		var entity = binding.Entity ?? string.Empty;
		var intent = new QueryIntent
		{
			OriginalQuestion = entity,
			IntentType = binding.Metrics.Count > 0 ? "Aggregate" : "Detail",
			Limit = binding.Limit,
		};

		intent.BusinessEntityHints.Add(new BusinessEntityHint { Name = entity, Confidence = 1 });
		foreach (var m in binding.Metrics)
		{
			var field = m.Field ?? string.Empty;
			intent.Metrics.Add(new QueryMetric { Name = field, Field = field, Aggregation = ToQueryAggregation(m.Aggregation) });
		}
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

	/// <summary>
	/// 一致性断言：Pipeline 产出必须与 binding 描述一致，否则 422。
	///
	/// <para>失败原因写入异常的 InnerException，便于服务端日志定位到<strong>具体是哪一项</strong>
	/// 偏离（数据源 / 实体 / 聚合 / 指标 / 维度 / 筛选 / 排序 / 上限），
	/// 对外仍是统一的 <c>SB_APP_002</c> 文案，不泄露内部结构。</para>
	/// </summary>
	private static void AssertConsistentWithBinding(QueryPlan plan, AppDataSourceBinding binding)
	{
		static SuperBuilderException Fail(string why) =>
			SuperBuilderException.FromCode(ErrorCodes.AppBindingNotSupported, 422, new InvalidOperationException(why));

		if (plan.DataSourceId != binding.DataSourceId)
			throw Fail($"数据源不一致：plan={plan.DataSourceId}，binding={binding.DataSourceId}。");
		if (plan.Distinct)
			throw Fail("应用绑定不支持 Distinct。");

		var mainTable = plan.Tables.FirstOrDefault()
			?? throw Fail("查询计划没有可用的目标表。");
		// 实体标识可能是语义文本 / 表注释 / 物理表名（见 AppQueryBindingExporter），三者任一匹配即可。
		var entityMatch = string.Equals(mainTable.SemanticText, binding.Entity, StringComparison.OrdinalIgnoreCase)
		                  || string.Equals(mainTable.TableComment, binding.Entity, StringComparison.OrdinalIgnoreCase)
		                  || string.Equals(mainTable.TableName, binding.Entity, StringComparison.OrdinalIgnoreCase);
		if (!entityMatch)
			throw Fail(
				$"目标表与绑定实体不一致：binding={binding.Entity}，" +
				$"plan={mainTable.TableName}/{mainTable.TableComment}/{mainTable.SemanticText}（TableId={mainTable.MetadataTableId}）。");

		if (plan.Limit != binding.Limit)
			throw Fail($"Limit 不一致：plan={plan.Limit}，binding={binding.Limit}。");

		// 聚合模式一致。
		var expectAggregate = binding.Metrics.Count > 0;
		if (plan.IsAggregate != expectAggregate)
			throw Fail($"聚合模式不一致：plan={plan.IsAggregate}，binding 期望={expectAggregate}。");

		// 指标一致（字段 + 聚合）。
		if (plan.Metrics.Count != binding.Metrics.Count)
			throw Fail($"指标数量不一致：plan={plan.Metrics.Count}，binding={binding.Metrics.Count}。");
		for (var i = 0; i < binding.Metrics.Count; i++)
		{
			if (!string.Equals(plan.Metrics[i].Field, binding.Metrics[i].Field, StringComparison.OrdinalIgnoreCase)
			    || !string.Equals(plan.Metrics[i].Aggregation, ToQueryAggregation(binding.Metrics[i].Aggregation), StringComparison.OrdinalIgnoreCase))
				throw Fail(
					$"指标[{i}]不一致：plan={plan.Metrics[i].Field}/{plan.Metrics[i].Aggregation}，" +
					$"binding={binding.Metrics[i].Field}/{ToQueryAggregation(binding.Metrics[i].Aggregation)}。");
		}

		// 维度集合一致（语义名，无序）。
		var planDims = plan.Dimensions.Select(d => d.SemanticText).ToHashSet(StringComparer.OrdinalIgnoreCase);
		if (planDims.Count != binding.Dimensions.Count || !binding.Dimensions.All(planDims.Contains))
			throw Fail($"维度集合不一致：plan=[{string.Join(",", planDims)}]，binding=[{string.Join(",", binding.Dimensions)}]。");

		// 筛选一致（字段 + 操作符 + 值）。
		if (plan.Filters.Count != binding.Filters.Count)
			throw Fail(
				$"筛选数量不一致：plan={plan.Filters.Count}（[{string.Join(",", plan.Filters.Select(f => f.Field))}]），" +
				$"binding={binding.Filters.Count}。");
		for (var i = 0; i < binding.Filters.Count; i++)
		{
			var bf = binding.Filters[i];
			var pf = plan.Filters[i];
			if (!string.Equals(pf.Field, bf.Field, StringComparison.OrdinalIgnoreCase)
			    || !string.Equals(pf.Operator, ToQueryOperator(bf.Operator), StringComparison.OrdinalIgnoreCase)
			    || !string.Equals(pf.Value, bf.Value, StringComparison.OrdinalIgnoreCase))
				throw Fail(
					$"筛选[{i}]不一致：plan={pf.Field}/{pf.Operator}/{pf.Value}，" +
					$"binding={bf.Field}/{ToQueryOperator(bf.Operator)}/{bf.Value}。");
		}

		// 排序一致（单字段）。
		if (plan.Orders.Count != binding.Sort.Count)
			throw Fail(
				$"排序数量不一致：plan={plan.Orders.Count}（[{string.Join(",", plan.Orders.Select(o => o.Field + ":" + o.Direction))}]），" +
				$"binding={binding.Sort.Count}。");
		if (binding.Sort.Count == 1)
		{
			var bo = binding.Sort[0];
			var po = plan.Orders[0];
			if (!string.Equals(po.Field, bo.Field, StringComparison.OrdinalIgnoreCase)
			    || !string.Equals(po.Direction, bo.Direction, StringComparison.OrdinalIgnoreCase))
				throw Fail($"排序不一致：plan={po.Field}:{po.Direction}，binding={bo.Field}:{bo.Direction}。");
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
