namespace SuperBuilder_AI.Models.Quota;

/// <summary>受配额约束的资源类型。</summary>
public enum QuotaResourceType
{
    Users = 1,
    Apps = 2,
    Agents = 3,
    Dashboards = 4,
    DataSources = 5,
    ApiCallsPerMonth = 6,
    StorageMb = 7,
}

/// <summary>配额窗口（用量重置周期）。</summary>
public enum QuotaWindow
{
    Total = 0,
    Monthly = 1,
    Daily = 2,
}
