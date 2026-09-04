using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Platform;
using SuperBuilder_AI.Models.Localization;

namespace SuperBuilder_AI.Services.Platform;

/// <summary>平台本地化目录（UiLanguage / UiTextResource）幂等种子，可在启动序列中调用。</summary>
public interface ILocalizationSeedService
{
    Task EnsureSeedAsync(CancellationToken ct = default);
}

/// <summary>
/// 从平台内置区域初始化数据库维护的语言目录与默认界面文案。
/// 与 <c>LocalizationController</c> 此前懒加载的种植逻辑等价，便于在启动顺序中固定执行
/// （M0-05：顺序 Schema → Identity/Permission → UiLanguage/Text → 默认策略/主题 → Bootstrap）。
/// </summary>
public sealed class LocalizationSeedService : ILocalizationSeedService
{
    private readonly SuperBIContext _db;
    private readonly ILocalizationService _localization;

    public LocalizationSeedService(SuperBIContext db, ILocalizationService localization)
    {
        _db = db;
        _localization = localization;
    }

    public async Task EnsureSeedAsync(CancellationToken ct = default)
    {
        var locales = _localization.SupportedLocales.ToList();
        var existingLanguages = await _db.UiLanguages.Select(x => x.Culture).ToListAsync(ct);
        _db.UiLanguages.AddRange(locales
            .Where(x => !existingLanguages.Contains(x.Culture, StringComparer.OrdinalIgnoreCase))
            .Select((x, i) => new UiLanguage
            {
                Culture = x.Culture,
                DisplayName = x.DisplayName,
                NativeName = x.DisplayName,
                Enabled = true,
                SortOrder = existingLanguages.Count + i,
            }));

        var defaults = new Dictionary<string, Dictionary<string, string>>
        {
            ["zh-CN"] = new()
            {
                ["Common.Login"] = "登录", ["Common.Confirm"] = "确认", ["Common.Cancel"] = "取消",
                ["Common.Save"] = "保存", ["Common.Close"] = "关闭", ["Document.OutboundOrder"] = "出库单",
                ["Login.Title"] = "登录 / 租户选择", ["Login.Username"] = "用户名", ["Login.Password"] = "口令",
                ["Login.TenantId"] = "租户 ID", ["Login.Tagline"] = "自然语言驱动的智能问数平台 —— 对话即分析，所见即洞察。",
                ["App.Subtitle"] = "智能问数平台", ["Common.Settings"] = "个人设置", ["Common.Logout"] = "退出登录",
            },
            ["en-US"] = new()
            {
                ["Common.Login"] = "Sign in", ["Common.Confirm"] = "Confirm", ["Common.Cancel"] = "Cancel",
                ["Common.Save"] = "Save", ["Common.Close"] = "Close", ["Document.OutboundOrder"] = "Outbound order",
                ["Login.Title"] = "Sign in / Select tenant", ["Login.Username"] = "Username", ["Login.Password"] = "Password",
                ["Login.TenantId"] = "Tenant ID", ["Login.Tagline"] = "Natural-language analytics — ask, analyze, and discover.",
                ["App.Subtitle"] = "AI Analytics Platform", ["Common.Settings"] = "Settings", ["Common.Logout"] = "Sign out",
            },
        };

        foreach (var locale in locales)
        {
            var source = defaults.TryGetValue(locale.Culture, out var exact) ? exact : defaults["en-US"];
            var existingKeys = await _db.UiTextResources
                .Where(x => x.TenantId == 0 && x.Culture == locale.Culture)
                .Select(x => x.ResourceKey).ToListAsync(ct);
            _db.UiTextResources.AddRange(source
                .Where(x => !existingKeys.Contains(x.Key, StringComparer.OrdinalIgnoreCase))
                .Select(x => new UiTextResource
                {
                    TenantId = 0,
                    Culture = locale.Culture,
                    ResourceKey = x.Key,
                    Value = x.Value,
                    Description = x.Key.StartsWith("Common.") ? "系统通用文本" : "系统界面文本",
                }));
        }

        await _db.SaveChangesAsync(ct);
    }
}
