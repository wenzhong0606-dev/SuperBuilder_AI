using System.Collections.Generic;
using System.Linq;
using SuperBuilder_AI.Models.DTO;

namespace SuperBuilder_AI.Models.Metadata;

/// <summary>
/// 扫描范围选择（C2 / §10.1 / §L.6）。
/// 空集合表示「全部」；跨库范围由 <see cref="Databases"/> 指定，扫描器枚举后逐库连接汇总（§10.1）。
/// MySQL 下 Schemas 与 Databases 同义（均指向 database 名），二者若同时指定且不一致，端点会拒绝请求。
/// </summary>
public class ScanScope
{
    /// <summary>仅扫描这些目录（数据库）。空=全部。跨库扫描的唯一策略（§10.1）。</summary>
    public List<string>? Databases { get; set; }

    /// <summary>仅扫描这些模式。空=全部。SQL Server / PostgreSQL 可含多 schema；MySQL 下与 Databases 同义。</summary>
    public List<string>? Schemas { get; set; }

    /// <summary>排除系统库（information_schema / mysql / performance_schema / sys / SQL Server 系统库 / 临时表）。默认 true。</summary>
    public bool ExcludeSystemDbs { get; set; } = true;

    /// <summary>排除空表（无可用列的表，见 §10.1 备注；非行数统计）。默认 false。</summary>
    public bool ExcludeEmptyTables { get; set; }

    /// <summary>排除表名前缀（大小写不敏感）。</summary>
    public List<string>? ExcludePrefixes { get; set; }

    /// <summary>是否扫描视图（ObjectKind=View）。默认 false（不扫视图，仅扫基表）。</summary>
    public bool ScanViews { get; set; }

    private static readonly HashSet<string> SystemDatabases = new(StringComparer.OrdinalIgnoreCase)
    {
        "information_schema", "mysql", "performance_schema", "sys",
        "master", "tempdb", "model", "msdb", "resource"
    };

    /// <summary>判断某数据库是否入选扫描（供跨库枚举 <see cref="IDataSourceMetadataReader.GetDatabasesAsync"/> 后过滤）。</summary>
    public bool IsDatabaseIncluded(string? database)
    {
        if (string.IsNullOrWhiteSpace(database)) return false;
        if (Databases is { Count: > 0 } && !Databases.Contains(database, StringComparer.OrdinalIgnoreCase))
            return false;
        if (ExcludeSystemDbs && SystemDatabases.Contains(database!))
            return false;
        return true;
    }

    /// <summary>判断单张表是否落在本次扫描范围内（供扫描器在发现结果上过滤）。</summary>
    public bool IsTableInScope(TableMetadataDto table)
    {
        var catalog = table.CatalogName;
        var schema = table.SchemaName;
        var name = table.TableName ?? string.Empty;

        // 临时表（SQL Server # 前缀）始终排除。
        if (name.StartsWith("#", System.StringComparison.Ordinal)) return false;

        // 系统库排除（按 catalog 或 schema 归一化后的库名）。
        if (ExcludeSystemDbs)
        {
            var dbKey = catalog ?? schema;
            if (!string.IsNullOrWhiteSpace(dbKey) && SystemDatabases.Contains(dbKey!))
                return false;
        }

        // 目录（数据库）包含过滤。
        if (Databases is { Count: > 0 })
        {
            var inDb = (!string.IsNullOrWhiteSpace(catalog) && Databases.Contains(catalog, StringComparer.OrdinalIgnoreCase))
                       || (!string.IsNullOrWhiteSpace(schema) && Databases.Contains(schema, StringComparer.OrdinalIgnoreCase));
            if (!inDb) return false;
        }

        // 模式包含过滤。
        if (Schemas is { Count: > 0 })
        {
            if (string.IsNullOrWhiteSpace(schema) || !Schemas.Contains(schema, StringComparer.OrdinalIgnoreCase))
                return false;
        }

        // 视图开关。
        if (!ScanViews && table.ObjectKind == MetadataObjectKind.View)
            return false;

        // 前缀排除。
        if (ExcludePrefixes is { Count: > 0 })
        {
            foreach (var prefix in ExcludePrefixes)
            {
                if (!string.IsNullOrWhiteSpace(prefix) && name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                    return false;
            }
        }

        return true;
    }
}
