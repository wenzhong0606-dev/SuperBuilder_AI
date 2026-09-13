using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.BI;
using SuperBuilder_AI.Api.Errors;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// P0-01 回归：明确指定数据源时，QueryPlanBuilder 必须约束元数据搜索/选表范围，
/// 使得 plan.DataSourceId 与请求的数据源同源；指定数据源但其中无匹配表时必须明确报错，
/// 而非静默回退到其它数据源。默认（不指定）路径行为保持不变。
/// </summary>
public class QueryPlanBuilderDataSourceConstraintTests
{
	private const long TenantId = 1;

	private sealed class FakeSearch : IMetadataSemanticSearchService
	{
		public List<MetadataSemanticSearchResult> Results { get; set; } = new();
		public Task<List<MetadataSemanticSearchResult>> SearchAsync(string question, int topK = 10, LocaleContext? locale = null)
			=> Task.FromResult(Results);

		public Task<List<MetadataSemanticSearchResult>> SearchByKeywordAsync(string keyword, int limit = 30)
			=> Task.FromResult(new List<MetadataSemanticSearchResult>());

		public Task<List<MetadataSemanticSearchResult>> SearchByKeywordSubstringAsync(string keyword, int limit = 30)
			=> Task.FromResult(new List<MetadataSemanticSearchResult>());
	}

	private sealed class FakeJoin : IQueryJoinInferenceService
	{
		public Task<List<QueryJoinCandidate>> InferAsync(List<MetadataSemanticSearchResult> metadataResults)
			=> Task.FromResult(new List<QueryJoinCandidate>());
	}

	private static MetadataTable MakeTable(long id, long dsId, string name, params (long colId, string colName, bool pk)[] cols)
		=> new MetadataTable
		{
			Id = id,
			TenantId = TenantId,
			DataSourceId = dsId,
			TableName = name,
			Columns = cols.Select(c => new MetadataColumn
			{
				Id = c.colId,
				ColumnName = c.colName,
				IsPrimaryKey = c.pk,
				DataType = "bigint",
			}).ToList(),
		};

	private static MetadataSemanticSearchResult TableVector(MetadataTable table, double score = 0.9)
		=> new MetadataSemanticSearchResult
		{
			// 必须设置唯一 VectorId：BusinessTermExtractor.SearchMetadataAsync 会按
			// "VectorType:VectorId" 去重，若两表 VectorId 相同会被合并成一条，
			// 从而破坏“跨数据源召回”的回归前提。
			VectorType = "table",
			VectorId = $"tbl-{table.Id}",
			Table = table,
			Score = score,
		};

	private static SuperBIContext CreateContext(out SqliteConnection connection)
	{
		connection = new SqliteConnection("DataSource=:memory:");
		connection.Open();
		var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
		var ctx = new SuperBIContext(options);
		ctx.Database.EnsureCreated();
		return ctx;
	}

	/// <summary>
	/// 在内存上下文中落库租户、所有涉及的数据源与表（含列），使 QueryPlanValidator 能加载并校验。
	/// 注意：这里只用于让校验通过，真正的“元数据召回”由 FakeSearch 提供，二者 Id/DataSourceId 必须一致。
	/// </summary>
	private static async Task SeedAsync(SuperBIContext ctx, params MetadataTable[] tables)
	{
		ctx.Tenants.Add(new Tenant { Id = TenantId, TenantCode = "seed", TenantName = "seed", Enabled = true, CreatedTime = DateTime.UtcNow });
		foreach (var dsId in tables.Select(t => t.DataSourceId).Distinct())
			ctx.DataSources.Add(new DataSource { Id = dsId, TenantId = TenantId, Name = $"ds-{dsId}", NormalizedName = $"ds-{dsId}", DbType = "sqlserver", ConnectionString = "x" });
		ctx.MetadataTables.AddRange(tables);
		await ctx.SaveChangesAsync();
	}

	private static QueryPlanBuilder BuildBuilder(FakeSearch search, SuperBIContext ctx)
		=> new QueryPlanBuilder(search, new FakeJoin(), new QueryPlanValidator(ctx), new QueryPlanDataSourceScope());

	private static QueryIntent MakeIntent()
		=> new QueryIntent
		{
			OriginalQuestion = "本月销售额",
			Metrics = { new QueryMetric { Name = "销售额", Field = "amount", Aggregation = "SUM" } },
		};

	[Fact]
	public async Task RequestedDataSource_ConstrainsToRequested_WhenPresent()
	{
		var tableA = MakeTable(10, 1, "sales", (100, "id", true), (101, "amount", false));
		var tableB = MakeTable(20, 2, "orders", (200, "id", true), (201, "amount", false));

		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedAsync(ctx, tableA, tableB);

		// 两个数据源的表都在召回结果中（跨源场景），但请求约束到 A（Id=1）。
		var search = new FakeSearch { Results = { TableVector(tableA), TableVector(tableB) } };
		var builder = BuildBuilder(search, ctx);

		var planA = await builder.BuildAsync(MakeIntent(), requestedDataSourceId: 1);
		Assert.Equal(1, planA.DataSourceId);
		Assert.All(planA.Tables, t => Assert.Equal(1, t.DataSourceId));

		// 约束到 B（Id=2）时同样必须同源。
		var planB = await builder.BuildAsync(MakeIntent(), requestedDataSourceId: 2);
		Assert.Equal(2, planB.DataSourceId);
		Assert.All(planB.Tables, t => Assert.Equal(2, t.DataSourceId));
	}

	[Fact]
	public async Task RequestedDataSource_NoMatchingTable_ThrowsClearError()
	{
		var tableB = MakeTable(20, 2, "orders", (200, "id", true), (201, "amount", false));

		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedAsync(ctx, tableB);

		// 召回结果只包含数据源 2 的表，但请求数据源 99（不存在匹配表）。
		var search = new FakeSearch { Results = { TableVector(tableB) } };
		var builder = BuildBuilder(search, ctx);

		var ex = await Assert.ThrowsAsync<InvalidOperationException>(
			() => builder.BuildAsync(MakeIntent(), requestedDataSourceId: 99));

		Assert.Contains("所选数据源", ex.Message);
		Assert.Contains("99", ex.Message);
	}

	[Fact]
	public async Task RequestedDataSource_Null_KeepsInferenceBehavior()
	{
		var tableA = MakeTable(10, 1, "sales", (100, "id", true), (101, "amount", false));
		var tableB = MakeTable(20, 2, "orders", (200, "id", true), (201, "amount", false));

		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedAsync(ctx, tableA, tableB);

		var search = new FakeSearch { Results = { TableVector(tableA), TableVector(tableB) } };
		var builder = BuildBuilder(search, ctx);

		// 不指定数据源：保持原有推断行为，不得抛“所选数据源”错误，且必须产出有效 DataSourceId。
		var plan = await builder.BuildAsync(MakeIntent(), requestedDataSourceId: null);
		Assert.True(plan.DataSourceId > 0);
	}

	[Fact]
	public async Task AuthorizedSources_FilterBeforeTableSelection()
	{
		var tableA = MakeTable(10, 1, "sales", (100, "id", true), (101, "amount", false));
		var tableB = MakeTable(20, 2, "orders", (200, "id", true), (201, "amount", false));
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedAsync(ctx, tableA, tableB);

		var builder = BuildBuilder(new FakeSearch { Results = { TableVector(tableA), TableVector(tableB) } }, ctx);
		var plan = await builder.BuildAsync(MakeIntent(), authorizedDataSourceIds: new[] { 2L });

		Assert.Equal(2, plan.DataSourceId);
		Assert.All(plan.Tables, table => Assert.Equal(2, table.DataSourceId));
	}

	[Fact]
	public async Task EmptyAuthorizedSources_DeniesBeforePlanCreation()
	{
		var tableA = MakeTable(10, 1, "sales", (100, "id", true), (101, "amount", false));
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedAsync(ctx, tableA);

		var builder = BuildBuilder(new FakeSearch { Results = { TableVector(tableA) } }, ctx);
		var ex = await Assert.ThrowsAsync<SuperBuilderException>(
			() => builder.BuildAsync(MakeIntent(), authorizedDataSourceIds: Array.Empty<long>()));

		Assert.Equal(ErrorCodes.DataSourceForbidden, ex.ErrorCode);
		Assert.Equal(403, ex.StatusCode);
	}
}
