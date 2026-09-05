using System.Threading;
using System.Threading.Tasks;

namespace SuperBuilder_AI.Interfaces.Localization;

/// <summary>
/// 平台语言目录（<see cref="UiLanguage"/>）维护服务端口（M3-02 平台语言维护）。
/// <para>平台管理员据此查看、添加、启停、排序语言；Culture 按 BCP 47 归一化且唯一；
/// 新建语言可复制已有键集合（标记“待翻译”）。所有操作仅作用于平台目录与 TenantId=0 基线，
/// 不涉及任何租户覆盖（租户覆盖属于 M3-03）。</para>
/// </summary>
public interface IPlatformLanguageService
{
    /// <summary>列举平台语言目录；<paramref name="includeDisabled"/> 为 true 时同时包含已停用项（平台管理视图）。</summary>
    Task<PlatformLanguageListResult> ListLanguagesAsync(bool includeDisabled, CancellationToken ct = default);

    /// <summary>按 Id 获取单个语言摘要；不存在返回 null。</summary>
    Task<UiLanguageSummary?> GetLanguageAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// 新建平台语言。Culture 经 BCP 47 归一化且唯一；DisplayName/NativeName 必填；
    /// 成功后将可选 <c>CopyFromCulture</c> 的平台基线键集合复制为新语言基线并标记“待翻译”。
    /// </summary>
    Task<UiLanguageSummary> CreateLanguageAsync(CreateUiLanguageRequest request, long actor, CancellationToken ct = default);

    /// <summary>更新语言显示名/本地名/排序（仅提供非空字段）。</summary>
    Task<UiLanguageSummary> UpdateLanguageAsync(long id, UpdateUiLanguageRequest request, long actor, CancellationToken ct = default);

    /// <summary>启用/停用平台语言；停用委托 <see cref="ITenantLanguageService.DisablePlatformLanguageAsync"/> 迁移租户默认并停用其关系。</summary>
    Task SetEnabledAsync(long id, bool enabled, long actor, CancellationToken ct = default);

    /// <summary>按给定 Id 顺序重排语言目录（SortOrder 自 0 递增）。</summary>
    Task ReorderLanguagesAsync(IReadOnlyList<long> orderedIds, long actor, CancellationToken ct = default);
}

/// <summary>新建平台语言请求（M3-02）。</summary>
public sealed record CreateUiLanguageRequest(
    string? Culture,
    string? DisplayName,
    string? NativeName,
    string? CopyFromCulture = null);

/// <summary>更新平台语言请求（M3-02）；仅提供非空字段生效。</summary>
public sealed record UpdateUiLanguageRequest(
    string? DisplayName = null,
    string? NativeName = null,
    int? SortOrder = null);

/// <summary>平台语言摘要（M3-02），含基线翻译进度（待翻译计数）。</summary>
public sealed record UiLanguageSummary(
    long Id,
    string Culture,
    string DisplayName,
    string NativeName,
    bool Enabled,
    int SortOrder,
    int TranslatedCount,
    int TotalKeys);

/// <summary>平台语言列表结果（M3-02）。</summary>
public sealed record PlatformLanguageListResult(IReadOnlyList<UiLanguageSummary> Languages);
