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
                ["Nav.Dashboard"] = "仪表盘", ["Nav.Ask"] = "问数", ["Nav.DataSources"] = "数据源", ["Nav.Admin"] = "管理",
                ["Accessibility.SkipToContent"] = "跳到主内容", ["Theme.Light"] = "浅色", ["Theme.Dark"] = "深色",
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
                ["Nav.Dashboard"] = "儀表板", ["Nav.Ask"] = "問數", ["Nav.DataSources"] = "資料來源", ["Nav.Admin"] = "管理",
                ["Accessibility.SkipToContent"] = "跳到主內容", ["Theme.Light"] = "淺色", ["Theme.Dark"] = "深色",
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
                ["Nav.Dashboard"] = "Dashboard", ["Nav.Ask"] = "Ask", ["Nav.DataSources"] = "Data Sources", ["Nav.Admin"] = "Admin",
                ["Accessibility.SkipToContent"] = "Skip to content", ["Theme.Light"] = "Light", ["Theme.Dark"] = "Dark",
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
                ["Nav.Dashboard"] = "ダッシュボード", ["Nav.Ask"] = "質問", ["Nav.DataSources"] = "データソース", ["Nav.Admin"] = "管理",
                ["Accessibility.SkipToContent"] = "メインコンテンツへ", ["Theme.Light"] = "ライト", ["Theme.Dark"] = "ダーク",
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
                ["Nav.Dashboard"] = "대시보드", ["Nav.Ask"] = "질문", ["Nav.DataSources"] = "데이터 소스", ["Nav.Admin"] = "관리",
                ["Accessibility.SkipToContent"] = "주요 콘텐츠로 건너뛰기", ["Theme.Light"] = "라이트", ["Theme.Dark"] = "다크",
            },
        };

        // M3-05：键驱动种子。以 ResourceKeys.All() 为权威键集，保证新增键即时覆盖：
        // zh-CN 取内置 ZhCnDefaults（与 RCL Keys.Defaults 镜像一致），en-US 取 ResourceKeys.Catalog.DefaultValue；
        // zh-TW/ja-JP/ko-KR 优先保留手维护母语基线（defaults），缺失键回退到 en-US，避免错译且保证全覆盖。
        var enUs = ResourceKeys.Catalog.ToDictionary(kv => kv.Key, kv => kv.Value.DefaultValue ?? kv.Key);

        foreach (var locale in locales)
        {
            var existingKeys = await _db.UiTextResources
                .Where(x => x.TenantId == 0 && x.Culture == locale.Culture)
                .Select(x => x.ResourceKey).ToListAsync(ct);

            var toAdd = new List<UiTextResource>();
            foreach (var key in ResourceKeys.All())
            {
                if (existingKeys.Contains(key, StringComparer.OrdinalIgnoreCase)) continue;

                // 解析值：① 该文化手维护母语基线；② zh-CN 内置简体；③ en-US 基线；④ 兜底键名。
                string value;
                if (defaults.TryGetValue(locale.Culture, out var hand) && hand.TryGetValue(key, out var hv))
                    value = hv;
                else if (locale.Culture == "zh-CN" && ZhCnDefaults.TryGetValue(key, out var zv))
                    value = zv;
                else if (enUs.TryGetValue(key, out var ev))
                    value = ev;
                else
                    value = key;

                toAdd.Add(new UiTextResource
                {
                    TenantId = 0,
                    Culture = locale.Culture,
                    ResourceKey = key,
                    Value = value,
                    Description = key.StartsWith("Common.") ? "系统通用文本" : "系统界面文本",
                    IsTranslated = true,
                });
            }

            _db.UiTextResources.AddRange(toAdd);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// 平台基线简体中文文案（zh-CN），与前端 RCL <c>Keys.Defaults</c> 的 ZhCn 镜像一致；后端单一事实来源。
    /// 公开以便回归测试断言「每个已登记键都有 zh-CN 基线」，避免新增键被静默降级为英文或键名。
    /// </summary>
    public static readonly Dictionary<string, string> ZhCnDefaults = new()
    {
        ["Common.Login"] = "登录", ["Common.Confirm"] = "确认", ["Common.Cancel"] = "取消",
        ["Common.Save"] = "保存", ["Common.Close"] = "关闭", ["Common.Settings"] = "个人设置",
        ["Common.Logout"] = "退出登录", ["Common.Loading"] = "加载中…", ["Common.Menu"] = "菜单",
        ["Common.Retry"] = "重试", ["Common.BackToWorkspace"] = "返回工作台", ["Common.CurrentUser"] = "当前用户",
        ["Common.Tenant"] = "租户", ["Common.User"] = "用户", ["Common.AccountMenu"] = "账号菜单",

        ["Login.Title"] = "登录 / 租户选择", ["Login.Username"] = "用户名", ["Login.Password"] = "口令",
        ["Login.TenantId"] = "租户 ID", ["Login.Tagline"] = "自然语言驱动的智能问数平台 —— 对话即分析，所见即洞察。",
        ["Login.InitEntryClosed"] = "初始化入口已关闭", ["Login.InitEntryClosedHint"] = "当前环境已禁用匿名初始化。请由部署配置完成首位平台管理员创建。",
        ["Login.InitPlatformAdmin"] = "初始化平台系统管理员", ["Login.InitNotice"] = "这是一次性初始化入口。创建完成后将永久关闭，请妥善保管管理员口令。",
        ["Login.AdminUsername"] = "管理员用户名", ["Login.DisplayName"] = "显示名", ["Login.AdminEmail"] = "管理员邮箱",
        ["Login.AdminPassword"] = "管理员口令", ["Login.ConfirmPassword"] = "确认口令", ["Login.CreateAdmin"] = "创建平台管理员",
        ["Login.TenantPlaceholder"] = "-- 请选择租户 --", ["Login.NoTenantRegister"] = "还没有租户？申请开通",
        ["Login.CheckingInit"] = "正在检查平台初始化状态…", ["Login.InitStatusError"] = "无法读取平台初始化状态。",
        ["Login.AdminUsernameRequired"] = "管理员用户名必填。", ["Login.AdminEmailInvalid"] = "请输入有效的管理员邮箱。",
        ["Login.AdminPasswordTooShort"] = "管理员口令至少 8 位。", ["Login.AdminPasswordMismatch"] = "两次输入的口令不一致。",
        ["Login.InitFailed"] = "平台管理员初始化失败。", ["Login.SelectTenantFirst"] = "请先选择租户。",
        ["Login.LoginFailed"] = "登录失败：用户不存在或已禁用。",
        ["Login.Hero.AINativeBI"] = "AI Native BI", ["Login.Hero.MultiTenant"] = "多租户", ["Login.Hero.Multilingual"] = "多语言",
        ["Login.Hero.LowCode"] = "低代码", ["Login.Hero.EnterpriseSaaS"] = "企业级 SaaS",

        ["App.Subtitle"] = "智能问数平台", ["Document.OutboundOrder"] = "出库单",

        ["Validation.Required"] = "此项为必填。", ["Validation.Email"] = "请输入有效的邮箱地址。", ["Validation.Format"] = "格式不正确。",

        ["Error.Generic"] = "发生错误，请稍后重试。", ["Error.NotFound"] = "未找到请求的资源。", ["Error.Unauthorized"] = "未授权。",
        ["Error.Forbidden"] = "无权访问该资源。", ["Error.Validation"] = "输入校验未通过。", ["Error.Conflict"] = "操作冲突，请刷新后重试。",
        ["Error.RateLimited"] = "请求过于频繁，请稍后再试。", ["Error.Maintenance"] = "系统维护中，请稍后访问。",
        ["Error.PageRender"] = "页面渲染出错", ["Error.PageRenderDesc"] = "该页面在渲染时发生异常，已被安全隔离，未影响其它功能。可重试当前页面或返回工作台。",
        ["Error.TechDetails"] = "技术详情",
        ["Error.Localization.CultureInvalid"] = "文化格式无效。", ["Error.Localization.TextEmpty"] = "文本不能为空。",
        ["Error.Localization.PlaceholderMismatch"] = "译文占位符与平台基线不一致。", ["Error.Localization.BaselineReset"] = "平台基线不能使用重置覆盖操作。",
        ["Error.OperationFailed"] = "操作失败。", ["Error.CodeLabel"] = "错误码：", ["Error.TraceIdLabel"] = "追踪 ID：",

        ["Page.Title.Home"] = "工作台", ["Page.Title.Profile"] = "个人设置", ["Page.Title.Tenants"] = "租户管理",
        ["Page.Title.TenantMembers"] = "租户成员", ["Page.Title.Identity"] = "身份与权限", ["Page.Title.Quota"] = "配额管理",
        ["Page.Title.PlatformAdmins"] = "平台管理员", ["Page.Title.PlatformAdminScopes"] = "管理员租户范围",
        ["Page.Title.SelfRegistrationAdmin"] = "自助注册", ["Page.Title.SystemStatus"] = "系统状态",
        ["Page.Title.Localization"] = "多语言中心", ["Page.Title.Themes"] = "主题（租户级）", ["Page.Title.DemoData"] = "演示数据",

        ["Empty.NoData"] = "暂无数据。",

        ["Nav.Home"] = "首页", ["Nav.Ask"] = "Ask BI 智能问数", ["Nav.Dashboards"] = "仪表盘", ["Nav.Apps"] = "应用工厂",
        ["Nav.Agent"] = "智能体 / Copilot", ["Nav.SemanticLabels"] = "语义标签", ["Nav.BusinessModel"] = "语义模型",
        ["Nav.Components"] = "组件库", ["Nav.ThemeEditor"] = "主题编辑器", ["Nav.DataSources"] = "数据源",
        // 历史别名：与 RCL 镜像、后端 Catalog 保持一致（护栏测试 All_RegisteredKeys_HaveZhCnBaseline 会校验）。
        ["Nav.Dashboard"] = "仪表盘", ["Nav.Admin"] = "管理",
        ["Nav.ModelAccounts"] = "模型与账号", ["Nav.Tenants"] = "租户", ["Nav.TenantMembers"] = "租户成员",
        ["Nav.SelfRegistration"] = "自助注册", ["Nav.DemoData"] = "演示数据", ["Nav.PlatformAdmins"] = "平台管理员",
        ["Nav.PlatformAdminScopes"] = "管理员租户范围", ["Nav.Identity"] = "身份权限", ["Nav.Audit"] = "审计",
        ["Nav.Quota"] = "配额", ["Nav.Localization"] = "多语言", ["Nav.Themes"] = "主题", ["Nav.System"] = "系统状态",
        ["Nav.Group.Flagship"] = "旗舰", ["Nav.Group.Analysis"] = "分析", ["Nav.Group.Custom"] = "自定义",
        ["Nav.Group.PlatformExt"] = "平台扩展", ["Nav.Group.Admin"] = "管理后台",

        ["Accessibility.SkipToContent"] = "跳到主内容",
        ["Theme.Light"] = "浅色", ["Theme.Dark"] = "深色", ["Theme.Switch"] = "切换主题",
    };
}
