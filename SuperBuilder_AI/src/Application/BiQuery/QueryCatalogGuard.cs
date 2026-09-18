using SuperBuilder_AI.Infrastructure.Database;
using SuperBuilder_AI.Interfaces.Database;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Application.BiQuery;

/// <summary>
/// §10.5 #5 PostgreSQL 跨 catalog 执行层校验。
///
/// <para>执行 SQL 前逐表核对非空 <see cref="QueryTable.CatalogName"/> 是否等于当前连接数据库；
/// 任一不一致即拒绝（PostgreSQL 无法在同一连接内跨数据库查询）。SQL Server / MySQL 支持跨
/// catalog 限定名，本方法为空操作。</para>
///
/// <para>无法从连接串解析出当前数据库时跳过断言（避免连接串格式异常阻断正常查询）。</para>
/// </summary>
public static class QueryCatalogGuard
{
    public static void Assert(
        QueryPlan plan,
        ISqlDialect dialect,
        string? connectionString)
    {
        if (dialect.Name != "POSTGRESQL")
            return;

        var currentCatalog = ConnectionStringDatabaseExtractor.GetDatabase(connectionString);
        if (currentCatalog is null)
            return;

        foreach (var table in plan.Tables)
        {
            if (!string.IsNullOrWhiteSpace(table.CatalogName))
                dialect.AssertCatalogResolvable(table.CatalogName, currentCatalog);
        }
    }
}
