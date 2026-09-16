using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.Database;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services.BI;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// Phase 2：结果字段译码（code→text）。
/// 覆盖 值映射(列内嵌) / 跨源字典授权守卫 / 取数故障降级 / 纯函数解析。
/// </summary>
public class DisplayResolutionServiceTests
{
	private static SuperBIContext CreateContext(out SqliteConnection connection)
	{
		connection = new SqliteConnection("DataSource=:memory:");
		connection.Open();
		var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
		var ctx = new SuperBIContext(options);
		ctx.Database.EnsureCreated();
		return ctx;
	}

	private static async Task SeedAsync(SuperBIContext ctx)
	{
		ctx.Tenants.Add(new Tenant { Id = 7, TenantCode = "t7", TenantName = "Tenant 7" });
		ctx.DataSources.Add(new DataSource
		{
			Id = 1, TenantId = 7, Name = "wms", NormalizedName = "wms",
			DbType = "MYSQL", ConnectionString = "x",
		});
		ctx.DataSources.Add(new DataSource
		{
			Id = 9, TenantId = 7, Name = "pmis", NormalizedName = "pmis",
			DbType = "MYSQL", ConnectionString = "x",
		});
		ctx.MetadataTables.Add(new MetadataTable
		{
			Id = 10, TenantId = 7, DataSourceId = 1, TableName = "wms_storage_receipt",
		});
		ctx.MetadataDictionaryConfigs.Add(new MetadataDictionaryConfig
		{
			Id = 99, TenantId = 7, DataSourceId = 9, TableName = "sys_dict",
			CodeColumn = "dict_code", NameColumn = "dict_name", TypeColumn = "dict_type",
			IsEnabled = true,
		});
		await ctx.SaveChangesAsync();
	}

	private static QueryPlan Plan() => new()
	{
		DataSourceId = 1,
		Tables =
		{
			new QueryTable { MetadataTableId = 10, DataSourceId = 1, TableName = "wms_storage_receipt" },
		},
	};

	private static QueryResult Rows(params (string Column, object? Value)[] cells)
	{
		var result = new QueryResult { Success = true };
		result.Rows.Add(cells.ToDictionary(c => c.Column, c => c.Value));
		return result;
	}

	[Fact]
	public async Task Enrich_WithColumnValueMapJson_ReplacesCodeWithLabel_InPlace()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedAsync(ctx);

		ctx.MetadataColumns.Add(new MetadataColumn
		{
			MetadataTableId = 10,
			ColumnName = "type",
			ValueMapJson = """{"1":"采购入库","2":"调拨入库"}""",
		});
		await ctx.SaveChangesAsync();

		var result = Rows(("type", "1"));
		result.Rows.Add(new Dictionary<string, object?> { ["type"] = "9" });

		var service = new DisplayResolutionService(ctx, new ThrowingConnectionFactory());
		var report = await service.EnrichAsync(Plan(), result, 7, 42, new long[] { 1 });

		Assert.True(report.AnyApplied);
		Assert.Equal("value-map", report.Hits.Single().Strategy);
		Assert.Equal("采购入库", result.Rows[0]["type"]);
		// 未命中的码值保留原值（不误伤）。
		Assert.Equal("9", result.Rows[1]["type"]);
	}

	[Fact]
	public async Task Enrich_WithUnauthorizedDictionarySource_IsSkippedAndValuePreserved()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedAsync(ctx);

		ctx.MetadataColumns.Add(new MetadataColumn
		{
			MetadataTableId = 10,
			ColumnName = "status",
			IsDictBacked = true,
			DictConfigId = 99,
			DictCategoryValue = "receipt_status",
		});
		await ctx.SaveChangesAsync();

		var result = Rows(("status", "3"));

		var service = new DisplayResolutionService(ctx, new ThrowingConnectionFactory());
		// 仅授权 WMS(1)，未授权 PMIS(9) → 字典路径必须静默降级，绝不跨源越权取数。
		var report = await service.EnrichAsync(Plan(), result, 7, 42, new long[] { 1 });

		Assert.False(report.AnyApplied);
		Assert.Contains(report.Skipped, s => s.Contains("dict-source-unauthorized", StringComparison.Ordinal));
		Assert.Equal("3", result.Rows[0]["status"]);
	}

	[Fact]
	public async Task Enrich_WhenDictionaryLookupFails_DegradesWithoutThrowing()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedAsync(ctx);

		ctx.MetadataColumns.Add(new MetadataColumn
		{
			MetadataTableId = 10,
			ColumnName = "status",
			IsDictBacked = true,
			DictConfigId = 99,
			DictCategoryValue = "receipt_status",
		});
		await ctx.SaveChangesAsync();

		var result = Rows(("status", "3"));

		var service = new DisplayResolutionService(ctx, new ThrowingConnectionFactory());
		// 已授权 PMIS，但连接失败 → 只降级，不得把异常抛给主流程。
		var report = await service.EnrichAsync(Plan(), result, 7, 42, new long[] { 1, 9 });

		Assert.False(report.AnyApplied);
		Assert.Equal("3", result.Rows[0]["status"]);
	}

	[Fact]
	public async Task Enrich_ReplacesNumericColumnAgainstStringCodeMap()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedAsync(ctx);

		ctx.MetadataColumns.Add(new MetadataColumn
		{
			MetadataTableId = 10,
			ColumnName = "type",
			ValueMapJson = """{"1":"采购入库"}""",
		});
		await ctx.SaveChangesAsync();

		// MySQL 常返回 decimal/int：1 必须能匹配 "1"。
		var result = Rows(("type", 1));

		var service = new DisplayResolutionService(ctx, new ThrowingConnectionFactory());
		var report = await service.EnrichAsync(Plan(), result, 7, 42, new long[] { 1 });

		Assert.True(report.AnyApplied);
		Assert.Equal("采购入库", result.Rows[0]["type"]);
	}

	/// <summary>
	/// PMIS 实测形态：同一列码值跨多个 dict_type。
	/// WMS 单据表 <c>type</c> 的码值分属 warehousing_type / outbound_type / …，
	/// 单分类过滤会漏译，故分类须支持多值（→ <c>dict_type IN (...)</c>）。
	/// </summary>
	[Fact]
	public async Task Enrich_WithMultiCategoryDictionary_ResolvesCodesAcrossCategories()
	{
		var ctx = CreateContext(out var metaConnection);
		await using var _ = metaConnection;
		await using var __ = ctx;
		await SeedAsync(ctx);

		ctx.MetadataColumns.Add(new MetadataColumn
		{
			MetadataTableId = 10,
			ColumnName = "type",
			IsDictBacked = true,
			DictConfigId = 99,
			DictCategoryValue = "warehousing_type, outbound_type",
		});
		await ctx.SaveChangesAsync();

		var connectionString = $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared";
		await using var keeper = new SqliteConnection(connectionString);
		await keeper.OpenAsync();
		using (var cmd = keeper.CreateCommand())
		{
			cmd.CommandText =
				"CREATE TABLE sys_dict(dict_code TEXT, dict_name TEXT, dict_type TEXT);"
				+ "INSERT INTO sys_dict VALUES('1581881771247636480','生产完工入库','warehousing_type');"
				+ "INSERT INTO sys_dict VALUES('1582297407643815936','销售发货','outbound_type');"
				+ "INSERT INTO sys_dict VALUES('999','其他分类，不应命中','other_type');";
			cmd.ExecuteNonQuery();
		}

		// 雪花 ID 以 long 形态从业务库返回。
		var result = Rows(("type", 1581881771247636480L));
		result.Rows.Add(new Dictionary<string, object?> { ["type"] = 1582297407643815936L });

		var service = new DisplayResolutionService(ctx, new SharedMemoryFactory(connectionString));
		var report = await service.EnrichAsync(Plan(), result, 7, 42, new long[] { 1, 9 });

		Assert.True(report.AnyApplied);
		Assert.Equal("dictionary", report.Hits.Single().Strategy);
		Assert.Equal("生产完工入库", result.Rows[0]["type"]);
		Assert.Equal("销售发货", result.Rows[1]["type"]);
	}

	/// <summary>字典表带软删标记时，已删除/停用的码值不得被译出。</summary>
	[Fact]
	public async Task Enrich_DictionaryRespectsActiveFilter_ExcludesSoftDeletedCodes()
	{
		var ctx = CreateContext(out var metaConnection);
		await using var _ = metaConnection;
		await using var __ = ctx;
		await SeedAsync(ctx);

		var config = await ctx.MetadataDictionaryConfigs.SingleAsync(x => x.Id == 99);
		config.ActiveFilterColumn = "status";
		config.ActiveFilterValue = "0";

		ctx.MetadataColumns.Add(new MetadataColumn
		{
			MetadataTableId = 10,
			ColumnName = "type",
			IsDictBacked = true,
			DictConfigId = 99,
			DictCategoryValue = "warehousing_type",
		});
		await ctx.SaveChangesAsync();

		var connectionString = $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared";
		await using var keeper = new SqliteConnection(connectionString);
		await keeper.OpenAsync();
		using (var cmd = keeper.CreateCommand())
		{
			cmd.CommandText =
				"CREATE TABLE sys_dict(dict_code TEXT, dict_name TEXT, dict_type TEXT, status TEXT);"
				+ "INSERT INTO sys_dict VALUES('1','正常项','warehousing_type','0');"
				+ "INSERT INTO sys_dict VALUES('2','已删除项','warehousing_type','1');"
				+ "INSERT INTO sys_dict VALUES('3','已停用项','warehousing_type','2');";
			cmd.ExecuteNonQuery();
		}

		var result = Rows(("type", "1"));
		result.Rows.Add(new Dictionary<string, object?> { ["type"] = "2" });
		result.Rows.Add(new Dictionary<string, object?> { ["type"] = "3" });

		var service = new DisplayResolutionService(ctx, new SharedMemoryFactory(connectionString));
		var report = await service.EnrichAsync(Plan(), result, 7, 42, new long[] { 1, 9 });

		Assert.True(report.AnyApplied);
		Assert.Equal("正常项", result.Rows[0]["type"]);
		// 软删/停用码值不译，保留原值。
		Assert.Equal("2", result.Rows[1]["type"]);
		Assert.Equal("3", result.Rows[2]["type"]);
	}

	/// <summary>
	/// 配置了分类列但列上未声明分类：必须如实标记为未指定，且**不得**退化为全表拉取
	/// （否则跨分类同码值会被错误译码）。此处连接工厂必抛，若发生取数即证明退化。
	/// </summary>
	[Fact]
	public async Task Enrich_DictionaryWithTypeColumnButNoCategory_IsSkippedWithoutFullScan()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		await SeedAsync(ctx);

		ctx.MetadataColumns.Add(new MetadataColumn
		{
			MetadataTableId = 10,
			ColumnName = "type",
			IsDictBacked = true,
			DictConfigId = 99,
			// 未声明 DictCategoryValue。
		});
		await ctx.SaveChangesAsync();

		var result = Rows(("type", "3"));

		var service = new DisplayResolutionService(ctx, new ThrowingConnectionFactory());
		var report = await service.EnrichAsync(Plan(), result, 7, 42, new long[] { 1, 9 });

		Assert.False(report.AnyApplied);
		Assert.Contains(report.Skipped, s => s.Contains("dict-category-unspecified", StringComparison.Ordinal));
		Assert.Equal("3", result.Rows[0]["type"]);
	}

	[Fact]
	public void SplitCategories_SplitsDedupesAndCapsInput()
	{
		Assert.Equal("a,b", string.Join(",", DisplayResolutionService.SplitCategories("a,b")));
		Assert.Equal("a,b", string.Join(",", DisplayResolutionService.SplitCategories(" a ； b ")));
		// 重复项去重（大小写不敏感），保持首次出现顺序。
		Assert.Equal("a,b", string.Join(",", DisplayResolutionService.SplitCategories("a|b;a")));
		Assert.Empty(DisplayResolutionService.SplitCategories(null));
		Assert.Empty(DisplayResolutionService.SplitCategories("  "));
	}

	[Fact]
	public void ParseValueMapJson_SupportsObjectAndArrayForms()	{
		var fromObject = DisplayResolutionService.ParseValueMapJson("""{"1":"采购入库","2":"调拨入库"}""");
		Assert.Equal("采购入库", fromObject["1"]);
		Assert.Equal("调拨入库", fromObject["2"]);

		var fromArray = DisplayResolutionService.ParseValueMapJson(
			"""[{"code":"A","label":"正常"},{"value":"B","name":"停用"}]""");
		Assert.Equal("正常", fromArray["A"]);
		Assert.Equal("停用", fromArray["B"]);

		Assert.Empty(DisplayResolutionService.ParseValueMapJson("not-json"));
		Assert.Empty(DisplayResolutionService.ParseValueMapJson(null));
	}

	[Fact]
	public void NormalizeCode_TrimsNumericNoise()
	{
		Assert.Equal("1", DisplayResolutionService.NormalizeCode(1m));
		Assert.Equal("1", DisplayResolutionService.NormalizeCode(1.0d));
		Assert.Equal("1.5", DisplayResolutionService.NormalizeCode(1.5m));
		Assert.Equal("A", DisplayResolutionService.NormalizeCode("  A  "));
		Assert.Equal(string.Empty, DisplayResolutionService.NormalizeCode(null));
	}

	/// <summary>
	/// MySQL tinyint(1) 被驱动物化为 bool，必须归一化为 "1"/"0"。
	/// 否则 "True"/"False" 与字典、注释图例里的 "1"/"0" 永不匹配
	/// —— 实测 wms_storage_receipt.source_type 译码失效即此因。
	/// </summary>
	[Fact]
	public void NormalizeCode_BoolFromTinyInt_MapsToZeroOne()
	{
		Assert.Equal("1", DisplayResolutionService.NormalizeCode(true));
		Assert.Equal("0", DisplayResolutionService.NormalizeCode(false));
		Assert.Equal("1", DisplayResolutionService.NormalizeCode((byte)1));
		Assert.Equal("0", DisplayResolutionService.NormalizeCode((byte)0));
	}

	[Fact]
	public async Task Enrich_WithLearnedDictionaryCategory_ResolvesAcrossDataSource()
	{
		var ctx = CreateContext(out var metaConnection);
		await using var _ = metaConnection;
		await using var __ = ctx;
		await SeedAsync(ctx);

		ctx.MetadataColumns.Add(new MetadataColumn { MetadataTableId = 10, ColumnName = "type" });
		await ctx.SaveChangesAsync();

		// 业务库（PMIS 字典）：sys_dict(dict_code, dict_name, dict_type)
		var connectionString = $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared";
		await using var keeper = new SqliteConnection(connectionString);
		await keeper.OpenAsync();
		using (var cmd = keeper.CreateCommand())
		{
			cmd.CommandText =
				"CREATE TABLE sys_dict(dict_code TEXT, dict_name TEXT, dict_type TEXT);"
				+ "INSERT INTO sys_dict VALUES('1','采购入库','receipt_type');"
				+ "INSERT INTO sys_dict VALUES('2','调拨入库','receipt_type');"
				+ "INSERT INTO sys_dict VALUES('1','已完成','receipt_status');";
			cmd.ExecuteNonQuery();
		}

		// 用户声明「type 应显示 receipt_type 名称」→ 按分类查 PMIS 字典（跨源，须已授权）。
		var learned = new CorrectionResolution
		{
			Corrections =
			{
				new LearnedCorrection
				{
					Kind = CorrectionKind.ColumnDisplay,
					Payload = new CorrectionPayload { ColumnName = "type", CategoryHint = "receipt_type" },
				},
			},
		};

		var result = Rows(("type", "1"));

		var service = new DisplayResolutionService(ctx, new SharedMemoryFactory(connectionString));
		var report = await service.EnrichAsync(Plan(), result, 7, 42, new long[] { 1, 9 }, learned);

		Assert.True(report.AnyApplied);
		Assert.Equal("learned-dictionary", report.Hits.Single().Strategy);
		Assert.Equal("采购入库", result.Rows[0]["type"]);
	}

	[Fact]
	public async Task Enrich_WithScannedForeignKey_WritesDisplayName()
	{
		var ctx = CreateContext(out var metaConnection);
		await using var _ = metaConnection;
		await using var __ = ctx;
		await SeedAsync(ctx);

		// 扫描已捕获的外键角色：warehouse_id → wms_warehouse.warehouse_name
		ctx.MetadataColumns.Add(new MetadataColumn
		{
			MetadataTableId = 10,
			ColumnName = "warehouse_id",
			ReferencedTable = "wms_warehouse",
			ReferencedColumn = "id",
			ReferencedDisplayColumn = "warehouse_name",
		});
		await ctx.SaveChangesAsync();

		var connectionString = $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared";
		await using var keeper = new SqliteConnection(connectionString);
		await keeper.OpenAsync();
		using (var cmd = keeper.CreateCommand())
		{
			cmd.CommandText =
				"CREATE TABLE wms_warehouse(id TEXT, warehouse_name TEXT);"
				+ "INSERT INTO wms_warehouse VALUES('1','中心仓');";
			cmd.ExecuteNonQuery();
		}

		var result = Rows(("warehouse_id", "1"));

		var service = new DisplayResolutionService(ctx, new SharedMemoryFactory(connectionString));
		var report = await service.EnrichAsync(Plan(), result, 7, 42, new long[] { 1 });

		Assert.True(report.AnyApplied);
		Assert.Equal("foreign-key", report.Hits.Single().Strategy);
		Assert.Equal("中心仓", result.Rows[0]["warehouse_id"]);
	}

	/// <summary>连接工厂假件：任何取数请求都失败，用于验证降级路径。</summary>
	private sealed class ThrowingConnectionFactory : IDataSourceConnectionFactory
	{
		public Task<DbConnection> CreateAsync(long dataSourceId)
			=> throw new InvalidOperationException("no-connection");
	}

	/// <summary>
	/// 共享内存库连接工厂：每次返回新连接指向同一内存库，
	/// 以便服务按自身生命周期 Dispose 连接而不破坏数据。
	/// </summary>
	private sealed class SharedMemoryFactory : IDataSourceConnectionFactory
	{
		private readonly string _connectionString;

		public SharedMemoryFactory(string connectionString) => _connectionString = connectionString;

		public async Task<DbConnection> CreateAsync(long dataSourceId)
		{
			var connection = new SqliteConnection(_connectionString);
			await connection.OpenAsync();
			return connection;
		}
	}
}
