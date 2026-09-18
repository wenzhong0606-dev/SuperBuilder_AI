using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Application.Metadata;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.BI.Entity;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.Organization;
using Xunit;

namespace SuperBuilder_AI.Tests;

public sealed class MetadataVersionGcJobTests
{
    [Fact]
    public async Task RunAsync_DeletesOnlyInactiveRowsAndIsIdempotent()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await SeedAsync(context);

        var gc = new MetadataVersionGcJob(context);
        await gc.RunAsync();
        await gc.RunAsync();

        var tables = await context.MetadataTables.AsNoTracking().Include(t => t.Columns).ThenInclude(c => c.Semantic).ToListAsync();
        var active = Assert.Single(tables);
        Assert.Equal(1, active.MetadataVersion);
        Assert.NotNull(Assert.Single(active.Columns).Semantic);
    }

    [Theory]
    [InlineData("RLS")]
    [InlineData("Binding")]
    [InlineData("Learning")]
    public async Task RunAsync_KeepsOldRowsWhenExternalReferenceStillExists(string referenceKind)
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await SeedAsync(context);
        var old = await context.MetadataTables.Include(t => t.Columns).SingleAsync(t => t.MetadataVersion == 0);
        var columnId = Assert.Single(old.Columns).Id;
        if (referenceKind == "RLS")
            context.RowLevelSecurityPolicies.Add(new RowLevelSecurityPolicy
            {
                TenantId = 7, DataSourceId = 1, MetadataTableId = old.Id,
                MetadataColumnId = columnId, Value = "1"
            });
        else if (referenceKind == "Binding")
            context.PhysicalBindings.Add(new PhysicalBinding
            {
                DataSourceId = 1, MetadataTableId = old.Id, MetadataColumnId = columnId
            });
        else
            context.LearningRecords.Add(new MetadataLearningRecord
            {
                TenantId = 7, MetadataColumnId = columnId, Question = "old question"
            });
        await context.SaveChangesAsync();

        await new MetadataVersionGcJob(context).RunAsync();

        Assert.Equal(2, await context.MetadataTables.CountAsync());
        Assert.Equal(2, await context.MetadataColumns.CountAsync());
        Assert.Equal(2, await context.MetadataSemantics.CountAsync());
    }

    private static SuperBIContext CreateContext(SqliteConnection connection)
    {
        var context = new SuperBIContext(new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options);
        context.Database.EnsureCreated();
        return context;
    }

    private static async Task SeedAsync(SuperBIContext context)
    {
        context.Tenants.Add(new Tenant { Id = 7, TenantCode = "t7", TenantName = "Tenant 7" });
        context.DataSources.Add(new DataSource
        {
            Id = 1, TenantId = 7, Name = "ds", NormalizedName = "ds", DbType = "SQLSERVER",
            ConnectionString = "x", ActiveMetadataVersion = 1
        });
        foreach (var version in new[] { 0, 1 })
            context.MetadataTables.Add(new MetadataTable
            {
                TenantId = 7, DataSourceId = 1, CatalogName = "db", SchemaName = "dbo",
                TableName = "orders", MetadataVersion = version,
                Columns = { new MetadataColumn
                {
                    ColumnName = "id", DataType = "int", MetadataVersion = version,
                    Semantic = new MetadataSemantic { MetadataVersion = version, BusinessMeaning = "identifier" }
                } }
            });
        await context.SaveChangesAsync();
    }
}
