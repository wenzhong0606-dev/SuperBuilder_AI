using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SuperBuilder_AI.Application.Common.Options;
using SuperBuilder_AI.Application.Metadata;
using SuperBuilder_AI.Controllers;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Audit;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Models.Audit;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Services;
using Xunit;

namespace SuperBuilder_AI.RealBackend.Tests;

public sealed class CleanupEndpointRealBackendTests
{
    [Fact]
    [Trait("Category", "RealBackend")]
    public async Task CleanupEndpoint_PersistsAllUntaggedPointIds_AndGcFinishesAfterContextRestart()
    {
        const string database = "sb_v10_cleanup_endpoint";
        await RealBackendConnections.CreateSqlServerDatabaseAsync(database);
        var qdrant = new QdrantService(Options.Create(new QdrantOptions
        {
            Host = Environment.GetEnvironmentVariable("SB_REAL_QDRANT_HOST") ?? "127.0.0.1",
            Port = int.Parse(Environment.GetEnvironmentVariable("SB_REAL_QDRANT_PORT") ?? "6334"),
            CollectionName = database,
            VectorSize = 4
        }));
        var ids = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid().ToString()).ToArray();
        var audit = new RecordingAudit();
        long sourceId;

        await using (var beforeRestart = Context(database))
        {
            await beforeRestart.Database.EnsureCreatedAsync();
            var tenant = new Tenant { TenantCode = "cleanup", TenantName = "Cleanup" };
            beforeRestart.Tenants.Add(tenant);
            await beforeRestart.SaveChangesAsync();
            var source = new DataSource
            {
                TenantId = tenant.Id, Name = "cleanup-source", NormalizedName = "cleanup-source",
                DbType = "SQLSERVER", ConnectionString = "ci-only", VectorsBackfilled = true
            };
            beforeRestart.DataSources.Add(source);
            await beforeRestart.SaveChangesAsync();
            sourceId = source.Id;
            beforeRestart.MetadataTables.Add(new MetadataTable
            {
                TenantId = tenant.Id, DataSourceId = source.Id, TableName = "legacy_orders",
                VectorId = ids[0], Columns =
                {
                    new MetadataColumn
                    {
                        ColumnName = "id", DataType = "int", VectorId = ids[1],
                        Semantic = new MetadataSemantic { BusinessMeaning = "identifier", VectorId = ids[2] }
                    }
                }
            });
            await beforeRestart.SaveChangesAsync();
            foreach (var id in ids)
                await qdrant.UpsertAsync(id, new[] { 1f, 0f, 0f, 0f }, new Dictionary<string, object>
                {
                    ["tenant_id"] = tenant.Id
                });

            var controller = new DataSourcesController(beforeRestart, new PermissiveIdentity(), null!, null!, audit)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                        {
                            new Claim("tid", tenant.Id.ToString()),
                            new Claim(ClaimTypes.NameIdentifier, "42")
                        }))
                    }
                }
            };
            Assert.IsType<OkObjectResult>(await controller.Delete(source.Id, "cleanup", true));
            var request = await beforeRestart.MetadataVectorGcRequests.SingleAsync();
            Assert.Equal("Pending", request.Status);
            Assert.Equal(sourceId, request.DataSourceId);
            var snapshot = JsonSerializer.Deserialize<string[]>(request.PayloadJson!);
            Assert.NotNull(snapshot);
            Assert.Equal(ids.Order(), snapshot.Order());
            Assert.Empty(beforeRestart.DataSources);
            Assert.Contains(audit.Entries, e => e.Action == "metadata:datasource:cleanup" && e.Result == "success");
        }

        foreach (var id in ids)
            Assert.NotNull(await qdrant.RetrieveVectorAsync(id));

        await using (var afterRestart = Context(database))
        {
            await new MetadataVectorGcJob(afterRestart, qdrant, audit).RunAsync();
            Assert.Equal("Done", (await afterRestart.MetadataVectorGcRequests.SingleAsync()).Status);
        }
        foreach (var id in ids)
            Assert.Null(await qdrant.RetrieveVectorAsync(id));
        Assert.Contains(audit.Entries, e => e.Action == "metadata:vector:gc" && e.Result == "success");
    }

    private static SuperBIContext Context(string database) => new(new DbContextOptionsBuilder<SuperBIContext>()
        .UseSqlServer(RealBackendConnections.SqlServer(database)).Options);

    private sealed class PermissiveIdentity : IIdentityService
    {
        public Task SeedAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<IdentityResult> CreateUserAsync(long tenantId, string username, string? displayName, string? email, string[]? roleCodes, CancellationToken ct = default) => Task.FromResult(IdentityResult.Ok(0));
        public Task<IdentityResult> AssignRoleAsync(long tenantId, long userId, string roleCode, CancellationToken ct = default) => Task.FromResult(IdentityResult.Ok(0));
        public Task<IdentityResult> RevokeRoleAsync(long tenantId, long userId, string roleCode, CancellationToken ct = default) => Task.FromResult(IdentityResult.Ok(0));
        public Task<IdentityResult> SetPasswordAsync(long tenantId, long userId, string password, CancellationToken ct = default) => Task.FromResult(IdentityResult.Ok(0));
        public Task<IdentityResult> SetUserStatusAsync(long tenantId, long userId, UserStatus status, CancellationToken ct = default) => Task.FromResult(IdentityResult.Ok(0));
        public Task<IReadOnlyList<string>> GetPermissionsAsync(long tenantId, long userId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<bool> HasPermissionAsync(long tenantId, long userId, string permissionCode, CancellationToken ct = default) => Task.FromResult(true);
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
