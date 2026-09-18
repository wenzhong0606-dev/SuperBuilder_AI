using System.Data.Common;

namespace SuperBuilder_AI.Infrastructure.Database;

/// <summary>
/// 从数据源连接串解析「当前执行连接数据库名」，供 §10.5 #5 PostgreSQL 跨 catalog 校验使用。
///
/// <para>解析失败时返回 <c>null</c>（调用方应据此跳过断言，而非拒绝），避免连接串格式异常阻断正常查询。</para>
/// </summary>
public static class ConnectionStringDatabaseExtractor
{
    public static string? GetDatabase(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return null;

        try
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            if (builder.TryGetValue("Database", out var db) && !string.IsNullOrWhiteSpace(db?.ToString()))
                return db!.ToString();
            if (builder.TryGetValue("Initial Catalog", out var ic) && !string.IsNullOrWhiteSpace(ic?.ToString()))
                return ic!.ToString();
            return null;
        }
        catch
        {
            return null;
        }
    }
}
