using System.Data.Common;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;

namespace SuperBuilder_AI.RealBackend.Tests;

internal static class RealBackendConnections
{
    private static string Required(string name) => Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"RealBackend requires {name}; do not run against a business database.");

    public static string SqlServer(string database) => new SqlConnectionStringBuilder(Required("SB_REAL_SQLSERVER"))
    {
        InitialCatalog = database
    }.ConnectionString;

    public static string MySql(string database) => new MySqlConnectionStringBuilder(Required("SB_REAL_MYSQL"))
    {
        Database = database
    }.ConnectionString;

    public static string PostgreSql(string database) => new NpgsqlConnectionStringBuilder(Required("SB_REAL_POSTGRES"))
    {
        Database = database
    }.ConnectionString;

    public static async Task CreateSqlServerDatabaseAsync(string name)
    {
        await using var connection = new SqlConnection(SqlServer("master"));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID('{name}') IS NULL CREATE DATABASE [{name}]";
        await command.ExecuteNonQueryAsync();
    }

    public static async Task CreateMySqlDatabaseAsync(string name)
    {
        await using var connection = new MySqlConnection(MySql("mysql"));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE IF NOT EXISTS `{name}`";
        await command.ExecuteNonQueryAsync();
    }

    public static async Task CreatePostgreSqlDatabaseAsync(string name)
    {
        await using var connection = new NpgsqlConnection(PostgreSql("postgres"));
        await connection.OpenAsync();
        await using var exists = connection.CreateCommand();
        exists.CommandText = "SELECT 1 FROM pg_database WHERE datname = @name";
        exists.Parameters.AddWithValue("name", name);
        if (await exists.ExecuteScalarAsync() is not null) return;
        await using var create = connection.CreateCommand();
        create.CommandText = $"CREATE DATABASE \"{name}\"";
        await create.ExecuteNonQueryAsync();
    }

    public static async Task ExecuteAsync(DbConnection connection, string sql)
    {
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    public static async Task<(string, string)> ReadPairAsync(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("Generated SQL returned no rows.");
        return (reader.GetString(0), reader.GetString(1));
    }
}
