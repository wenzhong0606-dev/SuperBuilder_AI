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

        // M3-G0：补全全部 5 种文化的平台基线（原 14 键译文 + Validation/Error/Empty/Loading 键），
        // 键集合与 <see cref="ResourceKeys"/> 登记保持一致。
        var defaults = new Dictionary<string, Dictionary<string, string>>
        {
            ["zh-CN"] = new()
            {
                ["Common.Login"] = "登录", ["Common.Confirm"] = "确认", ["Common.Cancel"] = "取消",
                ["Common.Save"] = "保存", ["Common.Close"] = "关闭", ["Document.OutboundOrder"] = "出库单",
                ["Login.Title"] = "登录 / 租户选择", ["Login.Username"] = "用户名", ["Login.Password"] = "口令",
                ["Login.TenantId"] = "租户 ID", ["Login.Tagline"] = "自然语言驱动的智能问数平台 —— 对话即分析，所见即洞察。",
                ["App.Subtitle"] = "智能问数平台", ["Common.Settings"] = "个人设置", ["Common.Logout"] = "退出登录",
                ["Common.Loading"] = "加载中…",
                ["Validation.Required"] = "此项为必填。", ["Validation.Email"] = "请输入有效的邮箱地址。", ["Validation.Format"] = "格式不正确。",
                ["Error.Generic"] = "发生错误，请稍后重试。", ["Error.NotFound"] = "未找到请求的资源。", ["Empty.NoData"] = "暂无数据。",
            },
            ["zh-TW"] = new()
            {
                ["Common.Login"] = "登入", ["Common.Confirm"] = "確認", ["Common.Cancel"] = "取消",
                ["Common.Save"] = "儲存", ["Common.Close"] = "關閉", ["Document.OutboundOrder"] = "出貨單",
                ["Login.Title"] = "登入 / 選擇租戶", ["Login.Username"] = "使用者名稱", ["Login.Password"] = "密碼",
                ["Login.TenantId"] = "租戶 ID", ["Login.Tagline"] = "以自然語言驅動的智能問數平台 —— 對話即分析，所見即洞察。",
                ["App.Subtitle"] = "智能問數平台", ["Common.Settings"] = "個人設定", ["Common.Logout"] = "登出",
                ["Common.Loading"] = "載入中…",
                ["Validation.Required"] = "此項為必填。", ["Validation.Email"] = "請輸入有效的電子郵件地址。", ["Validation.Format"] = "格式不正確。",
                ["Error.Generic"] = "發生錯誤，請稍後再試。", ["Error.NotFound"] = "找不到要求的資源。", ["Empty.NoData"] = "暫無資料。",
            },
            ["en-US"] = new()
            {
                ["Common.Login"] = "Sign in", ["Common.Confirm"] = "Confirm", ["Common.Cancel"] = "Cancel",
                ["Common.Save"] = "Save", ["Common.Close"] = "Close", ["Document.OutboundOrder"] = "Outbound order",
                ["Login.Title"] = "Sign in / Select tenant", ["Login.Username"] = "Username", ["Login.Password"] = "Password",
                ["Login.TenantId"] = "Tenant ID", ["Login.Tagline"] = "Natural-language analytics — ask, analyze, and discover.",
                ["App.Subtitle"] = "AI Analytics Platform", ["Common.Settings"] = "Settings", ["Common.Logout"] = "Sign out",
                ["Common.Loading"] = "Loading…",
                ["Validation.Required"] = "This field is required.", ["Validation.Email"] = "Please enter a valid email address.", ["Validation.Format"] = "Invalid format.",
                ["Error.Generic"] = "An error occurred. Please try again later.", ["Error.NotFound"] = "The requested resource was not found.", ["Empty.NoData"] = "No data available.",
            },
            ["ja-JP"] = new()
            {
                ["Common.Login"] = "ログイン", ["Common.Confirm"] = "確認", ["Common.Cancel"] = "キャンセル",
                ["Common.Save"] = "保存", ["Common.Close"] = "閉じる", ["Document.OutboundOrder"] = "出荷指示書",
                ["Login.Title"] = "ログイン / テナント選択", ["Login.Username"] = "ユーザー名", ["Login.Password"] = "パスワード",
                ["Login.TenantId"] = "テナントID", ["Login.Tagline"] = "自然言語で語る分析プラットフォーム —— 対話するだけで分析、見るだけで洞察。",
                ["App.Subtitle"] = "AI分析プラットフォーム", ["Common.Settings"] = "個人設定", ["Common.Logout"] = "ログアウト",
                ["Common.Loading"] = "読み込み中…",
                ["Validation.Required"] = "この項目は必須です。", ["Validation.Email"] = "有効なメールアドレスを入力してください。", ["Validation.Format"] = "形式が正しくありません。",
                ["Error.Generic"] = "エラーが発生しました。後でもう一度お試しください。", ["Error.NotFound"] = "要求されたリソースが見つかりません。", ["Empty.NoData"] = "データがありません。",
            },
            ["ko-KR"] = new()
            {
                ["Common.Login"] = "로그인", ["Common.Confirm"] = "확인", ["Common.Cancel"] = "취소",
                ["Common.Save"] = "저장", ["Common.Close"] = "닫기", ["Document.OutboundOrder"] = "출고 주문",
                ["Login.Title"] = "로그인 / 테넌트 선택", ["Login.Username"] = "사용자 이름", ["Login.Password"] = "비밀번호",
                ["Login.TenantId"] = "테넌트 ID", ["Login.Tagline"] = "자연어 기반 지능형 분석 플랫폼 —— 대화가 곧 분석, 보는 것이 곧 인사이트.",
                ["App.Subtitle"] = "AI 분석 플랫폼", ["Common.Settings"] = "개인 설정", ["Common.Logout"] = "로그아웃",
                ["Common.Loading"] = "로딩 중…",
                ["Validation.Required"] = "이 항목은 필수입니다。", ["Validation.Email"] = "유효한 이메일 주소를 입력하세요。", ["Validation.Format"] = "형식이 올바르지 않습니다。",
                ["Error.Generic"] = "오류가 발생했습니다. 잠시 후 다시 시도하세요。", ["Error.NotFound"] = "요청한 리소스를 찾을 수 없습니다。", ["Empty.NoData"] = "데이터가 없습니다。",
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
