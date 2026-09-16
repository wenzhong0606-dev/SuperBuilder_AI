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

		var service = new MetadataScannerService(ctx, new FakeReader(), new FakeTextBuilder(), new FakeSemantic(), new FakeVector());
		// 传入的 tenantId(5) 与数据源实际归属租户(7) 不一致 → 必须在写入前拒绝
		await Assert.ThrowsAsync<System.InvalidOperationException>(() => service.ScanAsync(tenantId: 5, dataSourceId: dsId, "x"));
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
		var service = new MetadataScannerService(ctx, new FakeReader(), new FakeTextBuilder(), new FakeSemantic(), new FakeVector(fail: true));

		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			service.ScanAsync(tenantId: 7, dataSourceId: 1, "x", telemetry: telemetry));

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

		var service = new MetadataScannerService(ctx, new DictFakeReader(), new FakeTextBuilder(), new FakeSemantic(), new FakeVector());
		await service.ScanAsync(tenantId: 7, dataSourceId: 1, "x");

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

		var service = new MetadataScannerService(ctx, new WideDictFakeReader(), new FakeTextBuilder(), new FakeSemantic(), new FakeVector());
		await service.ScanAsync(tenantId: 7, dataSourceId: 1, "x");

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
		public Task<List<TableMetadataDto>> GetTablesAsync(string connectionString)
			=> Task.FromResult(new List<TableMetadataDto>
			{
				new() { TableName = "wms_warehouse", TableComment = "仓库" },
				new() { TableName = "wms_storage_receipt", TableComment = "入库凭证" },
				new() { TableName = "sys_dict", TableComment = "字典表" }
			});

		public Task<List<ColumnMetadataDto>> GetColumnsAsync(string connectionString)
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

		public Task<List<ForeignKeyMetadataDto>> GetForeignKeysAsync(string connectionString)
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

		public Task<List<TableMetadataDto>> GetTablesAsync(string connectionString)
			=> Task.FromResult(new List<TableMetadataDto>
			{
				new() { TableName = "js_sys_dict_data", TableComment = "字典数据表" }
			});

		public Task<List<ColumnMetadataDto>> GetColumnsAsync(string connectionString)
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
	}

	private sealed class FakeReader : IDataSourceMetadataReader
	{
		public Task<List<TableMetadataDto>> GetTablesAsync(string connectionString)
			=> Task.FromResult(new List<TableMetadataDto>
			{
				new() { TableName = "Orders", TableComment = "orders" }
			});

		public Task<List<ColumnMetadataDto>> GetColumnsAsync(string connectionString)
			=> Task.FromResult(new List<ColumnMetadataDto>
			{
				new() { TableName = "Orders", ColumnName = "Id", ColumnComment = "id", DataType = "int" }
			});
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
