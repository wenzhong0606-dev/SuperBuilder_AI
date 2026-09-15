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
	}
	private sealed class FakeTextBuilder : IMetadataSearchTextBuilder
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
