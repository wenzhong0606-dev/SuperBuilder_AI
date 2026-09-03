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
/// M0-09 明细列表字段补全回归：
/// 1. 未指定字段的“最近N条”明细查询，自动按首选展示列补 6 个字段，不再只返回时间字段。
/// 2. 用户明确说“显示更多字段”时，即使 LLM 只返回少量字段，也强制扩展到首选展示列。
/// </summary>
public class QueryPlanBuilderDetailListTests
{
	private const long TenantId = 1;

	private sealed class FakeSearch : IMetadataSemanticSearchService
	{
		public List<MetadataSemanticSearchResult> Results { get; set; } = new();
		public Task<List<MetadataSemanticSearchResult>> SearchAsync(string question, int topK = 10, LocaleContext? locale = null)
			=> Task.FromResult(Results);
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
				new() { Id = 107, ColumnName = "create_by", DataType = "bigint" },
				new() { Id = 108, ColumnName = "update_by", DataType = "bigint" },
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
		connection = new SqliteConnection("DataSource=:memory:");
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
			ctx.DataSources.Add(new DataSource { Id = dsId, DbType = "sqlserver", ConnectionString = "x" });
		ctx.MetadataTables.AddRange(tables);
		await ctx.SaveChangesAsync();
	}

	private static QueryPlanBuilder BuildBuilder(FakeSearch search, SuperBIContext ctx)
		=> new QueryPlanBuilder(search, new FakeJoin(), new QueryPlanValidator(ctx));

	private static QueryIntent MakeDetailIntent(string question, int? limit = 10, string? orderBy = null)
		=> new QueryIntent
		{
			OriginalQuestion = question,
			IntentType = "Detail",
			Limit = limit,
			OrderBy = orderBy,
		};

	[Fact]
	public async Task Recent_detail_query_fills_six_preferred_display_columns()
	{
		var table = MakeReceiptTable();
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedAsync(ctx, table);

		var search = new FakeSearch { Results = { TableVector(table) } };
		var builder = BuildBuilder(search, ctx);

		var plan = await builder.BuildAsync(MakeDetailIntent("最近的十个入库单"));

		Assert.True(plan.Fields.Count >= 6, $"期望至少 6 个字段，实际 {plan.Fields.Count}");
		Assert.Contains(plan.Fields, f => string.Equals(f.ColumnName, "receipt_no", StringComparison.OrdinalIgnoreCase));
		Assert.Contains(plan.Fields, f => string.Equals(f.ColumnName, "come_time", StringComparison.OrdinalIgnoreCase));
		Assert.Contains(plan.Fields, f => string.Equals(f.ColumnName, "material_code", StringComparison.OrdinalIgnoreCase));
		Assert.Contains(plan.Fields, f => string.Equals(f.ColumnName, "quantity", StringComparison.OrdinalIgnoreCase));
		Assert.DoesNotContain(plan.Fields, f => string.Equals(f.ColumnName, "del_flag", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task Field_expansion_instruction_extends_to_preferred_columns()
	{
		var table = MakeReceiptTable();
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedAsync(ctx, table);

		var search = new FakeSearch { Results = { TableVector(table) } };
		var builder = BuildBuilder(search, ctx);

		// 带一个过滤条件使 Step 5.5 的兜底不触发；
		// 此时 plan.Fields 仅有 receipt_no，用户追加“显示更多字段”后，
		// Step 11 的扩展逻辑应把字段扩展到首选展示列。
		var plan = await builder.BuildAsync(new QueryIntent
		{
			OriginalQuestion = "最近的十个入库单；显示更多字段",
			IntentType = "Detail",
			Limit = 10,
			Filters = { new QueryFilter { Field = "receipt_no", Operator = "=", Value = "R001" } },
		});

		Assert.True(plan.Fields.Count >= 6, $"期望至少 6 个字段，实际 {plan.Fields.Count}");
		Assert.Contains(plan.Fields, f => string.Equals(f.ColumnName, "receipt_no", StringComparison.OrdinalIgnoreCase));
		Assert.Contains(plan.Fields, f => string.Equals(f.ColumnName, "material_code", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task Field_expansion_does_not_trigger_for_aggregates()
	{
		var table = MakeReceiptTable();
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedAsync(ctx, table);

		var search = new FakeSearch { Results = { TableVector(table) } };
		var builder = BuildBuilder(search, ctx);

		var plan = await builder.BuildAsync(new QueryIntent
		{
			OriginalQuestion = "最近的十个入库单；显示更多字段",
			IntentType = "Aggregate",
			Limit = 10,
			Metrics = { new QueryMetric { Name = "数量", Field = "quantity", Aggregation = "SUM" } },
		});

		// 聚合查询不应被明细列表字段扩展逻辑覆盖（isDetailList 为 false），
		// 字段集合保持原有规模（本场景由既有默认逻辑带入 quantity + 时间列，共 2 个），
		// 验证扩展逻辑没有把字段数推高到明细列表的 6~8 列。
		Assert.True(plan.Fields.Count < 6, $"聚合查询字段数不应被扩展，实际 {plan.Fields.Count}");
		Assert.Contains(plan.Fields, f => string.Equals(f.ColumnName, "quantity", StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// 用户报告的实战场景：表上 10+ 列，但只有 come_time 一列被 metadata 标记为「首选展示」，
	/// 旧版 GetPreferredDisplayColumns.Take 仅返回 1 列，导致界面只显示 come_time 一列。
	/// 兜底策略必须工作。
	/// </summary>
	[Fact]
	public async Task Field_expansion_falls_back_to_table_columns_when_metadata_poor()
	{
		// 模拟入库单 wms_storage_receipt_info：18+ 列，
		// 但 IsPreferredDisplay 标记只覆盖 come_time（与 user 实战复现一致）。
		var table = new MetadataTable
		{
			Id = 458,
			TenantId = TenantId,
			DataSourceId = 1,
			TableName = "wms_storage_receipt_info",
			Columns = new List<MetadataColumn>
			{
				new() { Id = 1, ColumnName = "id", IsPrimaryKey = true, DataType = "bigint" },
				new() { Id = 2, ColumnName = "storage_receipt_id", DataType = "bigint" },
				new() { Id = 3, ColumnName = "material_id", DataType = "bigint" },
				new() { Id = 4, ColumnName = "material_name", DataType = "varchar(50)" },
				new() { Id = 5, ColumnName = "material_code", DataType = "varchar(50)" },
				new() { Id = 6, ColumnName = "shelf_id", DataType = "int" },
				new() { Id = 7, ColumnName = "shelf_code", DataType = "varchar(50)" },
				new() { Id = 8, ColumnName = "shelf_name", DataType = "varchar(50)" },
				new() { Id = 9, ColumnName = "quantity", DataType = "decimal(18,4)" },
				new() { Id = 10, ColumnName = "unit", DataType = "bigint" },
				new() { Id = 11, ColumnName = "batch", DataType = "varchar(50)" },
				new() { Id = 12, ColumnName = "case_no", DataType = "varchar(50)" },
				// 仅 come_time 会被 GetPreferredDisplayColumns 命中
				// （datetime 类型落入 group 2），其他业务列全部 miss。
				new() { Id = 13, ColumnName = "come_time", DataType = "datetime" },
				new() { Id = 14, ColumnName = "create_by", DataType = "bigint" },
				new() { Id = 15, ColumnName = "update_by", DataType = "bigint" },
				new() { Id = 16, ColumnName = "del_flag", DataType = "tinyint" },
			},
		};

		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedAsync(ctx, table);

		var search = new FakeSearch { Results = { TableVector(table) } };
		var builder = BuildBuilder(search, ctx);

		var plan = await builder.BuildAsync(new QueryIntent
		{
			OriginalQuestion = "最近的十个入库单；显示更多字段",
			IntentType = "Detail",
			Limit = 10,
		});

		// 即便 metadata 标记稀疏，refine 后应至少有 6 个业务字段。
		Assert.True(plan.Fields.Count >= 6, $"期望至少 6 个字段，实际 {plan.Fields.Count}");
		Assert.Contains(plan.Fields, f => string.Equals(f.ColumnName, "come_time", StringComparison.OrdinalIgnoreCase));
		Assert.Contains(plan.Fields, f => string.Equals(f.ColumnName, "material_code", StringComparison.OrdinalIgnoreCase));
		Assert.Contains(plan.Fields, f => string.Equals(f.ColumnName, "quantity", StringComparison.OrdinalIgnoreCase));
		// 内部字段必须排除
		Assert.DoesNotContain(plan.Fields, f => f.ColumnName != null && f.ColumnName.EndsWith("_by", StringComparison.OrdinalIgnoreCase));
		Assert.DoesNotContain(plan.Fields, f => string.Equals(f.ColumnName, "del_flag", StringComparison.OrdinalIgnoreCase));
	}
}
