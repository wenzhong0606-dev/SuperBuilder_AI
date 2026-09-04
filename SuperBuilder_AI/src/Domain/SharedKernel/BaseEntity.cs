using System;

namespace SuperBuilder_AI.Models;

/// <summary>
/// 基础实体：统一主键与审计字段（M1-01）。
/// </summary>
public abstract class BaseEntity : IAuditable
{
    /// <summary>主键。</summary>
    public long Id { get; set; }

    /// <summary>创建时间（UTC）。</summary>
    public DateTime CreatedTime { get; set; } = DateTime.UtcNow;

    /// <summary>最后更新时间（UTC）；从未更新时为 null。</summary>
    public DateTime? UpdatedTime { get; set; }

    /// <summary>创建者标识（如平台引导、租户管理员用户 Id）。</summary>
    public string? CreatedBy { get; set; }

    /// <summary>最后更新者标识。</summary>
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// 应用层托管的乐观并发版本（ETag）。每次更新自增 1；冲突时由 DbContext 抛出
    /// <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>，API 层映射为 HTTP 409。
    /// 采用 long 而非数据库 rowversion，以兼容 SQL Server 与 SQLite（测试）。
    /// </summary>
    public long RowVersion { get; set; }
}
