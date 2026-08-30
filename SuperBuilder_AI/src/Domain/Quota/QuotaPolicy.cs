using System;

namespace SuperBuilder_AI.Models.Quota;

/// <summary>
/// 配额策略：定义某资源在某一租户（或平台默认 TenantId=0）下的上限与窗口。
/// 租户可覆盖平台默认（同 ResourceType 下存在 TenantId=实际租户 行即优先采用）。
/// </summary>
public class QuotaPolicy
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public QuotaResourceType ResourceType { get; set; }
    public long Limit { get; set; }
    public QuotaWindow Window { get; set; }
    public DateTime CreatedTime { get; set; }
}
