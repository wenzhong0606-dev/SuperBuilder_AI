using System;

namespace SuperBuilder_AI.Models.Quota;

/// <summary>
/// 配额使用量：某一租户某资源的当前累计用量。
/// PeriodKey 标识当前统计周期（Total 固定 "total"；Monthly 为 "yyyy-MM"；Daily 为 "yyyy-MM-dd"），
/// 跨周期调用时自动按周期键滚动归零（确定性）。
/// </summary>
public class QuotaUsage
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public QuotaResourceType ResourceType { get; set; }
    public long Used { get; set; }
    public string PeriodKey { get; set; } = "total";
    public DateTime LastReset { get; set; }
    public DateTime CreatedTime { get; set; }
}
