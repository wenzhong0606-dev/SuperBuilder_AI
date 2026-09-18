namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// 查询表计划。
/// SemanticText 保存 Golden 业务语义，TableName 保存物理表名。
/// </summary>
public class QueryTable
{
    public long MetadataTableId { get; set; }
    public long DataSourceId { get; set; }
    public string? SemanticText { get; set; }
    public string? TableName { get; set; }
    public string? TableComment { get; set; }

    /// <summary>
    /// 物理目录名（数据库名）。SQL Server/PostgreSQL 可为 null（同连接库时由执行层校验），
    /// MySQL 等于数据库名。可空以兼容历史单库源。
    /// </summary>
    public string? CatalogName { get; set; }

    /// <summary>
    /// 物理模式名（schema）。可空以兼容历史单库源。
    /// </summary>
    public string? SchemaName { get; set; }
}