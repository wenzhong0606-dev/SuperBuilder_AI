using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Api.Errors;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Infrastructure.Database;
using SuperBuilder_AI.Infrastructure.Persistence;
using SuperBuilder_AI.Infrastructure.Security;
using SuperBuilder_AI.Interfaces.AppBuilder;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Interfaces.Database;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.AppBuilder;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Services.AppBuilder;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M7-11 C1/C2 验证：固定首用例「单表明细 + 单字段倒序 + Limit=10 + 一个 eq 筛选」。
/// 覆盖：原查询语义保留（导出绑定字段无丢失，尤其 eq 筛选值）、不同访问者按各自权限取数、
/// 一致性断言拒绝偏差、EarlyResponse 安全短路、未授权数据源拒绝。
/// 使用 SQLite 内存库 + 手写桩（项目未引入 Moq）。
/// </summary>
public sealed class M7_11_AppRuntimeTests
{
	private const long Tenant1 = 1;
	private const long User1 = 11;
	private const long User2 = 22;
	private const long Ds1 = 1;

	// ── C1：导出器固定首用例 ───────────────────────────────────────────────

	[Fact]
	public async Task C1_Export_FixedFirstUseCase_PreservesSemantics()
	{
		var (ctx, conn) = NewContext();
		await using var _1 = conn;
		await using var _2 = ctx;

		var plan = DetailPlan(
			dataSourceId: Ds1,
			entity: "sales_order",
			eqField: "status", eqValue: "completed",
			orderField: "created_at", orderDir: "DESC",
			limit: 10);
		SeedSnapshot(ctx, turnId: "t1", tenantId: Tenant1, userId: User1, plan);

		var exporter = new AppQueryBindingExporter(new AskQuerySnapshotStore(ctx));
		var binding = await exporter.ExportAsync("t1", Tenant1, User1, CancellationToken.None);

		Assert.Equal("sales_order", binding.Entity);
		Assert.Equal(Ds1, binding.DataSourceId);
		Assert.Empty(binding.Metrics);
		Assert.Empty(binding.Dimensions);
		Assert.Equal(10, binding.Limit);

		// 关键：eq 筛选值必须被保留（修复前 MapFilterOperator 吞掉值 → 一致性断言 422）。
		Assert.Single(binding.Filters);
		Assert.Equal("status", binding.Filters[0].Field);
		Assert.Equal(AppFilterOperators.Eq, binding.Filters[0].Operator);
		Assert.Equal("completed", binding.Filters[0].Value);

		Assert.Single(binding.Sort);
		Assert.Equal("created_at", binding.Sort[0].Field);
		Assert.Equal("DESC", binding.Sort[0].Direction);
	}

	[Fact]
	public async Task C1_Export_UnknownTurnId_Returns404()
	{
		var (ctx, conn) = NewContext();
		await using var _1 = conn;
		await using var _2 = ctx;
		var exporter = new AppQueryBindingExporter(new AskQuerySnapshotStore(ctx));

		var ex = await Assert.ThrowsAsync<SuperBuilderException>(
			() => exporter.ExportAsync("missing", Tenant1, User1, CancellationToken.None));
		Assert.Equal(ErrorCodes.AppSnapshotNotFound, ex.ErrorCode);
		Assert.Equal(404, ex.StatusCode);
	}

	[Fact]
	public async Task C1_Export_CrossUser_Forbidden403()
	{
		var (ctx, conn) = NewContext();
		await using var _1 = conn;
		await using var _2 = ctx;
		SeedSnapshot(ctx, "t1", Tenant1, User1, DetailPlan(Ds1, "sales_order", "status", "completed", "created_at", "DESC", 10));

		var exporter = new AppQueryBindingExporter(new AskQuerySnapshotStore(ctx));
		// 同一租户、不同用户 → 403。
		var ex = await Assert.ThrowsAsync<SuperBuilderException>(
			() => exporter.ExportAsync("t1", Tenant1, User2, CancellationToken.None));
		Assert.Equal(ErrorCodes.AppSnapshotForbidden, ex.ErrorCode);
		Assert.Equal(403, ex.StatusCode);
	}

	[Fact]
	public async Task C1_Export_JoinPlan_Rejected422()
	{
		var (ctx, conn) = NewContext();
		await using var _1 = conn;
		await using var _2 = ctx;
		var plan = DetailPlan(Ds1, "sales_order", "status", "completed", "created_at", "DESC", 10);
		plan.Joins.Add(new QueryJoin());
		SeedSnapshot(ctx, "t1", Tenant1, User1, plan);

		var exporter = new AppQueryBindingExporter(new AskQuerySnapshotStore(ctx));
		var ex = await Assert.ThrowsAsync<SuperBuilderException>(
			() => exporter.ExportAsync("t1", Tenant1, User1, CancellationToken.None));
		Assert.Equal(ErrorCodes.AppBindingNotSupported, ex.ErrorCode);
		Assert.Equal(422, ex.StatusCode);
	}

	[Fact]
	public async Task C1_Export_DistinctPlan_Rejected422()
	{
		var (ctx, conn) = NewContext();
		await using var _1 = conn;
		await using var _2 = ctx;
		var plan = DetailPlan(Ds1, "sales_order", "status", "completed", "created_at", "DESC", 10);
		plan.Distinct = true;
		SeedSnapshot(ctx, "t1", Tenant1, User1, plan);

		var exporter = new AppQueryBindingExporter(new AskQuerySnapshotStore(ctx));
		var ex = await Assert.ThrowsAsync<SuperBuilderException>(
			() => exporter.ExportAsync("t1", Tenant1, User1, CancellationToken.None));
		Assert.Equal(ErrorCodes.AppBindingNotSupported, ex.ErrorCode);
		Assert.Equal(422, ex.StatusCode);
	}

	[Fact]
	public async Task C1_Export_InOperator_Rejected422()
	{
		var (ctx, conn) = NewContext();
		await using var _1 = conn;
		await using var _2 = ctx;
		var plan = DetailPlan(Ds1, "sales_order", null, null, "created_at", "DESC", 10);
		plan.Filters.Add(new QueryFilter { Field = "region", Operator = "IN", Value = "[\"east\",\"west\"]" });
		SeedSnapshot(ctx, "t1", Tenant1, User1, plan);

		var exporter = new AppQueryBindingExporter(new AskQuerySnapshotStore(ctx));
		var ex = await Assert.ThrowsAsync<SuperBuilderException>(
			() => exporter.ExportAsync("t1", Tenant1, User1, CancellationToken.None));
		Assert.Equal(ErrorCodes.AppBindingNotSupported, ex.ErrorCode);
		Assert.Equal(422, ex.StatusCode);
	}

	// ── C2：确定性执行器（绕过 NLU）────────────────────────────────────────

	[Fact]
	public async Task C2_Execute_NoAuthorizedDataSource_Throws403()
	{
		var (ctx, conn) = NewContext();
		await using var _1 = conn;
		await using var _2 = ctx;
		var executor = BuildExecutor(ctx, authorizedDs: Array.Empty<long>());

		var ex = await Assert.ThrowsAsync<SuperBuilderException>(
			() => executor.ExecuteComponentAsync(FixedBinding(), Tenant1, User1, CancellationToken.None));
		Assert.Equal(ErrorCodes.RowPolicyForbidden, ex.ErrorCode);
		Assert.Equal(403, ex.StatusCode);
	}

	[Fact]
	public async Task C2_Execute_BindingSourceNotAuthorized_Throws403()
	{
		var (ctx, conn) = NewContext();
		await using var _1 = conn;
		await using var _2 = ctx;
		// 访问者仅授权 Ds=2，绑定要求 Ds=1 → 拒绝。
		var executor = BuildExecutor(ctx, authorizedDs: new[] { 2L });

		var ex = await Assert.ThrowsAsync<SuperBuilderException>(
			() => executor.ExecuteComponentAsync(FixedBinding(), Tenant1, User1, CancellationToken.None));
		Assert.Equal(ErrorCodes.DataSourceForbidden, ex.ErrorCode);
		Assert.Equal(403, ex.StatusCode);
	}

	[Fact]
	public async Task C2_Execute_PipelineEarlyResponse_ReturnsFailedComponent()
	{
		var (ctx, conn) = NewContext();
		await using var _1 = conn;
		await using var _2 = ctx;
		var executor = BuildExecutor(ctx, authorizedDs: new[] { Ds1 }, earlyResponse: true);

		var render = await executor.ExecuteComponentAsync(FixedBinding(), Tenant1, User1, CancellationToken.None);
		Assert.False(render.Succeeded);
		Assert.Equal(ErrorCodes.AppBindingNotSupported, render.ErrorCode);
	}

	[Fact]
	public async Task C2_Execute_ConsistencyMismatch_Throws422()
	{
		var (ctx, conn) = NewContext();
		await using var _1 = conn;
		await using var _2 = ctx;
		// 管线返回与绑定不符的计划（实体不同）→ 一致性断言 422。
		var executor = BuildExecutor(ctx, authorizedDs: new[] { Ds1 }, planEntityOverride: "other_table");

		var ex = await Assert.ThrowsAsync<SuperBuilderException>(
			() => executor.ExecuteComponentAsync(FixedBinding(), Tenant1, User1, CancellationToken.None));
		Assert.Equal(ErrorCodes.AppBindingNotSupported, ex.ErrorCode);
		Assert.Equal(422, ex.StatusCode);
	}

	[Fact]
	public async Task C2_Execute_HappyPath_ReturnsData_AndSetsExecutionIdentity()
	{
		var (ctx, conn) = NewContext();
		await using var _1 = conn;
		await using var _2 = ctx;

		// 种子数据源（执行器在一致性通过后读取）。
		ctx.DataSources.Add(new SuperBuilder_AI.Models.Metadata.DataSource
		{
			Id = Ds1,
			TenantId = Tenant1,
			Name = "WMS",
			NormalizedName = "WMS",
			DbType = "SQLSERVER",
			Enabled = true,
		});
		await ctx.SaveChangesAsync();

		var accessor = new FakeExecutionIdentityAccessor();
		var executor = BuildExecutor(ctx, authorizedDs: new[] { Ds1 }, accessor: accessor);

		var render = await executor.ExecuteComponentAsync(FixedBinding(), Tenant1, User1, CancellationToken.None);

		Assert.True(render.Succeeded);
		// 明细路径：列来自 plan.Fields（ColumnName=status），证明语义字段被正确映射。
		Assert.Equal("status", render.Columns.First().Name);
		Assert.Single(render.Data);
		Assert.Equal("completed", render.Data[0]["status"]);
		// 当前访问者执行身份已被写入，供 RLS / SecurityGate 使用。
		Assert.NotNull(accessor.Current);
		Assert.Equal(Tenant1, accessor.Current!.TenantId);
		Assert.Equal(User1, accessor.Current.UserId);
	}

	// ── 助手 ─────────────────────────────────────────────────────────────

	private static (SuperBIContext Ctx, SqliteConnection Conn) NewContext()
	{
		var conn = new SqliteConnection("DataSource=:memory:");
		conn.Open();
		// 内存库关闭外键强制：测试仅关注 M7-11 路径，避免为 DataSource 等补齐全部父级（Tenant…）实体。
		using (var pragma = conn.CreateCommand())
		{
			pragma.CommandText = "PRAGMA foreign_keys = OFF;";
			pragma.ExecuteNonQuery();
		}
		var ctx = new SuperBIContext(new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(conn).Options);
		ctx.Database.EnsureCreated();
		return (ctx, conn);
	}

	private static QueryPlan DetailPlan(
		long dataSourceId, string entity,
		string? eqField, string? eqValue,
		string orderField, string orderDir, int limit)
	{
		var plan = new QueryPlan
		{
			DataSourceId = dataSourceId,
			IsAggregate = false,
			Distinct = false,
			Limit = limit,
		};
		plan.Tables.Add(new QueryTable { SemanticText = entity, TableName = entity, MetadataTableId = 1 });
		plan.Fields.Add(new QueryField { ColumnName = "status", DataType = "string" });
		plan.Orders.Add(new QueryOrder { Field = orderField, Direction = orderDir });
		if (eqField is not null)
			plan.Filters.Add(new QueryFilter { Field = eqField, Operator = "=", Value = eqValue! });
		return plan;
	}

	private static void SeedSnapshot(SuperBIContext ctx, string turnId, long tenantId, long userId, QueryPlan plan)
	{
		ctx.AskQuerySnapshots.Add(new AskQuerySnapshot
		{
			TurnId = turnId,
			TenantId = tenantId,
			UserId = userId,
			DataSourceId = plan.DataSourceId,
			EntityCode = plan.Tables.FirstOrDefault()?.SemanticText,
			QueryPlanJson = System.Text.Json.JsonSerializer.Serialize(plan),
			RequestHash = "h",
			ExpiresAt = DateTime.UtcNow.AddHours(1),
		});
		ctx.SaveChanges();
	}

	private static AppDataSourceBinding FixedBinding() =>
		new()
		{
			Entity = "sales_order",
			DataSourceId = Ds1,
			Limit = 10,
			Filters = { new AppFilterBinding { Field = "status", Operator = AppFilterOperators.Eq, Value = "completed" } },
			Sort = { new AppSortBinding { Field = "created_at", Direction = "DESC" } },
		};

	private static AppQueryExecutor BuildExecutor(
		SuperBIContext ctx,
		long[] authorizedDs,
		bool earlyResponse = false,
		string? planEntityOverride = null,
		FakeExecutionIdentityAccessor? accessor = null)
	{
		var pipeline = new FakePipeline(authorizedDs, earlyResponse, planEntityOverride);
		var auth = new FakeDataSourceAuth(authorizedDs);
		var rls = new FakeRowSecurity();
		var gate = new FakeSecurityGate();
		var resolver = new FakeDialectResolver();
		var sqlBuilder = new FakeSqlBuilder();
		var exec = new FakeExec();
		accessor ??= new FakeExecutionIdentityAccessor();
		return new AppQueryExecutor(pipeline, sqlBuilder, resolver, exec, ctx, auth, rls, accessor, gate, new FakeSecretStore());
	}

	// ── 桩实现 ───────────────────────────────────────────────────────────

	private sealed class FakeDataSourceAuth : IDataSourceAuthorizationService
	{
		private readonly IReadOnlyList<long> _ds;
		public FakeDataSourceAuth(long[] ds) => _ds = ds;
		public Task<IReadOnlyList<long>> GetAuthorizedDataSourceIdsAsync(long tenantId, long userId, CancellationToken ct = default)
			=> Task.FromResult(_ds);
		public Task<bool> IsAuthorizedAsync(long tenantId, long userId, long dataSourceId, CancellationToken ct = default)
			=> Task.FromResult(_ds.Contains(dataSourceId));
		public Task GrantAsync(long tenantId, long dataSourceId, DataSourceGrantSubjectType subjectType, long subjectId, CancellationToken ct = default) => Task.CompletedTask;
		public Task RevokeAsync(long tenantId, long dataSourceId, DataSourceGrantSubjectType subjectType, long subjectId, CancellationToken ct = default) => Task.CompletedTask;
		public Task RevokeBySubjectAsync(long tenantId, DataSourceGrantSubjectType subjectType, long subjectId, CancellationToken ct = default) => Task.CompletedTask;
		public Task<IReadOnlyList<DataSourceAccessGrant>> DetectOrphanGrantsAsync(long tenantId, CancellationToken ct = default)
			=> Task.FromResult<IReadOnlyList<DataSourceAccessGrant>>(Array.Empty<DataSourceAccessGrant>());
	}

	private sealed class FakeExecutionIdentityAccessor : IDataSourceExecutionIdentityAccessor
	{
		public DataSourceExecutionIdentity? Current { get; set; }
	}

	private sealed class FakeRowSecurity : IRowLevelSecurityService
	{
		public Task<string> GetPolicyFingerprintAsync(long tenantId, long userId, CancellationToken ct = default) => Task.FromResult("fp");
		public Task ApplyAsync(QueryPlan plan, long tenantId, long userId, CancellationToken ct = default) => Task.CompletedTask;
	}

	private sealed class FakeSecurityGate : IQueryPlanSecurityGate
	{
		public Task ValidateAsync(QueryPlan plan, long tenantId, long userId, CancellationToken ct = default) => Task.CompletedTask;
	}

	private sealed class FakeDialectResolver : ISqlDialectResolver
	{
		public ISqlDialect Resolve(string dbType) => new FakeDialect();
	}

	private sealed class FakeDialect : ISqlDialect
	{
		public string Name => "SQLSERVER";
		public string EscapeIdentifier(string identifier) => "[" + identifier + "]";
		public string ApplyLimit(string sql, int limit) => sql + " LIMIT " + limit;
		public string GetParameterName(int index) => "@p" + index;
		public string QualifyTable(string? catalog, string? schema, string table)
		{
			var parts = new List<string>();
			if (!string.IsNullOrWhiteSpace(catalog)) parts.Add(EscapeIdentifier(catalog));
			if (!string.IsNullOrWhiteSpace(schema)) parts.Add(EscapeIdentifier(schema));
			parts.Add(EscapeIdentifier(table));
			return string.Join(".", parts);
		}
		public void AssertCatalogResolvable(string? catalog, string? currentCatalog) { }
	}

	private sealed class FakeSecretStore : ISecretStore
	{
		public string Protect(string plaintext) => plaintext;
		public string Unprotect(string ciphertext) => ciphertext;
	}

	private sealed class FakeSqlBuilder : ISqlQueryBuilder
	{
		public Task<SqlQuery> BuildAsync(QueryPlan plan, ISqlDialect dialect)
			=> Task.FromResult(new SqlQuery { Sql = "SELECT 1" });
	}

	private sealed class FakeExec : IQueryExecutionService
	{
		public Task<QueryResult> ExecuteAsync(SqlQuery query, long dataSourceId)
			=> Task.FromResult(new QueryResult
			{
				Success = true,
				Rows = new List<Dictionary<string, object?>>
				{
					new() { ["status"] = "completed" },
				},
			});
	}

	private sealed class FakePipeline : IQueryPlanPipeline
	{
		private readonly long[] _authorized;
		private readonly bool _early;
		private readonly string? _entityOverride;

		public FakePipeline(long[] authorized, bool early, string? entityOverride)
		{
			_authorized = authorized;
			_early = early;
			_entityOverride = entityOverride;
		}

		public Task<QueryPlanPipelineResult> RunAsync(
			string question, QueryIntent intent, long? requestedDataSourceId = null, IReadOnlyCollection<long>? authorizedDataSourceIds = null)
		{
			if (_early)
				return Task.FromResult(new QueryPlanPipelineResult
				{
					EarlyResponse = new BIResponse { ErrorMessage = "blocked" },
				});

			// 由 intent 重建与绑定一致的计划（绕过 NLU，等价于确定性分支）。
			var plan = new QueryPlan
			{
				DataSourceId = requestedDataSourceId ?? _authorized.FirstOrDefault(),
				IsAggregate = intent.Metrics.Count > 0,
				Distinct = false,
				Limit = intent.Limit,
			};
			plan.Tables.Add(new QueryTable
			{
				SemanticText = _entityOverride ?? "sales_order",
				TableName = _entityOverride ?? "sales_order",
				MetadataTableId = 1,
			});
			plan.Fields.Add(new QueryField { ColumnName = "status", DataType = "string" });
			foreach (var d in intent.Dimensions)
				plan.Dimensions.Add(new QueryDimension { SemanticText = d });
			foreach (var m in intent.Metrics)
				plan.Metrics.Add(new QueryMetric { Field = m.Field, Aggregation = m.Aggregation });
			foreach (var f in intent.Filters)
				plan.Filters.Add(new QueryFilter { Field = f.Field, Operator = f.Operator, Value = f.Value });
			if (!string.IsNullOrEmpty(intent.OrderBy))
				plan.Orders.Add(new QueryOrder { Field = intent.OrderBy, Direction = intent.OrderDirection ?? "ASC" });

			return Task.FromResult(new QueryPlanPipelineResult { Plan = plan });
		}
	}
}
