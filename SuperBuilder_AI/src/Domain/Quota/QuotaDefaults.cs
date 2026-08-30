using System.Collections.Generic;

namespace SuperBuilder_AI.Models.Quota;

/// <summary>平台默认配额（TenantId=0）。租户未显式覆盖时回退至此。</summary>
public static class QuotaDefaults
{
    public static IReadOnlyDictionary<QuotaResourceType, (long Limit, QuotaWindow Window)> PlatformDefaults { get; } =
        new Dictionary<QuotaResourceType, (long, QuotaWindow)>
        {
            [QuotaResourceType.Users] = (50, QuotaWindow.Total),
            [QuotaResourceType.Apps] = (20, QuotaWindow.Total),
            [QuotaResourceType.Agents] = (10, QuotaWindow.Total),
            [QuotaResourceType.Dashboards] = (30, QuotaWindow.Total),
            [QuotaResourceType.DataSources] = (5, QuotaWindow.Total),
            [QuotaResourceType.ApiCallsPerMonth] = (10000, QuotaWindow.Monthly),
            [QuotaResourceType.StorageMb] = (1024, QuotaWindow.Total),
        };
}
