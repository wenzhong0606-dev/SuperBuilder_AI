using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.DTO;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services;
using SuperBuilder_AI.Application.Metadata;
using SuperBuilder_AI.Application.Common.Options;
using Microsoft.Extensions.Options;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M0-06：元数据扫描写入必须强制从 DataSource 继承 TenantId。
/// 当调用方传入的 tenantId 与数据源实际归属租户不一致时，必须在写入前拒绝。
/// </summary>
public class MetadataScannerServiceTests
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

	[Fact]
	public async Task ScanAsync_TenantMismatchWithDataSource_Rejects()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		ctx.Tenants.Add(new Tenant { Id = 7, TenantCode = "t7", TenantName = "Tenant 7" });
		ctx.DataSources.Add(new DataSource { Id = 1, TenantId = 7, Name = "other", NormalizedName = "other", DbType = "SQLSERVER", ConnectionString = "x" });
		await ctx.SaveChangesAsync();
		var dsId = ctx.DataSources.First().Id;

		var service = new MetadataScannerService(ctx, new FakeReader(), new FakeTextBuilder(), new FakeSemantic(), new FakeVector(), new VectorBackfillGate(Options.Create(new Features())));
		// 传入的 tenantId(5) 与数据源实际归属租户(7) 不一致 → 必须在写入前拒绝
		await Assert.ThrowsAsync<System.InvalidOperationException>(() => service.ScanAsync(tenantId: 5, dataSourceId: dsId, "x", batchVersion: 1, seedVersion: 0));
	}

	[Fact]
	public async Task ActivateAsync_QueuesOldVersionVectorsForGc()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		ctx.Tenants.Add(new Tenant { Id = 7, TenantCode = "t7", TenantName = "Tenant 7" });
		var source = new DataSource { Id = 1, TenantId = 7, Name = "ds", NormalizedName = "ds", DbType = "SQLSERVER", ConnectionString = "x", ActiveMetadataVersion = 0, VectorsBackfilled = true };
		ctx.DataSources.Add(source);
		ctx.MetadataTables.Add(new MetadataTable { TenantId = 7, DataSourceId = 1, TableName = "orders", MetadataVersion = 0, VectorId = "old-table", Columns = { new MetadataColumn { ColumnName = "id", DataType = "int", MetadataVersion = 0, VectorId = "old-column" } } });
		ctx.MetadataTables.Add(new MetadataTable { TenantId = 7, DataSourceId = 1, TableName = "orders", MetadataVersion = 1, VectorId = "new-table", VectorStatus = "Synced", Columns = { new MetadataColumn { ColumnName = "id", DataType = "int", MetadataVersion = 1, VectorId = "new-column", VectorStatus = "Synced" } } });
		await ctx.SaveChangesAsync();

		var job = new MetadataScanJob { TenantId = 7, DataSourceId = 1, BatchVersion = 1, SeedVersion = 0 };
		var scanner = new MetadataScannerService(ctx, new FakeReader(), new FakeTextBuilder(), new FakeSemantic(), new FakeVector(), new VectorBackfillGate(Options.Create(new Features())));
		await scanner.ActivateAsync(job, source);

		Assert.Equal(1, source.ActiveMetadataVersion);
		var request = Assert.Single(ctx.MetadataVectorGcRequests);
		Assert.Equal("StagingGc", request.Reason);
		Assert.Equal(0, request.OldVersion);
		var ids = System.Text.Json.JsonSerializer.Deserialize<List<string>>(request.PayloadJson!);
		Assert.Equal(new[] { "old-table", "old-column" }, ids);
	}

	[Fact]
	public async Task ActivateAsync_BlocksExistingVectorIdsWithFailedStatus()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		ctx.Tenants.Add(new Tenant { Id = 7, TenantCode = "t7", TenantName = "Tenant 7" });
		var source = new DataSource { Id = 1, TenantId = 7, Name = "ds", NormalizedName = "ds", DbType = "SQLSERVER", ConnectionString = "x", ActiveMetadataVersion = 0, VectorsBackfilled = true };
		ctx.DataSources.Add(source);
		ctx.MetadataTables.Add(new MetadataTable
		{
			TenantId = 7, DataSourceId = 1, TableName = "orders", CatalogName = "db", SchemaName = "dbo",
			MetadataVersion = 1, VectorId = "table-vector", VectorStatus = "Failed",
			Columns = { new MetadataColumn { ColumnName = "id", DataType = "int", MetadataVersion = 1, VectorId = "column-vector", VectorStatus = "Synced" } }
		});
		await ctx.SaveChangesAsync();

		var job = new MetadataScanJob { TenantId = 7, DataSourceId = 1, BatchVersion = 1, SeedVersion = 0 };
		var scanner = new MetadataScannerService(ctx, new FakeReader(), new FakeTextBuilder(), new FakeSemantic(), new FakeVector(), new VectorBackfillGate(Options.Create(new Features())));
		var error = await Assert.ThrowsAsync<MetadataActivationBlockedException>(() => scanner.ActivateAsync(job, source));
		Assert.Equal("vector_index_incomplete", error.Reason);
		var failedTable = Assert.Single(error.IncompleteVectorTables);
		Assert.Equal("db", failedTable.CatalogName);
		Assert.Equal("dbo", failedTable.SchemaName);
		Assert.Equal("orders", failedTable.TableName);
		Assert.Equal(0, source.ActiveMetadataVersion);
	}

	[Fact]
	public async Task ActivateAsync_ReportsEveryTableWithIncompleteVectors()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		ctx.Tenants.Add(new Tenant { Id = 7, TenantCode = "t7", TenantName = "Tenant 7" });
		var source = new DataSource { Id = 1, TenantId = 7, Name = "ds", NormalizedName = "ds", DbType = "SQLSERVER", ConnectionString = "x", VectorsBackfilled = true };
		ctx.DataSources.Add(source);
		foreach (var schema in new[] { "a", "b" })
			ctx.MetadataTables.Add(new MetadataTable { TenantId = 7, DataSourceId = 1, CatalogName = "db", SchemaName = schema, TableName = "orders", MetadataVersion = 1, VectorId = "table-" + schema, VectorStatus = "Synced", Columns = { new MetadataColumn { ColumnName = "id", DataType = "int", MetadataVersion = 1, VectorStatus = "Failed" } } });
		await ctx.SaveChangesAsync();
		var scanner = new MetadataScannerService(ctx, new FakeReader(), new FakeTextBuilder(), new FakeSemantic(), new FakeVector(), new VectorBackfillGate(Options.Create(new Features())));
		var error = await Assert.ThrowsAsync<MetadataActivationBlockedException>(() => scanner.ActivateAsync(new MetadataScanJob { TenantId = 7, DataSourceId = 1, BatchVersion = 1 }, source));
		Assert.Equal(new[] { "a", "b" }, error.IncompleteVectorTables.Select(t => t.SchemaName).OrderBy(x => x));
		Assert.Equal(0, source.ActiveMetadataVersion);
	}

	[Fact]
	public async Task ScanAsync_ColumnReadFailure_RetainsFailedTableAndReportsPartialResult()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;
		ctx.Tenants.Add(new Tenant { Id = 7, TenantCode = "t7", TenantName = "Tenant 7" });
		ctx.DataSources.Add(new DataSource { Id = 1, TenantId = 7, Name = "ds", NormalizedName = "ds", DbType = "SQLSERVER", ConnectionString = "x" });
		ctx.MetadataTables.Add(new MetadataTable
		{
			TenantId = 7, DataSourceId = 1, TableName = "broken", CatalogName = "db", SchemaName = "dbo",
			Columns = { new MetadataColumn { ColumnName = "old_column", DataType = "int" } }
		});
		await ctx.SaveChangesAsync();

		var reader = new PartiallyFailingReader();
		var scanner = new MetadataScannerService(ctx, reader, new FakeTextBuilder(), new FakeSemantic(), new FakeVector(), new VectorBackfillGate(Options.Create(new Features())));
		await scanner.PrepareStagingAsync(7, 1, 1, 0);
		var failures = new List<ScanTableFailure>();
		await scanner.ScanAsync(7, 1, "x", 1, 0, cleanupOrphans: true, failedTables: failures);

		var failure = Assert.Single(failures);
		Assert.Equal("broken", failure.TableName);
		Assert.Equal(3, failure.Attempts);
		Assert.Equal(3, reader.BrokenAttempts);
		var staged = await ctx.MetadataTables.Include(t => t.Columns).Where(t => t.MetadataVersion == 1).ToListAsync();
		Assert.Contains(staged, t => t.TableName == "broken" && t.Columns.Any(c => c.ColumnName == "old_column"));
		Assert.Contains(staged, t => t.TableName == "healthy");
		Assert.Equal(1, staged.Single(t => t.TableName == "healthy").Columns.Single().MetadataVersion);
	}

	private sealed class PartiallyFailingReader : IDataSourceMetadataReader
	{
		public int BrokenAttempts { get; private set; }
		public Task<List<TableMetadataDto>> GetTablesAsync(string connectionString, CancellationToken ct = default)
			=> Task.FromResult(new List<TableMetadataDto>
			{
				new() { CatalogName = "db", SchemaName = "dbo", TableName = "broken" },
				new() { CatalogName = "db", SchemaName = "dbo", TableName = "healthy" }
			});
		public Task<List<ColumnMetadataDto>> GetColumnsAsync(string connectionString, CancellationToken ct = default)
			=> throw new InvalidOperationException("Full column read must not be used.");
		public Task<List<ColumnMetadataDto>> GetColumnsAsync(string connectionString, string? dbType, IEnumerable<string> tableNames, CancellationToken ct = default)
		{
			if (tableNames.Single() == "broken")
			{
				BrokenAttempts++;
				throw new InvalidOperationException("Column read failed.");
			}
			return Task.FromResult(new List<ColumnMetadataDto> { new() { CatalogName = "db", SchemaName = "dbo", TableName = "healthy", ColumnName = "id", DataType = "int" } });
		}
	}

	[Fact]
	public async Task ScanAsync_VectorIndexFailure_FailsScanWithTelemetry()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		ctx.Tenants.Add(new Tenant { Id = 7, TenantCode = "t7", TenantName = "Tenant 7" });
		ctx.DataSources.Add(new DataSource { Id = 1, TenantId = 7, Name = "ds", NormalizedName = "ds", DbType = "SQLSERVER", ConnectionString = "x" });
		await ctx.SaveChangesAsync();

		var telemetry = new ScanTelemetry();
		var service = new MetadataScannerService(ctx, new FakeReader(), new FakeTextBuilder(), new FakeSemantic(), new FakeVector(fail: true), new VectorBackfillGate(Options.Create(new Features())));

		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			service.ScanAsync(tenantId: 7, dataSourceId: 1, "x", batchVersion: 1, seedVersion: 0, telemetry: telemetry));

		Assert.Contains("向量索引失败", ex.Message);
		Assert.Equal("Failed", ctx.MetadataTables.Single().VectorStatus);
		Assert.Contains(telemetry.Details.Events, e => e.Level == "Error" && e.EventCode == "VectorIndexFailed");
	}

	[Fact]
	public async Task ScanAsync_ForeignKeysAndDictionaryTable_PopulateRolesAndConfig()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		ctx.Tenants.Add(new Tenant { Id = 7, TenantCode = "t7", TenantName = "Tenant 7" });
		ctx.DataSources.Add(new DataSource { Id = 1, TenantId = 7, Name = "wms", NormalizedName = "wms", DbType = "MYSQL", ConnectionString = "x" });
		await ctx.SaveChangesAsync();

		var service = new MetadataScannerService(ctx, new DictFakeReader(), new FakeTextBuilder(), new FakeSemantic(), new FakeVector(), new VectorBackfillGate(Options.Create(new Features())));
		await service.ScanAsync(tenantId: 7, dataSourceId: 1, "x", batchVersion: 1, seedVersion: 0);

		// 外键角色落库：warehouse_id → wms_warehouse（展示列优选 warehouse_name）。
		var fkColumn = await ctx.MetadataColumns
			.Include(c => c.MetadataTable)
			.SingleAsync(c => c.ColumnName == "warehouse_id");
		Assert.Equal("wms_warehouse", fkColumn.ReferencedTable);
		Assert.Equal("id", fkColumn.ReferencedColumn);
		Assert.Equal("warehouse_name", fkColumn.ReferencedDisplayColumn);

		// 字典表启发式：sys_dict 被识别并记录角色映射（per 数据源 + 租户）。
		var dict = await ctx.MetadataDictionaryConfigs.SingleAsync();
		Assert.Equal("sys_dict", dict.TableName);
		Assert.Equal("dict_code", dict.CodeColumn);
		Assert.Equal("dict_name", dict.NameColumn);
		Assert.Equal("dict_type", dict.TypeColumn);
		Assert.Equal(1, dict.DataSourceId);
		Assert.Equal(7, dict.TenantId);
	}

	/// <summary>
	/// 实测回归：JeeSite/JNPF 系字典表（如 PMIS <c>js_sys_dict_data</c>，44 列，
	/// 含 <c>parent_codes</c> / <c>tree_names</c> / <c>extend_*</c>）必须仍被识别，
	/// 且角色不能落到噪声列上。
	/// </summary>
	[Fact]
	public async Task ScanAsync_WideJeeSiteDictionaryTable_StillDiscoveredWithCorrectRoles()
	{
		var ctx = CreateContext(out var connection);
		await using var _ = connection;
		await using var __ = ctx;

		ctx.Tenants.Add(new Tenant { Id = 7, TenantCode = "t7", TenantName = "Tenant 7" });
		ctx.DataSources.Add(new DataSource { Id = 1, TenantId = 7, Name = "pmis", NormalizedName = "pmis", DbType = "MYSQL", ConnectionString = "x" });
		await ctx.SaveChangesAsync();

		var service = new MetadataScannerService(ctx, new WideDictFakeReader(), new FakeTextBuilder(), new FakeSemantic(), new FakeVector(), new VectorBackfillGate(Options.Create(new Features())));
		await service.ScanAsync(tenantId: 7, dataSourceId: 1, "x", batchVersion: 1, seedVersion: 0);

		var dict = await ctx.MetadataDictionaryConfigs.SingleAsync();
		Assert.Equal("js_sys_dict_data", dict.TableName);
		// parent_codes 不得被当成 code、tree_names 不得被当成 name。
		Assert.Equal("dict_code", dict.CodeColumn);
		Assert.Equal("dict_label", dict.NameColumn);
		Assert.Equal("dict_type", dict.TypeColumn);
	}

	/// <summary>含外键与字典表的假读取器：验证 FK 角色与字典表发现链路。</summary>
	private sealed class DictFakeReader : IDataSourceMetadataReader
	{
		public Task<List<TableMetadataDto>> GetTablesAsync(string connectionString, CancellationToken ct = default)
			=> Task.FromResult(new List<TableMetadataDto>
			{
				new() { TableName = "wms_warehouse", TableComment = "仓库" },
				new() { TableName = "wms_storage_receipt", TableComment = "入库凭证" },
				new() { TableName = "sys_dict", TableComment = "字典表" }
			});

		public Task<List<ColumnMetadataDto>> GetColumnsAsync(string connectionString, CancellationToken ct = default)
			=> Task.FromResult(new List<ColumnMetadataDto>
			{
				new() { TableName = "wms_warehouse", ColumnName = "id", DataType = "int", IsPrimaryKey = true },
				new() { TableName = "wms_warehouse", ColumnName = "warehouse_name", DataType = "varchar" },
				new() { TableName = "wms_storage_receipt", ColumnName = "id", DataType = "int", IsPrimaryKey = true },
				new() { TableName = "wms_storage_receipt", ColumnName = "warehouse_id", DataType = "int" },
				new() { TableName = "wms_storage_receipt", ColumnName = "type", DataType = "varchar" },
				new() { TableName = "sys_dict", ColumnName = "dict_code", DataType = "varchar" },
				new() { TableName = "sys_dict", ColumnName = "dict_name", DataType = "varchar" },
				new() { TableName = "sys_dict", ColumnName = "dict_type", DataType = "varchar" }
			});

		public Task<List<ColumnMetadataDto>> GetColumnsAsync(string connectionString, string? dbType, IEnumerable<string> tableNames, CancellationToken ct = default)
			=> GetColumnsAsync(connectionString, ct);

		public Task<List<ForeignKeyMetadataDto>> GetForeignKeysAsync(string connectionString, CancellationToken ct = default)
			=> Task.FromResult(new List<ForeignKeyMetadataDto>
			{
				new()
				{
					TableName = "wms_storage_receipt",
					ColumnName = "warehouse_id",
					ReferencedTableName = "wms_warehouse",
					ReferencedColumnName = "id"
				}
			});
	}

	/// <summary>
	/// 复刻 PMIS <c>js_sys_dict_data</c> 的真实形态：44 列，语义列仅 5 个，
	/// 混入会误命中 code / name 角色的 <c>parent_codes</c> / <c>tree_names</c>。
	/// </summary>
	private sealed class WideDictFakeReader : IDataSourceMetadataReader
	{
		private static readonly string[] Semantic =
		{
			"dict_value", "dict_type", "dict_label", "dict_icon"
		};

		private static readonly string[] Noise =
		{
			"is_sys", "parent_code", "parent_codes", "remarks", "status",
			"tree_leaf", "tree_level", "tree_names", "tree_sort", "tree_sorts",
			"update_by", "update_date", "extend_s1", "extend_s2", "extend_s3",
			"extend_s4", "extend_s5", "extend_s6", "extend_s7", "extend_s8",
			"extend_json", "extend_i1", "extend_i2", "extend_i3", "extend_i4",
			"extend_f1", "extend_f2", "extend_f3", "extend_f4",
			"extend_d1", "extend_d2", "extend_d3", "extend_d4",
			"description", "css_style", "css_class", "create_date", "create_by",
			"corp_name", "corp_code"
		};

		public Task<List<TableMetadataDto>> GetTablesAsync(string connectionString, CancellationToken ct = default)
			=> Task.FromResult(new List<TableMetadataDto>
			{
				new() { TableName = "js_sys_dict_data", TableComment = "字典数据表" }
			});

		public Task<List<ColumnMetadataDto>> GetColumnsAsync(string connectionString, CancellationToken ct = default)
		{
			var list = new List<ColumnMetadataDto>
			{
				new() { TableName = "js_sys_dict_data", ColumnName = "dict_code", DataType = "bigint", IsPrimaryKey = true },
			};
			foreach (var n in Semantic)
				list.Add(new ColumnMetadataDto { TableName = "js_sys_dict_data", ColumnName = n, DataType = "varchar" });
			foreach (var n in Noise)
				list.Add(new ColumnMetadataDto { TableName = "js_sys_dict_data", ColumnName = n, DataType = "varchar" });

			return Task.FromResult(list);
		}

		public Task<List<ColumnMetadataDto>> GetColumnsAsync(string connectionString, string? dbType, IEnumerable<string> tableNames, CancellationToken ct = default)
			=> GetColumnsAsync(connectionString, ct);
	}

	private sealed class FakeReader : IDataSourceMetadataReader
	{
		public Task<List<TableMetadataDto>> GetTablesAsync(string connectionString, CancellationToken ct = default)
			=> Task.FromResult(new List<TableMetadataDto>
			{
				new() { TableName = "Orders", TableComment = "orders" }
			});

		public Task<List<ColumnMetadataDto>> GetColumnsAsync(string connectionString, CancellationToken ct = default)
			=> Task.FromResult(new List<ColumnMetadataDto>
			{
				new() { TableName = "Orders", ColumnName = "Id", ColumnComment = "id", DataType = "int" }
			});

		public Task<List<ColumnMetadataDto>> GetColumnsAsync(string connectionString, string? dbType, IEnumerable<string> tableNames, CancellationToken ct = default)
			=> GetColumnsAsync(connectionString, ct);
	}	private sealed class FakeTextBuilder : IMetadataSearchTextBuilder
	{
		public string BuildTableText(string? tableName, string? tableComment, string? businessDomain) => $"{tableName} {tableComment}";
		public string BuildColumnText(string? tableName, string? columnName, string? columnComment, string? dataType) => $"{tableName} {columnName} {columnComment} {dataType}";
		public string BuildMetadataText(string? tableName, string? tableComment, IEnumerable<string> columnTexts) => $"{tableName} {tableComment} {string.Join(' ', columnTexts)}";
	}
	private sealed class FakeSemantic : IMetadataSemanticService
	{
		public Task<MetadataSemantic?> GenerateAsync(MetadataColumn column) => Task.FromResult<MetadataSemantic?>(null);
		public Task<List<MetadataSemantic>> GenerateBatchAsync(List<MetadataColumn> columns) => Task.FromResult(new List<MetadataSemantic>());
	}
	private sealed class FakeVector : IMetadataVectorService
	{
		private readonly bool _fail;

		public FakeVector(bool fail = false) => _fail = fail;

		public Task<MetadataVectorIndexResult> IndexAsync(MetadataTable metadataTable, CancellationToken ct = default)
		{
			if (_fail)
			{
				metadataTable.VectorStatus = "Failed";
				metadataTable.VectorErrorCode = "VECTOR_TIMEOUT";
				foreach (var column in metadataTable.Columns)
				{
					column.VectorStatus = "Failed";
					column.VectorErrorCode = "VECTOR_TIMEOUT";
				}

				return Task.FromResult(new MetadataVectorIndexResult());
			}

			metadataTable.VectorStatus = "Synced";
			return Task.FromResult(new MetadataVectorIndexResult
			{
				TableVectorId = "table-vector"
			});
		}
	}
}
