using SuperBuilder_AI.Models.Localization;

namespace SuperBuilder_AI.Interfaces.Localization;

/// <summary>租户界面语言关系服务（M3-01 语言关系模型）。</summary>
public interface ITenantLanguageService
{
    /// <summary>返回租户已配置的语言列表（含启用/默认/排序）。</summary>
    Task<List<TenantLanguageInfo>> GetLanguagesAsync(long tenantId, CancellationToken ct = default);

    /// <summary>返回租户启用的文化代码列表（用于校验用户偏好与回退）。</summary>
    Task<List<string>> GetAvailableCulturesAsync(long tenantId, CancellationToken ct = default);

    /// <summary>返回租户默认文化；无默认时回退首个启用语言，再回退 zh-CN。</summary>
    Task<string> GetDefaultCultureAsync(long tenantId, CancellationToken ct = default);

    /// <summary>批量返回多租户的语言关系（用于租户列表/目录场景），键为 TenantId。</summary>
    Task<Dictionary<long, List<TenantLanguageInfo>>> GetLanguagesForTenantsAsync(IReadOnlyList<long> tenantIds, CancellationToken ct = default);

    /// <summary>
    /// 设置租户的语言授权集合（全量替换）。强制约束：至少一种启用语言、恰一个默认语言、默认语言必须启用。
    /// </summary>
    Task SetLanguagesAsync(long tenantId, IReadOnlyList<TenantLanguageUpdate> updates, long actor, CancellationToken ct = default);

    /// <summary>若租户尚无任何语言配置则播种（优先按 localization JSON，否则按平台默认 zh-CN）。幂等。</summary>
    Task EnsureTenantLanguagesAsync(long tenantId, CancellationToken ct = default);

    /// <summary>为所有尚无语言配置的租户播种默认语言。幂等。</summary>
    Task EnsureAllTenantsLanguagesAsync(CancellationToken ct = default);

    /// <summary>
    /// 停用平台语言：先把所有以该语言为默认的租户迁移到其首个其他启用语言，再禁用平台语言目录项。
    /// 满足"停用平台语言前展示受影响租户并迁移其默认语言"。
    /// </summary>
    Task<DisablePlatformLanguageResult> DisablePlatformLanguageAsync(string culture, long actor, CancellationToken ct = default);
}

/// <summary>租户语言配置视图。</summary>
public sealed record TenantLanguageInfo(
    long UiLanguageId,
    string Culture,
    string DisplayName,
    string NativeName,
    bool Enabled,
    bool IsDefault,
    int SortOrder);

/// <summary>租户语言授权更新项。</summary>
public sealed record TenantLanguageUpdate(
    long UiLanguageId,
    bool Enabled,
    bool IsDefault,
    int SortOrder);

/// <summary>停用平台语言的结果。</summary>
public sealed record DisablePlatformLanguageResult(
    string Culture,
    IReadOnlyList<long> AffectedTenantIds,
    IReadOnlyList<long> MigratedTenantIds);
