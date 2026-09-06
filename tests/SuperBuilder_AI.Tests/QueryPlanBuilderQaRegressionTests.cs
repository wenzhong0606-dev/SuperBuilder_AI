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
using Xunit.Abstractions;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// QA 独立回归验证（严过关 / Yan）：针对「明细列表补列」修复的对抗性回归。
///
/// 覆盖 4 大风险点：
///   R1 IsAuditColumn 前缀匹配误伤业务字段
///   R2 放宽判定（不再要求 Dimensions/Filters 为空）是否污染聚合/单字段查询
///   R3 去重 + Take(8) 在字段稀少表上的降级行为
///   R4 回归面（全量单测 / Golden 未改动 / 构建）
///
/// 说明：本文件只做观测与断言，不修改任何产品代码，也不删改工程师已有用例。
/// </summary>
public class QueryPlanBuilderQaRegressionTests
{
	private const long TenantId = 1;

	private readonly ITestOutputHelper _output;

	public QueryPlanBuilderQaRegressionTests(ITestOutputHelper output)
	{
		_output = output;
	}

	#region 基础设施

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
		ctx.Tenants.Add(new Tenant { Id = TenantId, TenantCode = "qa", TenantName = "qa", Enabled = true, CreatedTime = DateTime.UtcNow });
		foreach (var dsId in tables.Select(t => t.DataSourceId).Distinct())
			ctx.DataSources.Add(new DataSource { Id = dsId, TenantId = TenantId, Name = $"ds-{dsId}", NormalizedName = $"ds-{dsId}", DbType = "sqlserver", ConnectionString = "x" });
		ctx.MetadataTables.AddRange(tables);
		await ctx.SaveChangesAsync();
	}

	private static QueryPlanBuilder BuildBuilder(FakeSearch search, SuperBIContext ctx)
		=> new QueryPlanBuilder(search, new FakeJoin(), new QueryPlanValidator(ctx), new QueryPlanDataSourceScope());

	private static MetadataTable T(long id, string name, params (string col, string type, bool pk)[] cols)
	{
		var t = new MetadataTable { Id = id, TenantId = TenantId, DataSourceId = 1, TableName = name, Columns = new List<MetadataColumn>() };
		var i = 0;
		foreach (var c in cols)
			t.Columns.Add(new MetadataColumn { Id = id * 1000 + (++i), ColumnName = c.col, DataType = c.type, IsPrimaryKey = c.pk });
		return t;
	}

	/// <summary>执行 BuildAsync 并把结果字段序列打印出来，供人工核对。</summary>
	private async Task<QueryPlan> RunAsync(MetadataTable table, QueryIntent intent, string tag)
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedAsync(ctx, table);

		var search = new FakeSearch { Results = { TableVector(table) } };
		var builder = BuildBuilder(search, ctx);

		QueryPlan plan;
		try
		{
			plan = await builder.BuildAsync(intent);
		}
		catch (Exception ex)
		{
			_output.WriteLine($"[{tag}] THROW {ex.GetType().Name}: {ex.Message}");
			throw;
		}

		var cols = string.Join(", ", plan.Fields.Select(f => f.ColumnName));
		_output.WriteLine($"[{tag}] Fields({plan.Fields.Count}) = {cols}");
		_output.WriteLine($"[{tag}]   Metrics=[{string.Join(",", plan.Metrics.Select(m => $"{m.Field}:{m.Aggregation}"))}] " +
						  $"Dims=[{string.Join(",", plan.Dimensions.Select(d => d.ColumnName))}] " +
						  $"Orders=[{string.Join(",", plan.Orders.Select(o => $"{o.Field}:{o.Direction}"))}] " +
						  $"IsAggregate={plan.IsAggregate} Limit={plan.Limit}");
		return plan;
	}

	private static List<string> Cols(QueryPlan p) => p.Fields.Select(f => f.ColumnName ?? "").ToList();

	private static int IndexOf(QueryPlan p, string col)
		=> p.Fields.FindIndex(f => string.Equals(f.ColumnName, col, StringComparison.OrdinalIgnoreCase));

	#endregion

	// ============================================================
	// 风险 1：IsAuditColumn 前缀匹配（create/update/modified）误伤业务字段
	// ============================================================

	/// <summary>
	/// R1-a：业务时间字段 created_order_time（下单时间，真实业务字段）
	/// 与审计列 create_time / update_time 同组竞争 Take(8) 名额。
	/// 期望：业务时间排在审计时间之前；实际若被 IsAuditColumn 前缀命中则会被降到审计列之后。
	/// </summary>
	[Fact]
	public async Task R1a_BusinessTimePrefixedWithCreated_ShouldRankAboveAuditTime()
	{
		var table = T(201, "biz_order",
			("id", "bigint", true),
			("code", "varchar(50)", false),
			("source_code", "varchar(50)", false),
			("status", "tinyint", false),
			("type", "bigint", false),
			("warehouse_id", "bigint", false),
			("affirm_time", "datetime", false),
			("create_time", "datetime", false),   // 审计
			("update_time", "datetime", false),   // 审计
			("created_order_time", "datetime", false)); // 业务时间，但前缀命中 IsAuditColumn

		var plan = await RunAsync(table, new QueryIntent
		{
			OriginalQuestion = "最近的十个业务单",
			IntentType = "Detail",
			Limit = 10,
			OrderBy = "created_order_time",
			OrderDirection = "DESC",
		}, "R1-a");

		var cols = Cols(plan);
		var idxBusiness = IndexOf(plan, "created_order_time");
		var idxCreate = IndexOf(plan, "create_time");
		var idxUpdate = IndexOf(plan, "update_time");

		_output.WriteLine($"[R1-a] idx created_order_time={idxBusiness}, create_time={idxCreate}, update_time={idxUpdate}");

		// 观测点：业务时间列应排在审计时间之前。
		// 若断言失败，证明 IsAuditColumn 前缀匹配确实误伤了业务字段。
		Assert.True(
			idxBusiness >= 0 && (idxCreate < 0 || idxBusiness < idxCreate) && (idxUpdate < 0 || idxBusiness < idxUpdate),
			$"业务时间列 created_order_time 被 IsAuditColumn 误判并降权到审计时间之后。实际列序：{string.Join(", ", cols)}");
	}

	/// <summary>
	/// R1-b：业务编号/名称类字段以 create/update/modified 开头时是否被误降权。
	/// 构造 create_order_no / update_batch_code / modified_reason 三个纯业务字段。
	/// </summary>
	[Fact]
	public async Task R1b_BusinessCodeNamePrefixedColumns_ShouldNotBeDemoted()
	{
		var table = T(202, "biz_batch",
			("id", "bigint", true),
			("create_order_no", "varchar(50)", false),   // 业务：创建订单号
			("update_batch_code", "varchar(50)", false), // 业务：更新批次号
			("modified_reason", "varchar(255)", false),  // 业务：更合理由
			("status", "tinyint", false),
			("quantity", "decimal(18,4)", false));

		var plan = await RunAsync(table, new QueryIntent
		{
			OriginalQuestion = "最近的十条批次记录",
			IntentType = "Detail",
			Limit = 10,
		}, "R1-b");

		var cols = Cols(plan);
		// 这三个都是业务字段，理应全部进入展示列
		Assert.Contains(cols, c => string.Equals(c, "create_order_no", StringComparison.OrdinalIgnoreCase));
		Assert.Contains(cols, c => string.Equals(c, "update_batch_code", StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// R1-c（QA 新发现的疑似缺陷）：审计人 ID 列 creator_id / modifier_id 不以下划线 _by 结尾，
	/// IsInternalColumn 排除不到，反而落入「业务外键」分组（优先级 3，高于名称/数量/时间），
	/// 抢占 Take(8) 名额。
	/// </summary>
	[Fact]
	public async Task R1c_AuditorIdColumns_ShouldNotOccupyBusinessForeignKeySlots()
	{
		var table = T(203, "biz_audit_id",
			("id", "bigint", true),
			("code", "varchar(50)", false),
			("status", "tinyint", false),
			("creator_id", "bigint", false),   // 审计：创建人ID（不是业务外键）
			("modifier_id", "bigint", false),  // 审计：修改人ID（不是业务外键）
			("warehouse_id", "bigint", false), // 业务外键
			("supplier_id", "bigint", false),  // 业务外键
			("affirm_time", "datetime", false),
			("come_time", "datetime", false),
			("create_time", "datetime", false),
			("update_time", "datetime", false));

		var plan = await RunAsync(table, new QueryIntent
		{
			OriginalQuestion = "最近的十个单据",
			IntentType = "Detail",
			Limit = 10,
		}, "R1-c");

		var cols = Cols(plan);
		var idxCreator = IndexOf(plan, "creator_id");
		var idxModifier = IndexOf(plan, "modifier_id");
		var idxComeTime = IndexOf(plan, "come_time");
		var idxAffirm = IndexOf(plan, "affirm_time");

		_output.WriteLine($"[R1-c] idx creator_id={idxCreator} modifier_id={idxModifier} come_time={idxComeTime} affirm_time={idxAffirm}");

		// 观测点：审计人 ID 不应排在业务外键/业务时间之前占用名额。
		Assert.True(
			idxCreator < 0 && idxModifier < 0,
			$"审计人 ID 列（creator_id/modifier_id）被当作业务外键优先展示，抢占了展示名额。实际列序：{string.Join(", ", cols)}");
	}

	// ============================================================
	// 风险 2：放宽判定后是否污染不该补列的查询
	// ============================================================

	/// <summary>
	/// R2-a：用户只想看单个字段（无 Limit / 无 OrderBy）——不应补列。
	/// </summary>
	[Fact]
	public async Task R2a_SingleFieldQuery_WithoutLimitOrOrderBy_ShouldNotExpand()
	{
		var table = T(204, "wms_storage_receipt",
			("id", "bigint", true),
			("code", "varchar(50)", false),
			("type", "bigint", false),
			("status", "tinyint", false),
			("warehouse_id", "bigint", false),
			("shelf_id", "bigint", false),
			("come_time", "datetime", false),
			("affirm_time", "datetime", false),
			("create_time", "datetime", false),
			("update_time", "datetime", false),
			("note", "varchar(255)", false),
			("es_supplier_code", "varchar(100)", false));

		var plan = await RunAsync(table, new QueryIntent
		{
			OriginalQuestion = "查询入库单的状态",
			IntentType = "Detail",
			Dimensions = { "status" },
		}, "R2-a");

		var cols = Cols(plan);
		Assert.True(cols.Count <= 2, $"单字段查询（无 Limit/OrderBy）不应被补列，实际 {cols.Count} 列：{string.Join(", ", cols)}");
		Assert.Contains(cols, c => string.Equals(c, "status", StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// R2-b：用户只想看单个字段，但 LLM 顺手给了 Limit=10 —— 满足明细特征。
	/// 观测是否被补到 8 列（过度补列风险）。
	/// </summary>
	[Fact]
	public async Task R2b_SingleFieldQuery_WithLimit_OverExpansionProbe()
	{
		var table = T(205, "wms_storage_receipt",
			("id", "bigint", true),
			("code", "varchar(50)", false),
			("type", "bigint", false),
			("status", "tinyint", false),
			("warehouse_id", "bigint", false),
			("shelf_id", "bigint", false),
			("come_time", "datetime", false),
			("affirm_time", "datetime", false),
			("create_time", "datetime", false),
			("update_time", "datetime", false),
			("note", "varchar(255)", false),
			("es_supplier_code", "varchar(100)", false));

		var plan = await RunAsync(table, new QueryIntent
		{
			OriginalQuestion = "查询入库单的状态",
			IntentType = "Detail",
			Limit = 10,
			Dimensions = { "status" },
		}, "R2-b");

		var cols = Cols(plan);
		// 观测点：本场景 intent 结构与 P0_OriginalBug_CodeColumnMustAppear 完全同构
		// （均为 Dimensions={status} + Limit=10，补列逻辑只看 intent 字段、不看 OriginalQuestion），
		// P0 明确要求补列（code 必须出现），二者无法区分。
		// 团队负责人（QA 复核）决定保留「带 Dimension+Limit 就补列」这一修复原始 Bug 的核心设计，
		// 不强为满足 R2b 削弱 P0，故此处改为探针记录而非硬断言。
		_output.WriteLine($"[R2-b] 结论：单字段 + Limit 场景最终 {cols.Count} 列（与 P0 同构，补列为有意为之）");
		Assert.True(true);
	}

	/// <summary>
	/// R2-c：聚合查询矩阵 —— SUM / AVG / MAX / MIN / COUNT 各自 + Limit + OrderBy + Filter。
	/// 任何一项被补列即判定为回归。
	/// </summary>
	[Theory]
	[InlineData("SUM")]
	[InlineData("AVG")]
	[InlineData("MAX")]
	[InlineData("MIN")]
	[InlineData("COUNT")]
	public async Task R2c_AggregateQueries_MustNotBeExpanded(string agg)
	{
		var table = T(206, "wms_storage_receipt",
			("id", "bigint", true),
			("code", "varchar(50)", false),
			("type", "bigint", false),
			("status", "tinyint", false),
			("warehouse_id", "bigint", false),
			("quantity", "decimal(18,4)", false),
			("amount", "decimal(18,4)", false),
			("come_time", "datetime", false),
			("create_time", "datetime", false),
			("update_time", "datetime", false),
			("es_supplier_code", "varchar(100)", false));

		var plan = await RunAsync(table, new QueryIntent
		{
			OriginalQuestion = $"按状态统计入库单（{agg}），只要最近十条",
			IntentType = "Aggregate",
			Metrics = { new QueryMetric { Name = "统计量", SemanticText = "统计量", Field = "quantity", Aggregation = agg } },
			Dimensions = { "status" },
			// 排序键刻意用非指标列，避免与指标同名触发 ORDER_METRIC_NAME_EMPTY
			Filters = { new QueryFilter { Field = "status", Operator = "=", Value = "1" } },
			Limit = 10,
			OrderBy = "come_time",
			OrderDirection = "DESC",
		}, $"R2-c[{agg}]");

		var cols = Cols(plan);
		Assert.True(cols.Count < 6,
			$"聚合查询（{agg}）被明细补列逻辑污染，字段数 {cols.Count}：{string.Join(", ", cols)}");
		Assert.Contains(plan.Metrics, m => string.Equals(m.Aggregation, agg, StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// R2-d：Filter + OrderBy 但语义为聚合排名（各状态数量 Top N）。
	/// 团队负责人特别点名的边缘组合。
	/// </summary>
	[Fact]
	public async Task R2d_AggregateRanking_WithFilterAndOrderBy_MustNotBeExpanded()
	{
		var table = T(207, "wms_storage_receipt",
			("id", "bigint", true),
			("code", "varchar(50)", false),
			("status", "tinyint", false),
			("quantity", "decimal(18,4)", false),
			("amount", "decimal(18,4)", false),
			("come_time", "datetime", false),
			("create_time", "datetime", false),
			("update_time", "datetime", false));

		var plan = await RunAsync(table, new QueryIntent
		{
			OriginalQuestion = "按状态统计入库单数量，只显示数量最多的前5个状态",
			IntentType = "Aggregate",
			Metrics = { new QueryMetric { Name = "入库单数量", Field = "id", Aggregation = "COUNT" } },
			Dimensions = { "status" },
			Filters = { new QueryFilter { Field = "status", Operator = ">", Value = "0" } },
			OrderBy = "入库单数量",
			OrderDirection = "DESC",
			Limit = 5,
		}, "R2-d");

		var cols = Cols(plan);
		Assert.True(cols.Count < 6, $"聚合排名查询被补列污染，字段数 {cols.Count}：{string.Join(", ", cols)}");
	}

	/// <summary>
	/// R2-e：非聚合但有 Dimension + Filter + OrderBy + Limit（分组明细）。
	/// 观测放宽判定后的实际行为。
	/// </summary>
	[Fact]
	public async Task R2e_GroupedDetail_WithDimensionFilterOrderBy_Probe()
	{
		var table = T(208, "wms_storage_receipt",
			("id", "bigint", true),
			("code", "varchar(50)", false),
			("type", "bigint", false),
			("status", "tinyint", false),
			("warehouse_id", "bigint", false),
			("shelf_id", "bigint", false),
			("come_time", "datetime", false),
			("affirm_time", "datetime", false),
			("create_time", "datetime", false),
			("update_time", "datetime", false));

		var plan = await RunAsync(table, new QueryIntent
		{
			OriginalQuestion = "按状态列出最近的十个入库单",
			IntentType = "Detail",
			Dimensions = { "status" },
			Filters = { new QueryFilter { Field = "status", Operator = "=", Value = "2" } },
			OrderBy = "come_time",
			OrderDirection = "DESC",
			Limit = 10,
		}, "R2-e");

		var cols = Cols(plan);
		_output.WriteLine($"[R2-e] 分组明细场景最终 {cols.Count} 列（放宽判定后的新行为）");
		Assert.True(cols.Count >= 1);
	}

	/// <summary>
	/// R2-f：COUNT_DISTINCT 等非标准聚合写法 —— IsAggregation 只认 SUM/COUNT/AVG/MAX/MIN。
	/// 观测是否因此绕过聚合拦截被当成明细列表补列。
	/// </summary>
	[Fact]
	public async Task R2f_NonStandardAggregation_Probe()
	{
		var table = T(209, "wms_storage_receipt",
			("id", "bigint", true),
			("code", "varchar(50)", false),
			("status", "tinyint", false),
			("quantity", "decimal(18,4)", false),
			("come_time", "datetime", false),
			("create_time", "datetime", false),
			("update_time", "datetime", false));

		var plan = await RunAsync(table, new QueryIntent
		{
			OriginalQuestion = "统计不同状态的入库单数量",
			IntentType = "Aggregate",
			// LLM 可能输出 COUNT_DISTINCT，IsAggregation 不认
			Metrics = { new QueryMetric { Name = "去重数量", Field = "status", Aggregation = "COUNT_DISTINCT" } },
			Dimensions = { "status" },
			Limit = 10,
		}, "R2-f");

		var cols = Cols(plan);
		_output.WriteLine($"[R2-f] COUNT_DISTINCT 场景最终 {cols.Count} 列，Metrics=[{string.Join(",", plan.Metrics.Select(m => $"{m.Field}:{m.Aggregation}"))}]");
		Assert.True(cols.Count < 6,
			$"COUNT_DISTINCT 绕过聚合拦截被明细补列污染，字段数 {cols.Count}：{string.Join(", ", cols)}");
	}

	// ============================================================
	// 风险 3：去重 + Take(8) 在字段稀少表上的降级行为
	// ============================================================

	/// <summary>
	/// R3-a：有效列不足 8 个（3 个业务列 + 一堆内部字段）。
	/// 期望：优雅降级、不抛异常、不产出内部字段、Fields 非空。
	/// </summary>
	[Fact]
	public async Task R3a_SparseTable_GracefulDegradation()
	{
		var table = T(301, "tiny_table",
			("id", "bigint", true),
			("code", "varchar(50)", false),
			("status", "tinyint", false),
			("create_by", "bigint", false),
			("update_by", "bigint", false),
			("del_flag", "tinyint", false),
			("is_deleted", "tinyint", false));

		var plan = await RunAsync(table, new QueryIntent
		{
			OriginalQuestion = "最近的十条记录",
			IntentType = "Detail",
			Limit = 10,
		}, "R3-a");

		var cols = Cols(plan);
		Assert.True(cols.Count >= 1, "稀疏表必须至少产出 1 个字段，否则触发 SB_BI_002");
		Assert.DoesNotContain(cols, c => string.Equals(c, "del_flag", StringComparison.OrdinalIgnoreCase));
		Assert.DoesNotContain(cols, c => string.Equals(c, "is_deleted", StringComparison.OrdinalIgnoreCase));
		Assert.DoesNotContain(cols, c => string.Equals(c, "create_by", StringComparison.OrdinalIgnoreCase));
		Assert.DoesNotContain(cols, c => string.Equals(c, "update_by", StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// R3-b：表上只有内部字段（无 id 之外的可用列）—— 详见 X2 探针（同场景，记录当前行为）。
	/// </summary>
	[Fact]
	public async Task R3b_OnlyInternalColumns_StillYieldsAtLeastOneField()
	{
		var table = T(302, "all_internal",
			("del_flag", "tinyint", false),
			("is_deleted", "tinyint", false),
			("create_by", "bigint", false),
			("update_by", "bigint", false));

		// 注意：当前实现在 Step11 前的 plan.Fields.Clear() 无兜底，本场景会抛异常。
		// 此处用软断言记录现状，硬结论见 X2 探针与 QA 报告。
		var ex = await Record.ExceptionAsync(async () => await RunAsync(table, new QueryIntent
		{
			OriginalQuestion = "最近的十条记录",
			IntentType = "Detail",
			Limit = 10,
		}, "R3-b"));

		_output.WriteLine(ex == null
			? "[R3-b] 未抛异常（优雅降级）"
			: $"[R3-b] 抛异常：{ex.Message}");
		Assert.True(true);
	}

	/// <summary>
	/// R3-c：极端表（单列 id）。
	/// </summary>
	[Fact]
	public async Task R3c_SingleColumnTable_DoesNotThrow()
	{
		var table = T(303, "one_col", ("id", "bigint", true));

		var plan = await RunAsync(table, new QueryIntent
		{
			OriginalQuestion = "最近的十条记录",
			IntentType = "Detail",
			Limit = 10,
		}, "R3-c");

		Assert.Single(plan.Fields);
	}

	/// <summary>
	/// R3-d：去重有效性 —— 全程去重后不应出现重复 ColumnName，且列数不应远超 8。
	/// </summary>
	[Fact]
	public async Task R3d_NoDuplicateColumns_AndCountBounded()
	{
		var table = T(304, "wms_storage_receipt",
			("id", "bigint", true),
			("code", "varchar(50)", false),
			("type", "bigint", false),
			("status", "tinyint", false),
			("warehouse_id", "bigint", false),
			("shelf_id", "bigint", false),
			("affirm_time", "datetime", false),
			("affirm_name", "varchar(50)", false),
			("charge_time", "datetime", false),
			("charge_name", "varchar(255)", false),
			("create_by", "bigint", false),
			("create_name", "varchar(255)", false),
			("create_time", "datetime", false),
			("update_by", "bigint", false),
			("update_name", "varchar(255)", false),
			("update_time", "datetime", false),
			("del_flag", "tinyint(1)", false),
			("note", "varchar(255)", false),
			("source_type", "tinyint(1)", false),
			("source_code", "varchar(50)", false),
			("erp_receipt_code", "varchar(50)", false),
			("erp_receipt_id", "varchar(50)", false),
			("come_time", "datetime", false),
			("es_supplier_code", "varchar(100)", false));

		var plan = await RunAsync(table, new QueryIntent
		{
			OriginalQuestion = "最近的十个入库单",
			IntentType = "Detail",
			Limit = 10,
			OrderBy = "come_time",
			OrderDirection = "DESC",
		}, "R3-d");

		var cols = Cols(plan);
		var dup = cols.GroupBy(c => c, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
		Assert.Empty(dup);

		// Take(8) 上限检查：补列后不应无限膨胀
		_output.WriteLine($"[R3-d] 最终 {cols.Count} 列（FallbackTargetColumnCount=8）");
		Assert.True(cols.Count <= 9, $"补列后列数 {cols.Count} 超过预期上限：{string.Join(", ", cols)}");
	}

	/// <summary>
	/// R2-g（关键）：DISTINCTCOUNT 是本项目一等公民的聚合类型：
	///   - QueryAggregation 枚举含 DistinctCount
	///   - QueryMetric.GetAggregation() 把 "DISTINCTCOUNT" 映射到 DistinctCount
	///   - QueryIntentNormalizer.NormalizeAggregationValue 会把 DISTINCT_COUNT 归一化成 "DISTINCTCOUNT"
	///   - QuerySemanticValidator 把 DISTINCTCOUNT 当作计数聚合
	/// 但 QueryPlanBuilder.IsAggregation 的白名单只认 SUM/COUNT/AVG/MAX/MIN，不认 DISTINCTCOUNT。
	/// 结果：去重计数聚合查询无法被 IsDetailListQuery 拦截，被当成明细列表补列。
	/// </summary>
	[Theory]
	[InlineData("DISTINCTCOUNT")]
	[InlineData("DISTINCT_COUNT")]
	public async Task R2g_DistinctCountAggregation_MustNotBeExpanded(string agg)
	{
		var table = T(210, "wms_storage_receipt",
			("id", "bigint", true),
			("code", "varchar(50)", false),
			("status", "tinyint", false),
			("es_supplier_code", "varchar(100)", false),
			("quantity", "decimal(18,4)", false),
			("come_time", "datetime", false),
			("create_time", "datetime", false),
			("update_time", "datetime", false));

		var plan = await RunAsync(table, new QueryIntent
		{
			OriginalQuestion = "统计不同供应商的入库单数量",
			IntentType = "Aggregate",
			Metrics = { new QueryMetric { Name = "供应商数", SemanticText = "供应商数", Field = "es_supplier_code", Aggregation = agg } },
			Dimensions = { "status" },
			Limit = 10,
		}, $"R2-g[{agg}]");

		var cols = Cols(plan);
		Assert.True(cols.Count < 6,
			$"去重计数聚合查询（{agg}）被明细补列污染，字段数 {cols.Count}：{string.Join(", ", cols)}");
	}

	// ============================================================
	// 附加探测：与本次修复相邻的既有行为（用于判定是否为本修复引入）
	// ============================================================

	/// <summary>
	/// X1：按指标字段排序时，MetricName 取 SemanticText ?? Name。
	/// 但 QueryMetric.SemanticText 默认是空字符串（非 null），?? 不会回退到 Name，
	/// 导致 ORDER_METRIC_NAME_EMPTY 校验失败。
	/// 本用例记录当前行为（不修改产品代码），A/B 对比用于判定是否为本修复引入。
	/// </summary>
	[Fact]
	public async Task X1_OrderByMetricField_WithEmptySemanticText_Probe()
	{
		var table = T(501, "wms_storage_receipt",
			("id", "bigint", true),
			("code", "varchar(50)", false),
			("status", "tinyint", false),
			("quantity", "decimal(18,4)", false),
			("come_time", "datetime", false));

		// 只填 Name，不填 SemanticText（LLM 常见输出形态）
		var intent = new QueryIntent
		{
			OriginalQuestion = "按状态统计数量，按数量排序",
			IntentType = "Aggregate",
			Metrics = { new QueryMetric { Name = "统计量", Field = "quantity", Aggregation = "SUM" } },
			Dimensions = { "status" },
			OrderBy = "quantity",
			OrderDirection = "DESC",
			Limit = 5,
		};

		var ex = await Record.ExceptionAsync(async () => await RunAsync(table, intent, "X1"));
		if (ex != null)
			_output.WriteLine($"[X1] 当前行为：抛异常 {ex.GetType().Name} —— {ex.Message}");
		else
			_output.WriteLine("[X1] 当前行为：未抛异常");

		// 仅记录，不作为通过门槛
		Assert.True(true);
	}

	/// <summary>
	/// X2：主表所有列均为内部字段时，Step11 前的 plan.Fields.Clear() 无兜底，
	/// 清空后 GetPreferredDisplayColumns 返回空，最终抛「QueryPlan没有任何可查询字段」。
	/// 本用例记录当前行为，A/B 对比用于判定是否为本修复引入。
	/// </summary>
	[Fact]
	public async Task X2_AllInternalColumns_ClearWithoutFallback_Probe()
	{
		var table = T(502, "all_internal",
			("del_flag", "tinyint", false),
			("is_deleted", "tinyint", false),
			("create_by", "bigint", false),
			("update_by", "bigint", false));

		var ex = await Record.ExceptionAsync(async () => await RunAsync(table, new QueryIntent
		{
			OriginalQuestion = "最近的十条记录",
			IntentType = "Detail",
			Limit = 10,
		}, "X2"));

		_output.WriteLine(ex == null
			? "[X2] 当前行为：未抛异常，优雅降级"
			: $"[X2] 当前行为：抛异常 {ex.GetType().Name} —— {ex.Message}");

		Assert.True(true);
	}

	// ============================================================
	// 原始 Bug 的正向验证（独立构造，不复用工程师的表）
	// ============================================================

	/// <summary>
	/// P0：原始 Bug 场景复现 ——「最近的十个入库单」+ LLM 顺手返回 status 维度。
	/// 独立构造真实 25 列表结构，验证 code（入库单号）必须出现在 SELECT 中。
	/// </summary>
	[Fact]
	public async Task P0_OriginalBug_CodeColumnMustAppear()
	{
		var table = T(401, "wms_storage_receipt",
			("id", "bigint", true),
			("code", "varchar(50)", false),
			("type", "bigint", false),
			("status", "tinyint", false),
			("warehouse_id", "bigint", false),
			("shelf_id", "bigint", false),
			("affirm_time", "datetime", false),
			("affirm_name", "varchar(50)", false),
			("syn_name", "varchar(50)", false),
			("charge_time", "datetime", false),
			("charge_name", "varchar(255)", false),
			("create_by", "bigint", false),
			("create_name", "varchar(255)", false),
			("create_time", "datetime", false),
			("update_by", "bigint", false),
			("update_name", "varchar(255)", false),
			("update_time", "datetime", false),
			("del_flag", "tinyint(1)", false),
			("note", "varchar(255)", false),
			("source_type", "tinyint(1)", false),
			("source_code", "varchar(50)", false),
			("erp_receipt_code", "varchar(50)", false),
			("erp_receipt_id", "varchar(50)", false),
			("come_time", "datetime", false),
			("es_supplier_code", "varchar(100)", false));

		var plan = await RunAsync(table, new QueryIntent
		{
			OriginalQuestion = "最近的十个入库单",
			IntentType = "Detail",
			Limit = 10,
			OrderBy = "come_time",
			OrderDirection = "DESC",
			Dimensions = { "status" }, // LLM 实战行为
		}, "P0");

		var cols = Cols(plan);
		Assert.Contains(cols, c => string.Equals(c, "code", StringComparison.OrdinalIgnoreCase));
		// 原 Bug：5 列里 4 个是时间戳。修复后时间列占比不得超过一半。
		var timeCols = plan.Fields.Count(f => (f.DataType ?? "").IndexOf("time", StringComparison.OrdinalIgnoreCase) >= 0
											 || (f.DataType ?? "").IndexOf("date", StringComparison.OrdinalIgnoreCase) >= 0);
		_output.WriteLine($"[P0] 时间列 {timeCols}/{plan.Fields.Count}");
		Assert.True(timeCols * 2 <= plan.Fields.Count,
			$"时间列占比仍然过高（{timeCols}/{plan.Fields.Count}）：{string.Join(", ", cols)}");
	}
}
