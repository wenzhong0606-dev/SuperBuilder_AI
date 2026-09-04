using System;

namespace SuperBuilder_AI.Models.Identity;

/// <summary>角色（TenantId=0 表示平台全局角色，对所有租户可见可指派）。</summary>
public class Role : IAuditable
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedTime { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// 应用层托管的乐观并发版本（ETag）。每次更新自增 1；冲突时由 DbContext 抛出
    /// <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>，API 层映射为 HTTP 409。
    /// </summary>
    public long RowVersion { get; set; }
}
