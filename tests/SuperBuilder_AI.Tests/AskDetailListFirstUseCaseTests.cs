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
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M5-14 Ask 首用例回归：明细排序语义（实体 + Limit + Order）可构成有效计划，
/// 不强制 Metric/Dimension；且 <c>SB_BI_002</c>（未能识别可分析字段）仅用于确实无法形成
/// 可查询字段的请求。
///
/// 本类锁定 M5-14 的核心契约边界：
/// 1. 合法明细列表（目标实体已解析 + Limit/Order + 非聚合 + 无指标/维度）进入 SQL Builder，
///    不得误报 SB_BI_002（<see cref="SuperBuilder_AI.Api.Errors.ErrorCodes.BiNoQueryableField"/>）。
/// 2. 确实无法解析出任何实体/字段的请求，必须抛出「没有任何可查询字段」，由
///    <see cref="SuperBuilder_AI.Api.Middleware.UnifiedExceptionMiddleware"/> 映射为 SB_BI_002。
/// </summary>
public sealed class AskDetailListFirstUseCaseTests
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

	private static MetadataTable MakeReceiptTable(long id = 10, long dsId = 1)
		=> new MetadataTable
		{
			Id = id,
			TenantId = TenantId,
			DataSourceId = dsId,
			TableName = "wms_receipt",
			Columns = new List<MetadataColumn>
			{
				new() { Id = 100, ColumnName = "id", IsPrimaryKey = true, DataType = "bigint" },
				new() { Id = 101, ColumnName = "receipt_no", DataType = "varchar(64)" },
				new() { Id = 102, ColumnName = "come_time", DataType = "datetime" },
				new() { Id = 103, ColumnName = "material_code", DataType = "varchar(64)" },
				new() { Id = 104, ColumnName = "quantity", DataType = "decimal(18,4)" },
				new() { Id = 105, ColumnName = "amount", DataType = "decimal(18,4)" },
				new() { Id = 106, ColumnName = "warehouse_code", DataType = "varchar(64)" },
				new() { Id = 109, ColumnName = "del_flag", DataType = "tinyint" },
			},
		};

	private static MetadataSemanticSearchResult TableVector(MetadataTable table, double score = 0.9)
		=> new MetadataSemanticSearchResult
		{
			VectorType = "table",
			VectorId = $"tbl-{table.Id}",
			Table = table,
			Score = score,
		};

	private static SuperBIContext CreateContext(out SqliteConnection connection)
	{
		connection = new SqliteConnection("DataSource=:memory:;Pooling=false");
		connection.Open();
		var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
		var ctx = new SuperBIContext(options);
		ctx.Database.EnsureCreated();
		return ctx;
	}

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

	private static QueryIntent MakeDetailIntent(string question, int? limit = 10, string? orderBy = null)
		=> new QueryIntent
		{
			OriginalQuestion = question,
			IntentType = "Detail",
			Limit = limit,
			OrderBy = orderBy,
			OrderDirection = "DESC",
		};

	/// <summary>
	/// Ask 首用例正向：明细列表（实体 + Limit + Order，无指标/维度）必须解析为有效计划、
	/// 自动补列并进入 SQL Builder，绝不误报 SB_BI_002。
	/// 对应 M5-14「"最近十张入库凭证"进入 SQL Builder」。
	/// </summary>
	[Fact]
	public async Task Ask_First_UseCase_DetailList_Resolves_To_ValidPlan_Without_SB_BI_002()
	{
		var table = MakeReceiptTable();
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedAsync(ctx, table);

		var search = new FakeSearch { Results = { TableVector(table) } };
		var builder = BuildBuilder(search, ctx);

		var plan = await builder.BuildAsync(MakeDetailIntent("最近十张入库凭证", limit: 10, orderBy: "come_time"));

		// 实体已解析（目标表存在）。
		Assert.Contains(plan.Tables, t => t.MetadataTableId > 0);
		// 不强制 Metric/Dimension：明细列表语义自身构成有效计划。
		Assert.Empty(plan.Metrics);
		Assert.Empty(plan.Dimensions);
		// 自动补列：字段非空 → 不会触发「没有任何可查询字段」（SB_BI_002）。
		Assert.True(plan.Fields.Count > 0, "明细列表应自动补列，不应为空触发 SB_BI_002");
		Assert.DoesNotContain(plan.Fields, f => string.Equals(f.ColumnName, "del_flag", StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// Ask 首用例负向：实体已解析、但其所有列均为内部字段（无业务展示列），确实无法形成
	/// 任何可查询字段，必须抛出「没有任何可查询字段」，由 UnifiedExceptionMiddleware 映射为
	/// SB_BI_002（BiNoQueryableField）。
	/// 对应 M5-14「SB_BI_002 只用于确实无法形成可查询字段的请求」。
	/// 复现口径同 QueryPlanBuilderQaRegressionTests.X2（全内部列 → 补列为空 → 抛 SB_BI_002）。
	/// </summary>
	[Fact]
	public async Task Ask_First_Use_Case_Unformable_Query_Triggers_SB_BI_002()
	{
		var table = new MetadataTable
		{
			Id = 20,
			TenantId = TenantId,
			DataSourceId = 1,
			TableName = "all_internal",
			Columns = new List<MetadataColumn>
			{
				new() { Id = 200, ColumnName = "del_flag", DataType = "tinyint" },
				new() { Id = 201, ColumnName = "is_deleted", DataType = "tinyint" },
				new() { Id = 202, ColumnName = "create_by", DataType = "bigint" },
				new() { Id = 203, ColumnName = "update_by", DataType = "bigint" },
			},
		};

		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedAsync(ctx, table);

		// 实体已解析（表存在），但所有列均为内部字段，无业务展示列。
		var search = new FakeSearch { Results = { TableVector(table) } };
		var builder = BuildBuilder(search, ctx);

		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			builder.BuildAsync(new QueryIntent
			{
				OriginalQuestion = "最近的十条记录",
				IntentType = "Detail",
				Limit = 10,
			}));

		Assert.Contains("没有任何可查询字段", ex.Message);
	}
}
