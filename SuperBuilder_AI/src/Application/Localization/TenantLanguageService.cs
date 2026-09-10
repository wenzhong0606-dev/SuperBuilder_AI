using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Audit;
using SuperBuilder_AI.Interfaces.Localization;
using SuperBuilder_AI.Models.Localization;

namespace SuperBuilder_AI.Services.Localization;

/// <summary>
/// 租户界面语言关系服务（M3-01 语言关系模型）。
/// <para>以 <see cref="TenantUiLanguage"/> 关系替代 <c>localization:availableCultures/defaultCulture</c> JSON，
/// 并在事务内强制约束：每租户至少一种启用语言、恰一个默认语言、默认语言必须启用。</para>
/// </summary>
public sealed class TenantLanguageService : ITenantLanguageService
{
    private readonly SuperBIContext _db;
    private readonly IAuditLogService _audit;

    public TenantLanguageService(SuperBIContext db, IAuditLogService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<List<TenantLanguageInfo>> GetLanguagesAsync(long tenantId, CancellationToken ct = default)
    {
        return await _db.TenantUiLanguages.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Join(_db.UiLanguages.AsNoTracking(), t => t.UiLanguageId, u => u.Id,
                (t, u) => new TenantLanguageInfo(u.Id, u.Culture, u.DisplayName, u.NativeName, t.Enabled, t.IsDefault, t.SortOrder))
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);
    }

    public async Task<List<string>> GetAvailableCulturesAsync(long tenantId, CancellationToken ct = default)
    {
        var list = await _db.TenantUiLanguages.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Enabled)
            .Join(_db.UiLanguages.AsNoTracking(), t => t.UiLanguageId, u => u.Id, (t, u) => u.Culture)
            .ToListAsync(ct);
        if (list.Count == 0) list.Add("zh-CN");
        return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<string> GetDefaultCultureAsync(long tenantId, CancellationToken ct = default)
    {
        var def = await _db.TenantUiLanguages.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsDefault && x.Enabled)
            .Join(_db.UiLanguages.AsNoTracking(), t => t.UiLanguageId, u => u.Id, (t, u) => u.Culture)
            .FirstOrDefaultAsync(ct);
        if (def is not null) return def;

        var first = await _db.TenantUiLanguages.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Enabled)
            .Join(_db.UiLanguages.AsNoTracking(), t => t.UiLanguageId, u => u.Id, (t, u) => new { u.Culture, t.SortOrder })
            .OrderBy(x => x.SortOrder)
            .Select(x => x.Culture)
            .FirstOrDefaultAsync(ct);
        return first ?? "zh-CN";
    }

    public async Task<Dictionary<long, List<TenantLanguageInfo>>> GetLanguagesForTenantsAsync(IReadOnlyList<long> tenantIds, CancellationToken ct = default)
    {
        var dict = new Dictionary<long, List<TenantLanguageInfo>>();
        if (tenantIds.Count == 0) return dict;
        var rows = await _db.TenantUiLanguages.AsNoTracking()
            .Where(x => tenantIds.Contains(x.TenantId))
            .OrderBy(x => x.SortOrder)
            .Join(_db.UiLanguages.AsNoTracking(), t => t.UiLanguageId, u => u.Id,
                (t, u) => new { t.TenantId, Info = new TenantLanguageInfo(u.Id, u.Culture, u.DisplayName, u.NativeName, t.Enabled, t.IsDefault, t.SortOrder) })
            .ToListAsync(ct);
        foreach (var r in rows)
        {
            if (!dict.TryGetValue(r.TenantId, out var list))
            {
                list = new List<TenantLanguageInfo>();
                dict[r.TenantId] = list;
            }
            list.Add(r.Info);
        }
        return dict;
    }

    public async Task SetLanguagesAsync(long tenantId, IReadOnlyList<TenantLanguageUpdate> updates, long actor, CancellationToken ct = default)
    {
        if (updates is null || updates.Count == 0)
            throw new TenantLanguageException("至少需要配置一种语言。");

        var uiIds = updates.Select(u => u.UiLanguageId).Distinct().ToList();
        var langs = await _db.UiLanguages.AsNoTracking().Where(u => uiIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);

        foreach (var u in updates)
        {
            if (!langs.TryGetValue(u.UiLanguageId, out var lang))
                throw new TenantLanguageException($"未知的语言目录 Id：{u.UiLanguageId}。");
            if (!lang.Enabled && u.Enabled)
                throw new TenantLanguageException($"语言 {lang.Culture} 已在平台停用，不能在租户启用。");
        }

        if (updates.Count(u => u.Enabled) == 0)
            throw new TenantLanguageException("每租户至少需启用一种语言。");
        var defaults = updates.Where(u => u.IsDefault).ToList();
        if (defaults.Count == 0)
            throw new TenantLanguageException("必须指定一个默认语言。");
        if (defaults.Count > 1)
            throw new TenantLanguageException("默认语言只能有一个。");
        if (!defaults[0].Enabled)
            throw new TenantLanguageException("默认语言必须处于启用状态。");

        var existing = await _db.TenantUiLanguages.Where(x => x.TenantId == tenantId).ToListAsync(ct);
        var toRemove = existing.Where(e => !uiIds.Contains(e.UiLanguageId)).ToList();
        if (toRemove.Count > 0) _db.TenantUiLanguages.RemoveRange(toRemove);

        foreach (var upd in updates)
        {
            var row = existing.FirstOrDefault(e => e.UiLanguageId == upd.UiLanguageId);
            if (row is null)
            {
                row = new TenantUiLanguage { TenantId = tenantId, UiLanguageId = upd.UiLanguageId, CreatedBy = actor.ToString() };
                _db.TenantUiLanguages.Add(row);
            }
            row.Enabled = upd.Enabled;
            row.IsDefault = upd.IsDefault;
            row.SortOrder = upd.SortOrder;
            row.UpdatedBy = actor.ToString();
            row.UpdatedTime = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(new AuditLogEntry(tenantId, "tenant.language.set", "TenantUiLanguage",
            UserId: actor, Actor: "system", EntityId: tenantId.ToString(), Result: "success",
            Message: $"租户 {tenantId} 语言配置已更新（{updates.Count(u => u.Enabled)} 启用，默认 {defaults[0].UiLanguageId}）。"));
    }

    public async Task EnsureTenantLanguagesAsync(long tenantId, CancellationToken ct = default)
    {
        var existing = await _db.TenantUiLanguages.AsNoTracking().Where(x => x.TenantId == tenantId).ToListAsync(ct);
        if (existing.Count > 0)
        {
            // 仅当"从未真正配置"——无 localization:availableCultures 设置，且只有一条默认 zh-CN 记录
            // （旧回退逻辑遗留）——才视为未配置，清掉后以平台全语言集重建，避免租户语言恒为单一 zh-CN。
            var hasExplicitConfig = await _db.TenantSettings.AsNoTracking()
                .AnyAsync(s => s.TenantId == tenantId && s.Key == "localization:availableCultures", ct);
            var zhCnId = await _db.UiLanguages.AsNoTracking()
                .Where(u => u.Culture == "zh-CN").Select(u => u.Id).FirstOrDefaultAsync(ct);
            var isDefaultOnlyZhCn = existing.Count == 1
                && existing[0].IsDefault
                && existing[0].UiLanguageId == zhCnId
                && !hasExplicitConfig;
            if (isDefaultOnlyZhCn)
            {
                foreach (var r in existing) _db.TenantUiLanguages.Remove(r);
                await _db.SaveChangesAsync(ct);
            }
            else
            {
                return; // 视为已显式配置，尊重既有选择
            }
        }

        var json = await _db.TenantSettings.AsNoTracking()
            .Where(s => s.TenantId == tenantId && (s.Key == "localization:availableCultures" || s.Key == "localization:defaultCulture"))
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);
        List<string> cultures;
        try
        {
            cultures = JsonSerializer.Deserialize<List<string>>(json.GetValueOrDefault("localization:availableCultures") ?? "[]") ?? new();
        }
        catch (JsonException)
        {
            cultures = new();
        }
        cultures = cultures.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var defaultCulture = json.GetValueOrDefault("localization:defaultCulture");

        var uiLangs = await _db.UiLanguages.AsNoTracking().ToListAsync(ct);
        var byCulture = uiLangs.ToDictionary(u => u.Culture, u => u, StringComparer.OrdinalIgnoreCase);

        var rows = new List<TenantUiLanguage>();
        if (cultures.Count > 0)
        {
            var order = 0;
            foreach (var c in cultures)
            {
                if (byCulture.TryGetValue(c, out var u))
                {
                    var isDefault = string.Equals(c, defaultCulture, StringComparison.OrdinalIgnoreCase)
                        || (rows.Count == 0 && string.IsNullOrWhiteSpace(defaultCulture));
                    rows.Add(new TenantUiLanguage
                    {
                        TenantId = tenantId,
                        UiLanguageId = u.Id,
                        Enabled = true,
                        IsDefault = isDefault,
                        SortOrder = order++,
                        CreatedBy = "system",
                    });
                }
            }
        }

        if (rows.Count == 0)
        {
            // 无显式配置时继承平台全语言集（所有启用的平台语言），而非仅 zh-CN，
            // 使语言切换器在全新安装中可用。
            var platformLangs = uiLangs.Where(u => u.Enabled).OrderBy(u => u.SortOrder).ToList();
            if (platformLangs.Count == 0) platformLangs = uiLangs.ToList();
            var order = 0;
            foreach (var u in platformLangs)
            {
                var isDefault = string.Equals(u.Culture, defaultCulture, StringComparison.OrdinalIgnoreCase)
                    || (rows.Count == 0 && (string.IsNullOrWhiteSpace(defaultCulture) || string.Equals(u.Culture, "zh-CN", StringComparison.OrdinalIgnoreCase)));
                rows.Add(new TenantUiLanguage
                {
                    TenantId = tenantId,
                    UiLanguageId = u.Id,
                    Enabled = true,
                    IsDefault = isDefault,
                    SortOrder = order++,
                    CreatedBy = "system",
                });
            }
        }

        EnsureSingleDefault(rows);
        if (rows.Count == 0) return;

        _db.TenantUiLanguages.AddRange(rows);
        await _db.SaveChangesAsync(ct);
    }

    public async Task EnsureAllTenantsLanguagesAsync(CancellationToken ct = default)
    {
        var tenantIds = await _db.Tenants.AsNoTracking().Select(t => t.Id).ToListAsync(ct);
        foreach (var id in tenantIds)
            await EnsureTenantLanguagesAsync(id, ct);
    }

    public async Task<DisablePlatformLanguageResult> DisablePlatformLanguageAsync(string culture, long actor, CancellationToken ct = default)
    {
        var normalized = (culture ?? string.Empty).Trim();
        var lang = await _db.UiLanguages.FirstOrDefaultAsync(u => u.Culture == normalized, ct);
        if (lang is null)
            throw new TenantLanguageException($"未知的平台语言：{culture}。");
        if (!lang.Enabled)
            return new DisablePlatformLanguageResult(normalized, Array.Empty<long>(), Array.Empty<long>());

        var affectedRows = await _db.TenantUiLanguages
            .Where(x => x.IsDefault && x.UiLanguageId == lang.Id)
            .ToListAsync(ct);
        var affected = affectedRows.Select(x => x.TenantId).Distinct().ToList();
        var migrated = new List<long>();

        foreach (var tid in affected)
        {
            var others = await _db.TenantUiLanguages
                .Where(x => x.TenantId == tid && x.UiLanguageId != lang.Id && x.Enabled)
                .OrderBy(x => x.SortOrder).ToListAsync(ct);
            TenantUiLanguage newDefault;
            if (others.Count > 0)
            {
                newDefault = others[0];
            }
            else
            {
                var any = await _db.TenantUiLanguages
                    .Where(x => x.TenantId == tid && x.UiLanguageId != lang.Id)
                    .OrderBy(x => x.SortOrder).FirstOrDefaultAsync(ct);
                if (any is null)
                {
                    // 租户仅有被停用语言：从平台目录中挑选首个其他启用语言新建启用关系，避免陷入无可用语言状态。
                    var fallback = await _db.UiLanguages.AsNoTracking()
                        .Where(u => u.Id != lang.Id && u.Enabled)
                        .OrderBy(u => u.SortOrder).FirstOrDefaultAsync(ct);
                    if (fallback is null) continue;
                    any = new TenantUiLanguage { TenantId = tid, UiLanguageId = fallback.Id, Enabled = true, CreatedBy = actor.ToString() };
                    _db.TenantUiLanguages.Add(any);
                }
                any.Enabled = true;
                newDefault = any;
            }

            foreach (var r in await _db.TenantUiLanguages.Where(x => x.TenantId == tid && x.IsDefault).ToListAsync(ct))
                r.IsDefault = false;
            newDefault.IsDefault = true;
            newDefault.Enabled = true;
            newDefault.UpdatedBy = actor.ToString();
            newDefault.UpdatedTime = DateTime.UtcNow;
            migrated.Add(tid);
        }

        // 平台级禁用后，所有租户侧该语言关系一并停用，避免暴露已下线的平台语言。
        foreach (var r in await _db.TenantUiLanguages.Where(x => x.UiLanguageId == lang.Id).ToListAsync(ct))
        {
            r.Enabled = false;
            r.IsDefault = false;
            r.UpdatedBy = actor.ToString();
            r.UpdatedTime = DateTime.UtcNow;
        }

        lang.Enabled = false;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(new AuditLogEntry(0, "platform.language.disabled", "UiLanguage",
            UserId: actor, Actor: "system", EntityId: lang.Id.ToString(), Result: "success",
            Message: $"平台语言 {normalized} 已停用；影响租户 {affected.Count}，迁移默认语言 {migrated.Count}。"));

        return new DisablePlatformLanguageResult(normalized, affected, migrated);
    }

    private static void EnsureSingleDefault(List<TenantUiLanguage> rows)
    {
        if (rows.Count == 0) return;
        var defaults = rows.Where(r => r.IsDefault).ToList();
        if (defaults.Count == 0)
        {
            var firstEnabled = rows.FirstOrDefault(r => r.Enabled) ?? rows[0];
            firstEnabled.IsDefault = true;
        }
        else if (defaults.Count > 1)
        {
            for (var i = 1; i < defaults.Count; i++) defaults[i].IsDefault = false;
        }
    }
}
