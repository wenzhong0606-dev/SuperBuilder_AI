namespace SuperBuilder_AI.Models;

/// <summary>
/// 可审计实体契约：承载创建/更新时间、操作者，以及由应用层托管的乐观并发版本（RowVersion/ETag）。
/// 由 <see cref="BaseEntity"/> 与未继承 BaseEntity 的实体（如 Role）实现，
/// 供 <c>SuperBIContext.SaveChanges</c> 在写入期统一回填时间戳与自增并发版本。
/// </summary>
public interface IAuditable
{
    /// <summary>创建时间（UTC）。</summary>
    DateTime CreatedTime { get; set; }

    /// <summary>最后更新时间（UTC）；从未更新时为 null。</summary>
    DateTime? UpdatedTime { get; set; }

    /// <summary>创建者标识（如平台引导、租户管理员用户 Id）。</summary>
    string? CreatedBy { get; set; }

    /// <summary>最后更新者标识。</summary>
    string? UpdatedBy { get; set; }

    /// <summary>
    /// 应用层托管的乐观并发版本（ETag）。每次更新自增 1；
    /// 冲突时由 DbContext 抛出 <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>，
    /// API 层映射为 HTTP 409。采用 long 而非数据库 rowversion，以兼容 SQL Server 与 SQLite（测试）。
    /// </summary>
    long RowVersion { get; set; }
}
