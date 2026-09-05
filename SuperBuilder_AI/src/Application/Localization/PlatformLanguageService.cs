using System.Linq;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Audit;
using SuperBuilder_AI.Interfaces.Localization;
using SuperBuilder_AI.Models.Localization;
using SuperBuilder_AI.Models.Organization;

namespace SuperBuilder_AI.Services.Localization;

/// <summary>
/// 平台语言目录维护服务（M3-02 平台语言维护）。
/// <para>
/// 统一封装语言目录的查看/添加/启停/排序，强制 BCP 47 归一化与唯一性、DisplayName/NativeName 必填，
/// 并在新建语言时复制平台基线键集合（标记“待翻译”）。停用委托 <see cref="ITenantLanguageService.DisablePlatformLanguageAsync"/>
/// 完成租户侧关系迁移，避免孤立租户默认语言。
/// </para>
/// </summary>
public sealed class PlatformLanguageService : IPlatformLanguageService
{
    private readonly SuperBIContext _db;
    private readonly ITenantLanguageService _tenantLanguage;
    private readonly IAuditLogService _audit;

    public PlatformLanguageService(SuperBIContext db, ITenantLanguageService tenantLanguage, IAuditLogService audit)
    {
        _db = db;
        _tenantLanguage = tenantLanguage;
        _audit = audit;
    }

    public async Task<PlatformLanguageListResult> ListLanguagesAsync(bool includeDisabled, CancellationToken ct = default)
    {
        var langs = await _db.UiLanguages.AsNoTracking()
            .Where(x => includeDisabled || x.Enabled)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);

        var stats = (await _db.UiTextResources.AsNoTracking()
                .Where(x => x.TenantId == 0).ToListAsync(ct))
            .GroupBy(x => x.Culture)
            .ToDictionary(g => g.Key, g => (Total: g.Count(), Translated: g.Count(x => x.IsTranslated)));

        var summaries = langs.Select(l =>
        {
            stats.TryGetValue(l.Culture, out var s);
            return new UiLanguageSummary(l.Id, l.Culture, l.DisplayName, l.NativeName, l.Enabled, l.SortOrder, s.Translated, s.Total);
        }).ToList();

        return new PlatformLanguageListResult(summaries);
    }

    public async Task<UiLanguageSummary?> GetLanguageAsync(long id, CancellationToken ct = default)
    {
        var l = await _db.UiLanguages.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (l is null) return null;
        var total = await _db.UiTextResources.AsNoTracking().CountAsync(x => x.TenantId == 0 && x.Culture == l.Culture, ct);
        var translated = await _db.UiTextResources.AsNoTracking().CountAsync(x => x.TenantId == 0 && x.Culture == l.Culture && x.IsTranslated, ct);
        return new UiLanguageSummary(l.Id, l.Culture, l.DisplayName, l.NativeName, l.Enabled, l.SortOrder, translated, total);
    }

    public async Task<UiLanguageSummary> CreateLanguageAsync(CreateUiLanguageRequest request, long actor, CancellationToken ct = default)
    {
        var culture = LocaleContext.NormalizeCulture(request.Culture);
        if (culture is null)
            throw new PlatformLanguageException("语言代码无效，须为合法的 IETF BCP 47 标签（如 zh-CN、en-US、ja-JP）。");
        if (string.IsNullOrWhiteSpace(request.DisplayName) || string.IsNullOrWhiteSpace(request.NativeName))
            throw new PlatformLanguageException("显示名称（DisplayName）与本地名称（NativeName）均为必填。");
        if (await _db.UiLanguages.AnyAsync(x => x.Culture == culture, ct))
            throw new PlatformLanguageException($"语言代码 {culture} 已存在。");

        var language = new UiLanguage
        {
            Culture = culture,
            DisplayName = request.DisplayName!.Trim(),
            NativeName = request.NativeName!.Trim(),
            Enabled = true,
            SortOrder = await _db.UiLanguages.CountAsync(ct),
        };
        _db.UiLanguages.Add(language);
        await _db.SaveChangesAsync(ct);

        // 复制键集合：从 CopyFromCulture（默认 en-US）复制平台基线，标记“待翻译”。
        var copyFrom = LocaleContext.NormalizeCulture(request.CopyFromCulture) ?? "en-US";
        var source = await _db.UiTextResources.AsNoTracking()
            .Where(x => x.TenantId == 0 && x.Culture == copyFrom).ToListAsync(ct);
        if (source.Count > 0)
        {
            _db.UiTextResources.AddRange(source.Select(x => new UiTextResource
            {
                TenantId = 0,
                Culture = culture,
                ResourceKey = x.ResourceKey,
                Value = x.Value,
                Description = x.Description,
                IsTranslated = false,
            }));
            await _db.SaveChangesAsync(ct);
        }

        await _audit.LogAsync(new AuditLogEntry(0, "platform.language.create", "UiLanguage",
            UserId: actor, Actor: "system", EntityId: language.Id.ToString(), Result: "success",
            Message: $"平台语言 {culture} 已创建（复制自 {copyFrom}，待翻译 {source.Count} 项）。"));

        return (await GetLanguageAsync(language.Id, ct))!;
    }

    public async Task<UiLanguageSummary> UpdateLanguageAsync(long id, UpdateUiLanguageRequest request, long actor, CancellationToken ct = default)
    {
        var lang = await _db.UiLanguages.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (lang is null) throw new PlatformLanguageException($"未找到语言 Id={id}。");
        if (!string.IsNullOrWhiteSpace(request.DisplayName)) lang.DisplayName = request.DisplayName.Trim();
        if (!string.IsNullOrWhiteSpace(request.NativeName)) lang.NativeName = request.NativeName.Trim();
        if (request.SortOrder.HasValue) lang.SortOrder = request.SortOrder.Value;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(new AuditLogEntry(0, "platform.language.update", "UiLanguage",
            UserId: actor, Actor: "system", EntityId: id.ToString(), Result: "success",
            Message: $"平台语言 {lang.Culture} 已更新。"));

        return (await GetLanguageAsync(id, ct))!;
    }

    public async Task SetEnabledAsync(long id, bool enabled, long actor, CancellationToken ct = default)
    {
        var lang = await _db.UiLanguages.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (lang is null) throw new PlatformLanguageException($"未找到语言 Id={id}。");
        if (lang.Enabled == enabled) return;

        if (!enabled)
        {
            // 委托既有停用逻辑：迁移租户默认语言 + 停用全部租户侧该语言关系，并写审计。
            await _tenantLanguage.DisablePlatformLanguageAsync(lang.Culture, actor, ct);
            return;
        }

        lang.Enabled = true;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(new AuditLogEntry(0, "platform.language.enable", "UiLanguage",
            UserId: actor, Actor: "system", EntityId: id.ToString(), Result: "success",
            Message: $"平台语言 {lang.Culture} 已启用。"));
    }

    public async Task ReorderLanguagesAsync(IReadOnlyList<long> orderedIds, long actor, CancellationToken ct = default)
    {
        if (orderedIds is null || orderedIds.Count == 0) return;
        var langs = await _db.UiLanguages.Where(x => orderedIds.Contains(x.Id)).ToListAsync(ct);
        for (var i = 0; i < orderedIds.Count; i++)
        {
            var lang = langs.FirstOrDefault(x => x.Id == orderedIds[i]);
            if (lang is not null) lang.SortOrder = i;
        }
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(new AuditLogEntry(0, "platform.language.reorder", "UiLanguage",
            UserId: actor, Actor: "system", EntityId: string.Join(",", orderedIds), Result: "success",
            Message: $"平台语言目录已重排（{orderedIds.Count} 项）。"));
    }
}
