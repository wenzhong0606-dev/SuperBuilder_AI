using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;
using SuperBuilder_AI.Infrastructure.Database;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Services;
using SuperBuilder_AI.Services.BI;
using Xunit;

namespace SuperBuilder_AI.RealBackend.Tests;

public sealed class QueryScopeRealBackendTests
{
    [Fact]
    [Trait("Category", "RealBackend")]
    public async Task SameNameAcrossSchemas_SqlServerAndPostgreSql_QueryTheCorrectRows()
    {
        const string database = "sb_v10_schemas";
        await RealBackendConnections.CreateSqlServerDatabaseAsync(database);
        await using (var sql = new SqlConnection(RealBackendConnections.SqlServer(database)))
        {
            await RealBackendConnections.ExecuteAsync(sql, """
                IF SCHEMA_ID('schema1') IS NULL EXEC('CREATE SCHEMA schema1');
                IF SCHEMA_ID('schema2') IS NULL EXEC('CREATE SCHEMA schema2');
                IF OBJECT_ID('schema1.orders') IS NULL CREATE TABLE schema1.orders (id int PRIMARY KEY, label nvarchar(30));
                IF OBJECT_ID('schema2.orders') IS NULL CREATE TABLE schema2.orders (id int PRIMARY KEY, label nvarchar(30));
                DELETE FROM schema1.orders; DELETE FROM schema2.orders;
                INSERT INTO schema1.orders VALUES (1, 'left-sql');
                INSERT INTO schema2.orders VALUES (1, 'right-sql');
                """);
            var reader = new MySqlMetadataReader();
            var tables = await reader.GetTablesAsync(RealBackendConnections.SqlServer(database), "SQLSERVER");
            Assert.Contains(tables, t => t.TableName == "orders" && t.SchemaName == "schema1");
            Assert.Contains(tables, t => t.TableName == "orders" && t.SchemaName == "schema2");
            var query = await BuildJoinAsync(new SqlServerDialect(), database, "schema1", database, "schema2");
            Assert.Equal(("left-sql", "right-sql"), await RealBackendConnections.ReadPairAsync(sql, query));
        }

        await RealBackendConnections.CreatePostgreSqlDatabaseAsync(database);
        await using (var pg = new NpgsqlConnection(RealBackendConnections.PostgreSql(database)))
        {
            await RealBackendConnections.ExecuteAsync(pg, """
                CREATE SCHEMA IF NOT EXISTS schema1;
                CREATE SCHEMA IF NOT EXISTS schema2;
                CREATE TABLE IF NOT EXISTS schema1.orders (id integer PRIMARY KEY, label text);
                CREATE TABLE IF NOT EXISTS schema2.orders (id integer PRIMARY KEY, label text);
                DELETE FROM schema1.orders; DELETE FROM schema2.orders;
                INSERT INTO schema1.orders VALUES (1, 'left-pg');
                INSERT INTO schema2.orders VALUES (1, 'right-pg');
                """);
            var reader = new MySqlMetadataReader();
            var tables = await reader.GetTablesAsync(RealBackendConnections.PostgreSql(database), "POSTGRESQL");
            Assert.Contains(tables, t => t.TableName == "orders" && t.SchemaName == "schema1");
            Assert.Contains(tables, t => t.TableName == "orders" && t.SchemaName == "schema2");
            var columns = await reader.GetColumnsAsync(RealBackendConnections.PostgreSql(database), "POSTGRESQL");
            Assert.Contains(columns, c => c.TableName == "orders" && c.ColumnName == "label" && c.CatalogName == database && c.SchemaName == "schema1");
            Assert.Contains(columns, c => c.TableName == "orders" && c.ColumnName == "label" && c.CatalogName == database && c.SchemaName == "schema2");
            var query = await BuildJoinAsync(new PostgreSqlDialect(), database, "schema1", database, "schema2");
            Assert.Equal(("left-pg", "right-pg"), await RealBackendConnections.ReadPairAsync(pg, query));
        }
    }

    [Fact]
    [Trait("Category", "RealBackend")]
    public async Task CrossDatabase_MySqlAndSqlServer_JoinCorrectRows_PostgreSqlRejects()
    {
        const string leftDb = "sb_v10_left";
        const string rightDb = "sb_v10_right";
        await RealBackendConnections.CreateMySqlDatabaseAsync(leftDb);
        await RealBackendConnections.CreateMySqlDatabaseAsync(rightDb);
        await using (var mysql = new MySqlConnection(RealBackendConnections.MySql(leftDb)))
        {
            await RealBackendConnections.ExecuteAsync(mysql, """
                CREATE TABLE IF NOT EXISTS sb_v10_left.orders (id int PRIMARY KEY, label varchar(30));
                CREATE TABLE IF NOT EXISTS sb_v10_right.orders (id int PRIMARY KEY, label varchar(30));
                DELETE FROM sb_v10_left.orders; DELETE FROM sb_v10_right.orders;
                INSERT INTO sb_v10_left.orders VALUES (1, 'left-my');
                INSERT INTO sb_v10_right.orders VALUES (1, 'right-my');
                """);
            var reader = new MySqlMetadataReader();
            var databases = await reader.GetDatabasesAsync(RealBackendConnections.MySql(leftDb), "MYSQL");
            Assert.Contains(leftDb, databases);
            Assert.Contains(rightDb, databases);
            Assert.Contains(await reader.GetTablesAsync(RealBackendConnections.MySql(leftDb), "MYSQL"), t => t.TableName == "orders" && t.CatalogName == leftDb);
            Assert.Contains(await reader.GetTablesAsync(RealBackendConnections.MySql(rightDb), "MYSQL"), t => t.TableName == "orders" && t.CatalogName == rightDb);
            var query = await BuildJoinAsync(new MySqlDialect(), leftDb, null, rightDb, null);
            Assert.Equal(("left-my", "right-my"), await RealBackendConnections.ReadPairAsync(mysql, query));
        }

        await RealBackendConnections.CreateSqlServerDatabaseAsync(leftDb);
        await RealBackendConnections.CreateSqlServerDatabaseAsync(rightDb);
        await using (var sql = new SqlConnection(RealBackendConnections.SqlServer(leftDb)))
        {
            await RealBackendConnections.ExecuteAsync(sql, """
                IF OBJECT_ID('dbo.orders') IS NULL CREATE TABLE dbo.orders (id int PRIMARY KEY, label nvarchar(30));
                IF OBJECT_ID('sb_v10_right.dbo.orders') IS NULL CREATE TABLE sb_v10_right.dbo.orders (id int PRIMARY KEY, label nvarchar(30));
                DELETE FROM dbo.orders; DELETE FROM sb_v10_right.dbo.orders;
                INSERT INTO dbo.orders VALUES (1, 'left-sql');
                INSERT INTO sb_v10_right.dbo.orders VALUES (1, 'right-sql');
                """);
            var reader = new MySqlMetadataReader();
            var databases = await reader.GetDatabasesAsync(RealBackendConnections.SqlServer(leftDb), "SQLSERVER");
            Assert.Contains(leftDb, databases);
            Assert.Contains(rightDb, databases);
            Assert.Contains(await reader.GetTablesAsync(RealBackendConnections.SqlServer(rightDb), "SQLSERVER"), t => t.TableName == "orders" && t.CatalogName == rightDb);
            var query = await BuildJoinAsync(new SqlServerDialect(), leftDb, "dbo", rightDb, "dbo");
            Assert.Equal(("left-sql", "right-sql"), await RealBackendConnections.ReadPairAsync(sql, query));
        }

        var pgDialect = new PostgreSqlDialect();
        Assert.Throws<InvalidOperationException>(() => pgDialect.AssertCatalogResolvable(rightDb, leftDb));
    }

    private static async Task<string> BuildJoinAsync(ISqlDialect dialect,
        string leftCatalog, string? leftSchema, string rightCatalog, string? rightSchema)
    {
        var plan = new QueryPlan
        {
            Tables =
            {
                new QueryTable { MetadataTableId = 1, DataSourceId = 1, CatalogName = leftCatalog, SchemaName = leftSchema, TableName = "orders" },
                new QueryTable { MetadataTableId = 2, DataSourceId = 1, CatalogName = rightCatalog, SchemaName = rightSchema, TableName = "orders" }
            },
            Fields =
            {
                new QueryField { MetadataTableId = 1, TableName = "orders", MetadataColumnId = 11, ColumnName = "label", Aggregation = "NONE" },
                new QueryField { MetadataTableId = 2, TableName = "orders", MetadataColumnId = 21, ColumnName = "label", Aggregation = "NONE" }
            },
            Joins =
            {
                new QueryJoin { LeftTableId = 1, LeftTableName = "orders", LeftColumnId = 10, LeftColumnName = "id", RightTableId = 2, RightTableName = "orders", RightColumnId = 20, RightColumnName = "id" }
            },
            Limit = 10
        };
        return (await new SqlQueryBuilder().BuildAsync(plan, dialect)).Sql;
    }
}
