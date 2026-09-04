using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Api.Diagnostics;
using SuperBuilder_AI.Data;
using Xunit;

namespace SuperBuilder_AI.Tests;

public sealed class SchemaProbeTests
{
    [Fact]
    public async Task ProbeAsync_EnsureCreatedWithoutMigrations_ReturnsSchemaNotCreated()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SuperBIContext>().UseSqlite(connection).Options;
        await using var db = new SuperBIContext(options);
        // EnsureCreated 直接建表，不写入 __EFMigrationsHistory，等价于「Schema 未迁移」环境
        await db.Database.EnsureCreatedAsync();

        var (state, reason) = await SchemaProbe.ProbeAsync(db, migrateOnStartup: false);

        Assert.Equal(BootstrapState.SchemaNotCreated, state);
        Assert.False(string.IsNullOrEmpty(reason));
    }
}
