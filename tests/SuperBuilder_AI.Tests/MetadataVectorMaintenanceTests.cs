using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Application.Metadata;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Interfaces.Audit;
using SuperBuilder_AI.Models.AI;
using SuperBuilder_AI.Models.Audit;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.Organization;
using Xunit;

namespace SuperBuilder_AI.Tests;

public sealed class MetadataVectorMaintenanceTests
{
	private static SuperBIContext CreateContext(out SqliteConnection connection)
	{
		connection = new SqliteConnection("DataSource=:memory:");
		connection.Open();
		var context = new SuperBIContext(new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options);
		context.Database.EnsureCreated();
		return context;
	}

	[Fact]
	public async Task Backfill_MissingStoredPoint_DoesNotMarkSourceComplete()
	{
		await using var context = CreateContext(out var connection);
		await using var _ = connection;
		context.Tenants.Add(new Tenant { Id = 7, TenantCode = "t7", TenantName = "Tenant 7" });
		context.DataSources.Add(new DataSource { Id = 1, TenantId = 7, Name = "ds", NormalizedName = "ds", DbType = "SQLSERVER", ConnectionString = "x" });
		context.MetadataTables.Add(new MetadataTable { TenantId = 7, DataSourceId = 1, TableName = "orders", VectorId = "missing-point" });
		await context.SaveChangesAsync();

		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			new MetadataVectorBackfillJob(context, new FakeQdrant()).RunAsync());

		Assert.False((await context.DataSources.SingleAsync()).VectorsBackfilled);
	}

	[Theory]
	[InlineData("Failed")]
	[InlineData("Running")]
	public async Task Gc_InterruptedOrFailedRequest_Retries(string status)
	{
		await using var context = CreateContext(out var connection);
		await using var _ = connection;
		context.MetadataVectorGcRequests.Add(new MetadataVectorGcRequest
		{
			DataSourceId = 1, TenantId = 7, Reason = "DataSourceDeleted", Status = status,
			LastAttemptAt = DateTime.UtcNow.AddHours(-1)
		});
		await context.SaveChangesAsync();
		var qdrant = new FakeQdrant();
		qdrant.Points["old-point"] = (new float[] { 1 }, new Dictionary<string, object> { ["data_source_id"] = 1L });

		await new MetadataVectorGcJob(context, qdrant, new FakeAudit()).RunAsync();

		Assert.Equal("Done", (await context.MetadataVectorGcRequests.SingleAsync()).Status);
		Assert.Empty(qdrant.Points);
	}

	private sealed class FakeQdrant : IQdrantService
	{
		public Dictionary<string, (float[] Vector, IReadOnlyDictionary<string, object> Payload)> Points { get; } = new();
		public Task CreateCollectionAsync() => Task.CompletedTask;
		public Task<bool> ExistsAsync() => Task.FromResult(true);
		public Task RecreateCollectionAsync() => Task.CompletedTask;
		public Task UpsertAsync(string id, float[] vector, Dictionary<string, object> payload, CancellationToken ct = default)
		{
			Points[id] = (vector, payload);
			return Task.CompletedTask;
		}
		public Task<List<VectorSearchResult>> QueryAsync(float[] vector, int limit = 10) => Task.FromResult(new List<VectorSearchResult>());
		public Task<(float[] Vector, IReadOnlyDictionary<string, object> Payload)?> RetrieveVectorAsync(string id, CancellationToken ct = default)
			=> Task.FromResult(Points.TryGetValue(id, out var point) ? ((float[], IReadOnlyDictionary<string, object>)?)point : null);
		public Task DeleteAsync(string id)
		{
			Points.Remove(id);
			return Task.CompletedTask;
		}
		public Task DeleteBatchAsync(IEnumerable<string> ids, CancellationToken ct = default)
		{
			foreach (var id in ids) Points.Remove(id);
			return Task.CompletedTask;
		}
		public Task<IReadOnlyList<string>> ListPointIdsAsync(CancellationToken cancellationToken = default)
			=> Task.FromResult<IReadOnlyList<string>>(Points.Keys.ToList());
	}

	private sealed class FakeAudit : IAuditLogService
	{
		public Task<long> LogAsync(AuditLogEntry entry, CancellationToken ct = default) => Task.FromResult(1L);
		public Task<IReadOnlyList<AuditLog>> QueryAsync(AuditLogQuery query, CancellationToken ct = default)
			=> Task.FromResult<IReadOnlyList<AuditLog>>(Array.Empty<AuditLog>());
	}
}
