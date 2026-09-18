using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using SuperBuilder_AI.Application.Common.Options;
using SuperBuilder_AI.Application.Metadata;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Audit;
using SuperBuilder_AI.Models.Audit;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services;
using Xunit;

namespace SuperBuilder_AI.RealBackend.Tests;

public sealed class MetadataLifecycleRealBackendTests
{
    [Fact]
    [Trait("Category", "RealBackend")]
    public async Task IncompleteVector_BlocksActivation_ThenOldPointsGcAfterSuccessfulActivation()
    {
        const string database = "sb_v10_vector_gate";
        await RealBackendConnections.CreateSqlServerDatabaseAsync(database);
        await using var context = Context(database);
        await context.Database.EnsureCreatedAsync();
        var qdrant = Qdrant("sb_v10_vector_gate");
        var oldId = Guid.NewGuid().ToString();
        var newTableId = Guid.NewGuid().ToString();
        var newColumnId = Guid.NewGuid().ToString();
        var source = Source();
        source.VectorsBackfilled = true;
        await SeedSourceAsync(context, source);
        await qdrant.UpsertAsync(oldId, Vector(), new Dictionary<string, object>
        {
            ["tenant_id"] = source.TenantId, ["data_source_id"] = source.Id, ["metadata_version"] = 0, ["metadata_type"] = "table"
        });
        context.MetadataTables.Add(new MetadataTable
        {
            TenantId = source.TenantId, DataSourceId = source.Id, CatalogName = database, SchemaName = "dbo",
            TableName = "orders", MetadataVersion = 0, VectorId = oldId, VectorStatus = "Synced"
        });
        var staged = new MetadataTable
        {
            TenantId = source.TenantId, DataSourceId = source.Id, CatalogName = database, SchemaName = "dbo",
            TableName = "orders", MetadataVersion = 1, VectorId = newTableId, VectorStatus = "Failed",
            Columns = { new MetadataColumn { ColumnName = "id", DataType = "int", MetadataVersion = 1, VectorId = newColumnId, VectorStatus = "Synced" } }
        };
        context.MetadataTables.Add(staged);
        await context.SaveChangesAsync();

        var job = new MetadataScanJob { TenantId = source.TenantId, DataSourceId = source.Id, BatchVersion = 1, SeedVersion = 0 };
        var scanner = new MetadataScannerService(context, null!, null!, null!, null!,
            new VectorBackfillGate(Options.Create(new Features { MetadataVersionFilterEnabled = true })));
        var blocked = await Assert.ThrowsAsync<MetadataActivationBlockedException>(() => scanner.ActivateAsync(job, source));
        Assert.Equal("vector_index_incomplete", blocked.Reason);
        Assert.Equal("orders", Assert.Single(blocked.IncompleteVectorTables).TableName);
        Assert.Equal(0, source.ActiveMetadataVersion);
        Assert.NotNull(await qdrant.RetrieveVectorAsync(oldId));
        Assert.Empty(context.MetadataVectorGcRequests);

        await qdrant.UpsertAsync(newTableId, Vector(), new Dictionary<string, object>
        {
            ["tenant_id"] = source.TenantId, ["data_source_id"] = source.Id, ["metadata_version"] = 1, ["metadata_type"] = "table"
        });
        await qdrant.UpsertAsync(newColumnId, Vector(), new Dictionary<string, object>
        {
            ["tenant_id"] = source.TenantId, ["data_source_id"] = source.Id, ["metadata_version"] = 1, ["metadata_type"] = "column"
        });
        staged.VectorStatus = "Synced";
        await scanner.ActivateAsync(job, source);
        Assert.Equal(1, source.ActiveMetadataVersion);
        await new MetadataVectorGcJob(context, qdrant, new NoopAudit()).RunAsync();
        Assert.Null(await qdrant.RetrieveVectorAsync(oldId));
        Assert.NotNull(await qdrant.RetrieveVectorAsync(newTableId));
    }

    [Fact]
    [Trait("Category", "RealBackend")]
    public async Task LegacyDatabase_MigrationRepairsVersionCounter_AndBackfillsThreePointTypes()
    {
        const string database = "sb_v10_legacy";
        await RealBackendConnections.CreateSqlServerDatabaseAsync(database);
        var qdrant = Qdrant("sb_v10_legacy");
        var ids = new[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString() };
        long sourceId;

        await using (var oldContext = Context(database))
        {
            await oldContext.GetService<IMigrator>().MigrateAsync("20260917064339_M13_VectorGcAndFailureTables");
            var source = Source();
            source.NextMetadataVersion = 0;
            source.VectorsBackfilled = false;
            await SeedSourceAsync(oldContext, source);
            sourceId = source.Id;
            foreach (var id in ids)
                await qdrant.UpsertAsync(id, Vector(), new Dictionary<string, object> { ["tenant_id"] = source.TenantId });
            oldContext.MetadataTables.Add(new MetadataTable
            {
                TenantId = source.TenantId, DataSourceId = source.Id, TableName = "legacy_orders", MetadataVersion = 0,
                VectorId = ids[0], Columns = { new MetadataColumn
                {
                    ColumnName = "id", DataType = "int", MetadataVersion = 0, VectorId = ids[1],
                    Semantic = new MetadataSemantic { MetadataVersion = 0, VectorId = ids[2], BusinessMeaning = "identifier" }
                } }
            });
            await oldContext.SaveChangesAsync();
        }

        await using (var upgraded = Context(database))
        {
            await upgraded.Database.MigrateAsync();
            var source = await upgraded.DataSources.SingleAsync();
            Assert.Equal(1, source.NextMetadataVersion);
            await new MetadataVectorBackfillJob(upgraded, qdrant).RunAsync();
            Assert.True(source.VectorsBackfilled);
        }
        foreach (var id in ids)
        {
            var point = await qdrant.RetrieveVectorAsync(id);
            Assert.NotNull(point);
            Assert.Equal(sourceId.ToString(), point.Value.Payload["data_source_id"].ToString());
            Assert.Equal("0", point.Value.Payload["metadata_version"].ToString());
        }
    }

    [Fact]
    [Trait("Category", "RealBackend")]
    public async Task DeletedSource_StoredGcRequest_SurvivesContextRestart_AndDeletesUntaggedPoint()
    {
        const string database = "sb_v10_delete_gc";
        await RealBackendConnections.CreateSqlServerDatabaseAsync(database);
        var qdrant = Qdrant("sb_v10_delete_gc");
        var pointId = Guid.NewGuid().ToString();
        await using (var beforeRestart = Context(database))
        {
            await beforeRestart.Database.EnsureCreatedAsync();
            var source = Source();
            await SeedSourceAsync(beforeRestart, source);
            await qdrant.UpsertAsync(pointId, Vector(), new Dictionary<string, object> { ["tenant_id"] = source.TenantId });
            beforeRestart.MetadataTables.Add(new MetadataTable
            {
                TenantId = source.TenantId, DataSourceId = source.Id, TableName = "legacy_orders", VectorId = pointId
            });
            await beforeRestart.SaveChangesAsync();
            await using var transaction = await beforeRestart.Database.BeginTransactionAsync();
            beforeRestart.DataSources.Remove(await beforeRestart.DataSources.SingleAsync());
            beforeRestart.MetadataVectorGcRequests.Add(new MetadataVectorGcRequest
            {
                DataSourceId = source.Id, TenantId = source.TenantId, Reason = "DataSourceDeleted", Status = "Pending",
                PayloadJson = JsonSerializer.Serialize(new[] { pointId })
            });
            await beforeRestart.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        Assert.NotNull(await qdrant.RetrieveVectorAsync(pointId));
        await using (var afterRestart = Context(database))
        {
            await new MetadataVectorGcJob(afterRestart, qdrant, new NoopAudit()).RunAsync();
            Assert.Equal("Done", (await afterRestart.MetadataVectorGcRequests.SingleAsync()).Status);
            Assert.Empty(afterRestart.DataSources);
        }
        Assert.Null(await qdrant.RetrieveVectorAsync(pointId));
    }

    private static SuperBIContext Context(string database) => new(new DbContextOptionsBuilder<SuperBIContext>()
        .UseSqlServer(RealBackendConnections.SqlServer(database)).Options);

    private static async Task SeedSourceAsync(SuperBIContext context, DataSource source)
    {
        var tenant = new Tenant { TenantCode = "v10", TenantName = "V10" };
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();
        source.TenantId = tenant.Id;
        context.DataSources.Add(source);
        await context.SaveChangesAsync();
    }

    private static DataSource Source() => new()
    {
        Name = "v10-source", NormalizedName = "v10-source",
        DbType = "SQLSERVER", ConnectionString = "ci-only"
    };

    private static QdrantService Qdrant(string collection) => new(Options.Create(new QdrantOptions
    {
        Host = Environment.GetEnvironmentVariable("SB_REAL_QDRANT_HOST") ?? "127.0.0.1",
        Port = int.Parse(Environment.GetEnvironmentVariable("SB_REAL_QDRANT_PORT") ?? "6334"),
        CollectionName = collection, VectorSize = 4
    }));

    private static float[] Vector() => new[] { 1f, 0f, 0f, 0f };

    private sealed class NoopAudit : IAuditLogService
    {
        public Task<long> LogAsync(AuditLogEntry entry, CancellationToken ct = default) => Task.FromResult(1L);
        public Task<IReadOnlyList<AuditLog>> QueryAsync(AuditLogQuery query, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AuditLog>>(Array.Empty<AuditLog>());
    }
}
