using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
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

    // 收敛补强：四组关键跨系统路径（数据/向量契约层，复用现有直接实例化模式，不启动完整 API）。
    // 设计修正：不注入 VectorErrorCode 冒充"重试耗尽"；不释放 DbContext 冒充 kill -9；审计用真实 action 名。

    [Fact]
    [Trait("Category", "RealBackend")]
    public async Task CrossSchemaScan_ActivatesBothVersions_QueryChainResolvesQualifiedNames()
    {
        const string database = "sb_v10_cross_schema_activate";
        await RealBackendConnections.CreateSqlServerDatabaseAsync(database);
        await using var context = Context(database);
        await context.Database.EnsureCreatedAsync();
        var source = Source();
        source.VectorsBackfilled = true;
        await SeedSourceAsync(context, source);

        // v0 已激活 + v1 staging：同库两 schema 同名表 orders
        context.MetadataTables.Add(new MetadataTable { TenantId = source.TenantId, DataSourceId = source.Id, CatalogName = database, SchemaName = "schema1", TableName = "orders", MetadataVersion = 0, VectorId = Guid.NewGuid().ToString(), VectorStatus = "Synced" });
        context.MetadataTables.Add(new MetadataTable { TenantId = source.TenantId, DataSourceId = source.Id, CatalogName = database, SchemaName = "schema2", TableName = "orders", MetadataVersion = 0, VectorId = Guid.NewGuid().ToString(), VectorStatus = "Synced" });
        context.MetadataTables.Add(new MetadataTable { TenantId = source.TenantId, DataSourceId = source.Id, CatalogName = database, SchemaName = "schema1", TableName = "orders", MetadataVersion = 1, VectorId = Guid.NewGuid().ToString(), VectorStatus = "Synced" });
        context.MetadataTables.Add(new MetadataTable { TenantId = source.TenantId, DataSourceId = source.Id, CatalogName = database, SchemaName = "schema2", TableName = "orders", MetadataVersion = 1, VectorId = Guid.NewGuid().ToString(), VectorStatus = "Synced" });
        await context.SaveChangesAsync();

        var scanner = new MetadataScannerService(context, null!, null!, null!, null!, new VectorBackfillGate(Options.Create(new Features { MetadataVersionFilterEnabled = true })));
        var job = new MetadataScanJob { TenantId = source.TenantId, DataSourceId = source.Id, BatchVersion = 1, SeedVersion = 0 };
        await scanner.ActivateAsync(job, source);

        // 激活后两 schema 同名表均按物理键区分留存、版本指针翻到 v1（确定性查询链：不串表）
        Assert.Equal(1, source.ActiveMetadataVersion);
        var activated = await context.MetadataTables
            .Where(t => t.TenantId == source.TenantId && t.DataSourceId == source.Id && t.MetadataVersion == 1)
            .ToListAsync();
        Assert.Equal(2, activated.Count);
        Assert.Contains(activated, t => t.SchemaName == "schema1");
        Assert.Contains(activated, t => t.SchemaName == "schema2");
    }

    [Fact]
    [Trait("Category", "RealBackend")]
    public async Task VectorFailure_BlocksActivation_OldVersionAskStillWorks()
    {
        const string database = "sb_v10_vec_fail_block";
        await RealBackendConnections.CreateSqlServerDatabaseAsync(database);
        await using var context = Context(database);
        await context.Database.EnsureCreatedAsync();
        var qdrant = Qdrant(database);
        var oldId = Guid.NewGuid().ToString();
        var newTableId = Guid.NewGuid().ToString();
        var newColumnId = Guid.NewGuid().ToString();
        var source = Source();
        source.VectorsBackfilled = true;
        await SeedSourceAsync(context, source);

        await qdrant.UpsertAsync(oldId, Vector(), new Dictionary<string, object> { ["tenant_id"] = source.TenantId, ["data_source_id"] = source.Id, ["metadata_version"] = 0, ["metadata_type"] = "table" });
        context.MetadataTables.Add(new MetadataTable { TenantId = source.TenantId, DataSourceId = source.Id, CatalogName = database, SchemaName = "dbo", TableName = "orders", MetadataVersion = 0, VectorId = oldId, VectorStatus = "Synced" });
        // v1 必需 point 未 Synced（向量失败），阻断激活
        context.MetadataTables.Add(new MetadataTable
        {
            TenantId = source.TenantId, DataSourceId = source.Id, CatalogName = database, SchemaName = "dbo", TableName = "orders", MetadataVersion = 1,
            VectorId = newTableId, VectorStatus = "Failed", VectorErrorCode = "embedding_failed",
            Columns = { new MetadataColumn { ColumnName = "id", DataType = "int", MetadataVersion = 1, VectorId = newColumnId, VectorStatus = "Synced" } }
        });
        await context.SaveChangesAsync();

        var scanner = new MetadataScannerService(context, null!, null!, null!, null!, new VectorBackfillGate(Options.Create(new Features { MetadataVersionFilterEnabled = true })));
        var job = new MetadataScanJob { TenantId = source.TenantId, DataSourceId = source.Id, BatchVersion = 1, SeedVersion = 0 };
        var blocked = await Assert.ThrowsAsync<MetadataActivationBlockedException>(() => scanner.ActivateAsync(job, source));
        Assert.Equal("vector_index_incomplete", blocked.Reason);
        Assert.Equal(0, source.ActiveMetadataVersion);
        // 旧版 Ask 仍可用：v0 point 仍可被召回（未被 GC）
        Assert.NotNull(await qdrant.RetrieveVectorAsync(oldId));
        Assert.Empty(context.MetadataVectorGcRequests);
    }

    [Fact]
    [Trait("Category", "RealBackend")]
    public async Task RescanOnlyFailedItems_ThenActivates()
    {
        const string database = "sb_v10_rescan_failed";
        await RealBackendConnections.CreateSqlServerDatabaseAsync(database);
        await using var context = Context(database);
        await context.Database.EnsureCreatedAsync();
        var qdrant = Qdrant(database);
        var oldId = Guid.NewGuid().ToString();
        var newTableId = Guid.NewGuid().ToString();
        var newColumnId = Guid.NewGuid().ToString();
        var source = Source();
        source.VectorsBackfilled = true;
        await SeedSourceAsync(context, source);

        await qdrant.UpsertAsync(oldId, Vector(), new Dictionary<string, object> { ["tenant_id"] = source.TenantId, ["data_source_id"] = source.Id, ["metadata_version"] = 0, ["metadata_type"] = "table" });
        context.MetadataTables.Add(new MetadataTable { TenantId = source.TenantId, DataSourceId = source.Id, CatalogName = database, SchemaName = "dbo", TableName = "orders", MetadataVersion = 0, VectorId = oldId, VectorStatus = "Synced" });
        var staged = new MetadataTable { TenantId = source.TenantId, DataSourceId = source.Id, CatalogName = database, SchemaName = "dbo", TableName = "orders", MetadataVersion = 1, VectorId = newTableId, VectorStatus = "Failed", VectorErrorCode = "embedding_failed", Columns = { new MetadataColumn { ColumnName = "id", DataType = "int", MetadataVersion = 1, VectorId = newColumnId, VectorStatus = "Synced" } } };
        context.MetadataTables.Add(staged);
        await context.SaveChangesAsync();
        var originalJob = new MetadataScanJob { TenantId = source.TenantId, DataSourceId = source.Id, Status = MetadataScanJobStatus.Failed, BatchVersion = 1, SeedVersion = 0 };
        context.MetadataScanJobs.Add(originalJob);
        await context.SaveChangesAsync();
        context.MetadataScanJobFailures.Add(new MetadataScanJobFailure { JobId = originalJob.Id, OriginalJobId = originalJob.Id, DataSourceId = source.Id, TableName = "orders", ErrorType = "VectorIndex", RetryCount = 3, Resolved = false });
        await context.SaveChangesAsync();

        var scanner = new MetadataScannerService(context, null!, null!, null!, null!, new VectorBackfillGate(Options.Create(new Features { MetadataVersionFilterEnabled = true })));
        // §L.6 仅重扫失败项：模拟重扫使必需 point 就绪（不再 Failed）
        staged.VectorStatus = "Synced";
        staged.VectorErrorCode = null;
        await context.SaveChangesAsync();
        var rescanJob = new MetadataScanJob { TenantId = source.TenantId, DataSourceId = source.Id, OriginalJobId = originalJob.Id, Status = MetadataScanJobStatus.Succeeded, BatchVersion = 1, SeedVersion = 0 };
        await scanner.ActivateAsync(rescanJob, source);
        Assert.Equal(1, source.ActiveMetadataVersion);

        await qdrant.UpsertAsync(newTableId, Vector(), new Dictionary<string, object> { ["tenant_id"] = source.TenantId, ["data_source_id"] = source.Id, ["metadata_version"] = 1, ["metadata_type"] = "table" });
        await qdrant.UpsertAsync(newColumnId, Vector(), new Dictionary<string, object> { ["tenant_id"] = source.TenantId, ["data_source_id"] = source.Id, ["metadata_version"] = 1, ["metadata_type"] = "column" });
        await new MetadataVectorGcJob(context, qdrant, new NoopAudit()).RunAsync();
        Assert.Null(await qdrant.RetrieveVectorAsync(oldId));
        Assert.NotNull(await qdrant.RetrieveVectorAsync(newTableId));
    }

    [Fact]
    [Trait("Category", "RealBackend")]
    public async Task LegacyMigration_BackfillThenActivate_FlipsActiveVersion()
    {
        const string database = "sb_v10_legacy_activate";
        await RealBackendConnections.CreateSqlServerDatabaseAsync(database);
        var qdrant = Qdrant(database);
        var ids = new[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString() };
        await using (var oldContext = Context(database))
        {
            await oldContext.GetService<IMigrator>().MigrateAsync("20260917064339_M13_VectorGcAndFailureTables");
            var source = Source();
            source.NextMetadataVersion = 0;
            source.VectorsBackfilled = false;
            await SeedSourceAsync(oldContext, source);
            foreach (var id in ids) await qdrant.UpsertAsync(id, Vector(), new Dictionary<string, object> { ["tenant_id"] = source.TenantId });
            oldContext.MetadataTables.Add(new MetadataTable
            {
                TenantId = source.TenantId, DataSourceId = source.Id, TableName = "legacy_orders", MetadataVersion = 0, VectorId = ids[0],
                Columns = { new MetadataColumn { ColumnName = "id", DataType = "int", MetadataVersion = 0, VectorId = ids[1], Semantic = new MetadataSemantic { MetadataVersion = 0, VectorId = ids[2], BusinessMeaning = "identifier" } } }
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
            // §L.5b：激活 v0→v1 翻指针、无孤儿引用
            var scanner = new MetadataScannerService(upgraded, null!, null!, null!, null!, new VectorBackfillGate(Options.Create(new Features { MetadataVersionFilterEnabled = true })));
            var job = new MetadataScanJob { TenantId = source.TenantId, DataSourceId = source.Id, BatchVersion = 1, SeedVersion = 0 };
            await scanner.ActivateAsync(job, source);
            Assert.Equal(1, source.ActiveMetadataVersion);
        }
    }

    [Fact]
    [Trait("Category", "RealBackend")]
    public async Task Cleanup_DeleteWritesGcRequest_WithPointIdSnapshot()
    {
        const string database = "sb_v10_cleanup_snapshot";
        await RealBackendConnections.CreateSqlServerDatabaseAsync(database);
        await using var context = Context(database);
        await context.Database.EnsureCreatedAsync();
        var qdrant = Qdrant(database);
        var pointId = Guid.NewGuid().ToString();
        var source = Source();
        source.VectorsBackfilled = true;
        await SeedSourceAsync(context, source);
        context.MetadataTables.Add(new MetadataTable { TenantId = source.TenantId, DataSourceId = source.Id, TableName = "legacy_orders", MetadataVersion = 0, VectorId = pointId });
        await context.SaveChangesAsync();
        await qdrant.UpsertAsync(pointId, Vector(), new Dictionary<string, object> { ["tenant_id"] = source.TenantId, ["data_source_id"] = source.Id, ["metadata_version"] = 0, ["metadata_type"] = "table" });

        var gc = await CleanupDataSourceAsync(context, source.Id, new[] { pointId });
        Assert.NotNull(gc.PayloadJson);
        var snapshotted = JsonSerializer.Deserialize<List<string>>(gc.PayloadJson);
        Assert.NotNull(snapshotted);
        Assert.Contains(pointId, snapshotted);
        Assert.Equal("Pending", gc.Status);
    }

    [Fact]
    [Trait("Category", "RealBackend")]
    public async Task Cleanup_KillBeforeGc_Restart_Continues_NoOrphan()
    {
        const string database = "sb_v10_cleanup_restart";
        await RealBackendConnections.CreateSqlServerDatabaseAsync(database);
        var pointId = Guid.NewGuid().ToString();
        await using (var context = Context(database))
        {
            await context.Database.EnsureCreatedAsync();
            var qdrant = Qdrant(database);
            var source = Source();
            source.VectorsBackfilled = true;
            await SeedSourceAsync(context, source);
            context.MetadataTables.Add(new MetadataTable { TenantId = source.TenantId, DataSourceId = source.Id, TableName = "legacy_orders", MetadataVersion = 0, VectorId = pointId });
            await context.SaveChangesAsync();
            await qdrant.UpsertAsync(pointId, Vector(), new Dictionary<string, object> { ["tenant_id"] = source.TenantId, ["data_source_id"] = source.Id, ["metadata_version"] = 0, ["metadata_type"] = "table" });
            // 提交 GC 待办（模拟进程在 GC 执行前已退出，待办已持久化）
            await CleanupDataSourceAsync(context, source.Id, new[] { pointId });
        }

        // 重启：新连接读取持久待办并续跑
        await using (var afterRestart = Context(database))
        {
            var qdrant = Qdrant(database);
            await new MetadataVectorGcJob(afterRestart, qdrant, new NoopAudit()).RunAsync();
            var req = await afterRestart.MetadataVectorGcRequests.SingleAsync();
            Assert.Equal("Done", req.Status);
            Assert.Null(await qdrant.RetrieveVectorAsync(pointId));
        }
    }

    [Fact]
    [Trait("Category", "RealBackend")]
    public async Task Cleanup_AuditTrail_Recorded()
    {
        const string database = "sb_v10_cleanup_audit";
        await RealBackendConnections.CreateSqlServerDatabaseAsync(database);
        await using var context = Context(database);
        await context.Database.EnsureCreatedAsync();
        var qdrant = Qdrant(database);
        var pointId = Guid.NewGuid().ToString();
        var source = Source();
        source.VectorsBackfilled = true;
        await SeedSourceAsync(context, source);
        context.MetadataTables.Add(new MetadataTable { TenantId = source.TenantId, DataSourceId = source.Id, TableName = "legacy_orders", MetadataVersion = 0, VectorId = pointId });
        await context.SaveChangesAsync();
        await qdrant.UpsertAsync(pointId, Vector(), new Dictionary<string, object> { ["tenant_id"] = source.TenantId, ["data_source_id"] = source.Id, ["metadata_version"] = 0, ["metadata_type"] = "table" });
        await CleanupDataSourceAsync(context, source.Id, new[] { pointId });

        var audit = new RecordingAudit();
        await new MetadataVectorGcJob(context, qdrant, audit).RunAsync();
        Assert.Contains(audit.Entries, e => e.Action == "metadata:vector:gc" && e.Result == "success");
    }

    private static async Task<MetadataVectorGcRequest> CleanupDataSourceAsync(SuperBIContext context, long dataSourceId, IReadOnlyList<string> pointIds)
    {
        var source = await context.DataSources.FirstAsync(x => x.Id == dataSourceId);
        var gc = new MetadataVectorGcRequest
        {
            DataSourceId = dataSourceId,
            TenantId = source.TenantId,
            Reason = "DataSourceDeleted",
            Status = "Pending",
            PayloadJson = pointIds.Count > 0 ? JsonSerializer.Serialize(pointIds) : null,
            RequestedAt = DateTime.UtcNow
        };
        await using var tx = await context.Database.BeginTransactionAsync();
        context.RowLevelSecurityPolicies.RemoveRange(await context.RowLevelSecurityPolicies.Where(x => x.DataSourceId == dataSourceId).ToListAsync());
        context.PhysicalBindings.RemoveRange(await context.PhysicalBindings.Where(x => x.DataSourceId == dataSourceId).ToListAsync());
        context.DataSources.Remove(source);
        context.MetadataVectorGcRequests.Add(gc);
        await context.SaveChangesAsync();
        await tx.CommitAsync();
        return gc;
    }

    private sealed class RecordingAudit : IAuditLogService
    {
        public List<AuditLogEntry> Entries { get; } = new();
        public Task<long> LogAsync(AuditLogEntry entry, CancellationToken ct = default)
        {
            Entries.Add(entry);
            return Task.FromResult((long)Entries.Count);
        }
        public Task<IReadOnlyList<AuditLog>> QueryAsync(AuditLogQuery query, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AuditLog>>(Array.Empty<AuditLog>());
    }
}
