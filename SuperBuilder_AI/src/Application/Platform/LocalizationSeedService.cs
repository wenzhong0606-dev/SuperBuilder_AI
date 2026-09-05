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

        // M3-05 递延项：后端统一错误码 zh-CN 基线（与 Api/Errors/ErrorCodes.cs 的 Friendly 中文文案完全一致）。
        ["Error.SB_BAD_REQUEST"] = "请求参数不合法，请检查输入后重试。",
        ["Error.SB_UNAUTHORIZED"] = "鉴权失败，请重新登录后再试。",
        ["Error.SB_FORBIDDEN"] = "权限不足，当前账号无权执行该操作。",
        ["Error.SB_NOT_FOUND"] = "请求的资源不存在或已被删除。",
        ["Error.SB_UNSUPPORTED"] = "当前操作不被支持。",
        ["Error.SB_INTERNAL"] = "服务暂时不可用，请稍后重试；如持续出现，可凭错误码联系管理员。",
        ["Error.SB_SERVICE_UNAVAILABLE"] = "平台尚未就绪（数据库不可达或尚未完成初始化），请稍后重试或联系管理员。",
        ["Error.SB_TOO_MANY_REQUESTS"] = "请求过于频繁，请稍后再试。",
        ["Error.SB_AUTH_001"] = "用户名或租户不存在，或账号已被禁用。",
        ["Error.SB_BI_001"] = "未能从问题中识别出可查询的数据表，请换一种表述或指定具体业务对象（如「订单」「库存」）。",
        ["Error.SB_BI_002"] = "未能从问题中识别可分析的字段，请补充指标或维度（如「销售额」「按地区」）。",
        ["Error.SB_BI_003"] = "当前业务语义暂不支持该分析（如聚合方式不受支持），请调整问法。",
        ["Error.SB_BI_004"] = "问题中存在多个可能匹配的度量字段，请明确指定（如「订单金额」而非「金额」）。",
        ["Error.SB_BI_005"] = "模型对该查询的把握不足，请补充更明确的指标、维度或筛选条件后重试。",
        ["Error.SB_BI_006"] = "数据源暂时不可达，请稍后重试或联系管理员检查连接。",
        ["Error.SB_APP_001"] = "应用定义（DSL）不合法，请检查组件配置后重试。",
        ["Error.SB_AGENT_001"] = "智能体未返回有效内容，请重新描述任务。",
        ["Error.SB_PFM_001"] = "当前租户配额已用尽，请升级套餐或联系管理员。",
        ["Error.SB_PFM_002"] = "操作越过了租户边界，已被安全策略拒绝。",
        ["Error.SB_AUTHZ_001"] = "当前账号无权访问所选数据源。",
        ["Error.SB_AUTHZ_002"] = "当前账号没有满足行级数据策略的访问范围。",
        ["Error.SB_SECURITY_001"] = "查询计划未通过最终安全校验，已在执行前阻断。",

        ["Page.Title.Home"] = "工作台", ["Page.Title.Profile"] = "个人设置", ["Page.Title.Tenants"] = "租户管理",
        ["Page.Title.TenantMembers"] = "租户成员", ["Page.Title.Identity"] = "身份与权限", ["Page.Title.Quota"] = "配额管理",
        ["Page.Title.PlatformAdmins"] = "平台管理员", ["Page.Title.PlatformAdminScopes"] = "管理员租户范围",
        ["Page.Title.SelfRegistrationAdmin"] = "自助注册", ["Page.Title.SystemStatus"] = "系统状态",
        ["Page.Title.Localization"] = "多语言中心", ["Page.Title.Themes"] = "主题（租户级）", ["Page.Title.DemoData"] = "演示数据",
        ["Page.Title.BusinessModel"] = "语义模型", ["Page.Title.BusinessModelEntityDetail"] = "实体详情",
        ["Page.Title.SemanticLabelDetail"] = "语义标签详情", ["Page.Title.ComponentGallery"] = "组件库",
        ["Page.Title.ThemeEditor"] = "主题编辑器", ["Page.Title.DataSources"] = "数据源管理",
        ["Page.Title.ModelAccounts"] = "模型与账号（BYO）", ["Page.Title.MetadataEntityDetail"] = "元数据关系详情",
        ["Page.Title.DataSource"] = "数据源详情",

        ["Page.Desc.BusinessModel"] = "把物理表结构映射为业务语言：实体、业务域与字段同义词，让自然语言问数更精准。",
        ["Page.Desc.BusinessModelEntityDetail"] = "查看实体映射、字段口径与解析状态。",
        ["Page.Desc.SemanticLabelDetail"] = "查看标签口径、同义词与召回策略。",
        ["Page.Desc.ComponentGallery"] = "SuperBuilder 设计系统一览：基于中国古风色板与 Bootstrap 的通用组件，全站统一复用。",
        ["Page.Desc.ThemeEditor"] = "基于古风色板自定义你的视觉风格：实时预览、保存并指派给租户或复制为蓝图。",
        ["Page.Desc.DataSources"] = "统一管理租户数据连接、元数据覆盖与访问状态。",
        ["Page.Desc.ModelAccounts"] = "选择当前 AI 模型，或绑定你自有的模型 API Key（BYO），按租户隔离、密钥掩码存储。",
        ["Page.Desc.MetadataEntityDetail"] = "查看元数据实体的实际数据、所属对象及向量索引关系。",
        ["Page.Desc.DataSource"] = "数据源的授权清单与行级安全策略（RLS）。",

        ["Empty.NoData"] = "暂无数据。",

        ["Action.New"] = "新建", ["Action.Create"] = "创建", ["Action.Save"] = "保存", ["Action.Cancel"] = "取消",
        ["Action.Refresh"] = "刷新", ["Action.Delete"] = "删除", ["Action.Edit"] = "编辑",         ["Action.Search"] = "搜索", ["Action.Close"] = "关闭", ["Action.Reset"] = "重置",

        ["Page.ThemeEditor.Preview"] = "实时预览", ["Page.ThemeEditor.Palette"] = "调色板",

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

        // M3-05 重内容页正文批次：Content.* 简体基线
        ["Content.BusinessModelDomains"] = "业务域", ["Content.BusinessModelDomainsSub"] = "用于分类",
        ["Content.BusinessModelEntities"] = "实体", ["Content.BusinessModelEntitiesSub"] = "对应业务表",
        ["Content.BusinessModelResolved"] = "已解析语义", ["Content.BusinessModelResolvedSub"] = "可在 Ask 提问",
        ["Content.BusinessModelSearchPlaceholder"] = "搜索实体…",
        ["Content.BusinessModelEmptyDomainsTitle"] = "暂无业务域",
        ["Content.BusinessModelEmptyDomainsText"] = "业务域用于组织对实体的分类，例如「销售」「库存」「财务」。",
        ["Content.BusinessModelEmptyEntitiesTitle"] = "暂无实体",
        ["Content.BusinessModelEmptyEntitiesText"] = "实体对应一张业务表（如「客户」「订单」），解析后可在 Ask 中直接以业务名提问。",
        ["Content.BusinessModelNoMatch"] = "无匹配实体",
        ["Content.BusinessModelDetail"] = "详情",
        ["Content.BusinessModelEditorNotReady"] = "实体编辑器将在 S2 阶段接入。",

        ["Content.BusinessModelEntityLoading"] = "正在加载实体详情…",
        ["Content.BusinessModelEntityPanelDetail"] = "详情",
        ["Content.BusinessModelEntityEmptyTitle"] = "未找到该实体",
        ["Content.BusinessModelEntityEmptyText"] = "实体可能已被删除，或后端详情端点尚未接入。",
        ["Content.BusinessModelEntityPanelInfo"] = "实体信息",
        ["Content.BusinessModelEntityBack"] = "返回语义模型",

        ["Content.DataSourcesNew"] = "新增数据源",
        ["Content.DataSourcesMetricTotal"] = "数据源总数", ["Content.DataSourcesMetricOnline"] = "在线连接",
        ["Content.DataSourcesMetricTables"] = "元数据表", ["Content.DataSourcesMetricColumns"] = "已识别字段",
        ["Content.DataSourcesPanelAssets"] = "连接资产",
        ["Content.DataSourcesSearchPlaceholder"] = "搜索名称或数据库类型…",
        ["Content.DataSourcesLoading"] = "正在加载数据源…",
        ["Content.DataSourcesEmptyTitle"] = "暂无数据源",
        ["Content.DataSourcesEmptyText"] = "请在下方创建第一个数据源。",
        ["Content.DataSourcesColId"] = "ID", ["Content.DataSourcesColName"] = "名称", ["Content.DataSourcesColType"] = "类型",
        ["Content.DataSourcesColStatus"] = "状态", ["Content.DataSourcesColTables"] = "元数据表", ["Content.DataSourcesColColumns"] = "字段",
        ["Content.DataSourcesTenantScoped"] = "租户专属连接",
        ["Content.DataSourcesStatusOnline"] = "运行中", ["Content.DataSourcesStatusOffline"] = "已停用",
        ["Content.DataSourcesManage"] = "管理连接 →",
        ["Content.DataSourcesModalTitle"] = "接入新的数据源",
        ["Content.DataSourcesConnSqlServer"] = "企业主流关系型数据库", ["Content.DataSourcesConnMysql"] = "WMS 等业务常用",
        ["Content.DataSourcesConnPostgres"] = "开源关系型数据库", ["Content.DataSourcesConnOracle"] = "大型事务系统",
        ["Content.DataSourcesConnClickhouse"] = "列式分析型数据库", ["Content.DataSourcesConnMongodb"] = "文档型 NoSQL",
        ["Content.DataSourcesFieldName"] = "数据源名称", ["Content.DataSourcesFieldNameHint"] = "给团队一个易识别的名称",
        ["Content.DataSourcesFieldNamePlaceholder"] = "如：WMS 生产库", ["Content.DataSourcesFieldType"] = "连接器类型",
        ["Content.DataSourcesFieldConnStr"] = "连接串", ["Content.DataSourcesFieldConnStrPlaceholder"] = "Server=host;Database=db;User=...;",
        ["Content.DataSourcesTestConn"] = "测试连接", ["Content.DataSourcesSaveAndConnect"] = "保存并接入",
        ["Content.DataSourcesLoadFailed"] = "加载数据源失败。",
        ["Content.DataSourcesConnectorSelected"] = "已选择连接器：{0}",
        ["Content.DataSourcesTestSubmitted"] = "连通性测试已提交（Multi-DB Connector 后端计划于 P12 实现）。",
        ["Content.DataSourcesRequiredError"] = "数据源名称和连接串必填。",
        ["Content.DataSourcesSaveFailed"] = "保存数据源失败。",
        ["Content.DataSourcesSavedToast"] = "数据源已保存，并已授权给当前管理员。Ask 页面现在可以选择它。",
        ["Content.DataSourcesSaving"] = "正在保存…",

        ["Content.DataSourceBackList"] = "返回列表",
        ["Content.DataSourceTabMeta"] = "元数据", ["Content.DataSourceRescan"] = "重新扫描", ["Content.DataSourceScanning"] = "扫描中…",
        ["Content.DataSourceLoadingMeta"] = "正在加载元数据…",
        ["Content.DataSourceEmptyMetaTitle"] = "尚未扫描到元数据",
        ["Content.DataSourceEmptyMetaText"] = "点击重新扫描，从业务数据库读取表和字段结构。",
        ["Content.DataSourceFieldsBadge"] = "{0} 字段",
        ["Content.DataSourceColFieldRel"] = "字段 / 关系", ["Content.DataSourceColType"] = "类型",
        ["Content.DataSourceColNullable"] = "可空", ["Content.DataSourceColSemantic"] = "Semantic",
        ["Content.DataSourceColVector"] = "Vector", ["Content.DataSourceColBusinessKey"] = "业务键",
        ["Content.DataSourceNotVectorized"] = "未向量化",
        ["Content.DataSourceTabGrants"] = "访问授权", ["Content.DataSourceGrantUser"] = "用户", ["Content.DataSourceGrantRole"] = "角色",
        ["Content.DataSourceGrantSubjectPlaceholder"] = "选择授权主体…", ["Content.DataSourceGrantAccess"] = "授予访问",
        ["Content.DataSourceRefreshGrants"] = "刷新授权", ["Content.DataSourceLoadingGrants"] = "正在加载授权…",
        ["Content.DataSourceEmptyGrantsTitle"] = "暂无显式授权",
        ["Content.DataSourceEmptyGrantsText"] = "选择用户或角色并授予该数据源访问权限。",
        ["Content.DataSourcePanelGrants"] = "授权清单",
        ["Content.DataSourceColSubjectType"] = "主体类型", ["Content.DataSourceColSubject"] = "主体",
        ["Content.DataSourceColCreated"] = "授权时间", ["Content.DataSourceColActions"] = "操作", ["Content.DataSourceRevoke"] = "撤销",
        ["Content.DataSourceTabRls"] = "行级安全", ["Content.DataSourceLoadingPolicies"] = "正在加载策略…",
        ["Content.DataSourcePanelRls"] = "行级安全策略",
        ["Content.DataSourceEmptyRlsTitle"] = "未配置行级安全策略",
        ["Content.DataSourceEmptyRlsText"] = "配置后可按用户/角色限制可见数据行，控制越权取数风险。",
        ["Content.DataSourcePanelPolicies"] = "策略清单",
        ["Content.DataSourceLoadMetaFailed"] = "加载元数据失败（{0}）。",
        ["Content.DataSourceScanFailed"] = "元数据扫描失败。", ["Content.DataSourceScanDone"] = "元数据扫描完成。",
        ["Content.DataSourceSelectSubject"] = "请选择用户或角色。",
        ["Content.DataSourceGrantFailed"] = "授权失败。", ["Content.DataSourceGranted"] = "数据源访问权限已授予。",
        ["Content.DataSourceRevokeFailed"] = "撤销失败。", ["Content.DataSourceRevoked"] = "授权已撤销。",
        ["Content.DataSourceLoadPolicyFailed"] = "加载策略失败（{0}）。",
        ["Content.DataSourcePolicyDeleted"] = "策略已删除。", ["Content.DataSourceDeleteFailed"] = "删除失败（{0}）。",
        ["Content.DataSourceLoadGrantsFailed"] = "加载授权失败（{0}）。",

        // M3-05 重内容页正文批次（续）：ModelAccounts / SemanticLabel / ComponentGallery / ThemeEditor 补充 / MetadataEntity
        ["Content.ModelAccountsBound"] = "已绑定", ["Content.ModelAccountsUnbound"] = "未绑定",
        ["Content.ModelAccountsDefault"] = "默认", ["Content.ModelAccountsSetDefault"] = "设为默认",
        ["Content.ModelAccountsBindPanel"] = "绑定自有 API Key", ["Content.ModelAccountsFieldModel"] = "模型",
        ["Content.ModelAccountsFieldApiKey"] = "API Key", ["Content.ModelAccountsApiKeyPlaceholder"] = "sk-••••••",
        ["Content.ModelAccountsFieldNote"] = "备注", ["Content.ModelAccountsNotePlaceholder"] = "如：生产环境专用",
        ["Content.ModelAccountsSaveBind"] = "保存绑定", ["Content.ModelAccountsSetDefaultToast"] = "已将 {0} 设为默认模型",
        ["Content.ModelAccountsBindSubmitted"] = "绑定已提交（ILLMProvider + UserModelBinding 计划于 P13 实现，密钥将以掩码存储）。",

        ["Content.SemanticLabelLoading"] = "正在加载标签详情…",
        ["Content.SemanticLabelPanelDetail"] = "详情",
        ["Content.SemanticLabelNotFoundTitle"] = "未找到该标签",
        ["Content.SemanticLabelNotFoundText"] = "标签可能已被删除，或后端详情端点尚未接入。",
        ["Content.SemanticLabelInfoPanel"] = "标签信息",
        ["Content.SemanticLabelBack"] = "返回列表",

        ["Content.ComponentGallerySecButtons"] = "按钮", ["Content.ComponentGallerySecBadges"] = "徽标",
        ["Content.ComponentGallerySecForms"] = "表单控件", ["Content.ComponentGallerySecProgress"] = "进度条",
        ["Content.ComponentGallerySecSegmented"] = "分段控件", ["Content.ComponentGallerySecAlerts"] = "告警",
        ["Content.ComponentGallerySecTabs"] = "标签页 SbTabs", ["Content.ComponentGallerySecDataTable"] = "数据表格 SbDataTable",
        ["Content.ComponentGallerySecModal"] = "弹窗 SbModal / SbConfirm", ["Content.ComponentGallerySecToast"] = "轻提示 Toast",
        ["Content.ComponentGallerySecGuard"] = "守卫 Guard",
        ["Content.ComponentGalleryBtnPrimary"] = "主要", ["Content.ComponentGalleryBtnSecondary"] = "次要",
        ["Content.ComponentGalleryBtnGhost"] = "幽灵", ["Content.ComponentGalleryBtnDanger"] = "危险", ["Content.ComponentGalleryBtnDisabled"] = "禁用",
        ["Content.ComponentGalleryBadgePrimary"] = "主要", ["Content.ComponentGalleryBadgePublished"] = "已发布",
        ["Content.ComponentGalleryBadgeDraft"] = "草稿", ["Content.ComponentGalleryBadgeFailed"] = "失败",
        ["Content.ComponentGalleryBadgeGlobal"] = "全局", ["Content.ComponentGalleryBadgeDefault"] = "默认",
        ["Content.ComponentGalleryFormTextbox"] = "文本框", ["Content.ComponentGalleryFormDropdown"] = "下拉",
        ["Content.ComponentGalleryOptA"] = "选项 A", ["Content.ComponentGalleryOptB"] = "选项 B",
        ["Content.ComponentGalleryProgressUsed"] = "已用 {0}",
        ["Content.ComponentGallerySegDay"] = "日", ["Content.ComponentGallerySegWeek"] = "周", ["Content.ComponentGallerySegMonth"] = "月",
        ["Content.ComponentGalleryAlertInfo"] = "信息提示", ["Content.ComponentGalleryAlertWarning"] = "注意提示",
        ["Content.ComponentGalleryStatQuestions"] = "今日问数", ["Content.ComponentGalleryStatHitRate"] = "命中率",
        ["Content.ComponentGalleryStatPending"] = "待复核", ["Content.ComponentGalleryStatActiveTenants"] = "活跃租户",
        ["Content.ComponentGallerySampleTable"] = "示例表格",
        ["Content.ComponentGalleryColCode"] = "编码", ["Content.ComponentGalleryColName"] = "名称",
        ["Content.ComponentGalleryColStatus"] = "状态", ["Content.ComponentGalleryColTenant"] = "租户",
        ["Content.ComponentGalleryCardAskTitle"] = "Ask BI", ["Content.ComponentGalleryCardAskDesc"] = "自然语言问数的旗舰入口。",
        ["Content.ComponentGalleryCardDashTitle"] = "仪表盘", ["Content.ComponentGalleryCardDashDesc"] = "固化可复用的可视化看板。",
        ["Content.ComponentGalleryCardAppTitle"] = "应用工厂", ["Content.ComponentGalleryCardAppDesc"] = "把会话沉淀为 BI 应用。",
        ["Content.ComponentGalleryTabOverview"] = "概览", ["Content.ComponentGalleryTabDetail"] = "明细", ["Content.ComponentGalleryTabSettings"] = "设置",
        ["Content.ComponentGalleryDataEmpty"] = "无数据",
        ["Content.ComponentGalleryModalTitle"] = "示例弹窗", ["Content.ComponentGalleryOpenModal"] = "打开弹窗", ["Content.ComponentGalleryModalBody"] = "弹窗用于承载表单或详情，支持遮罩点击关闭与自定义底部操作。",
        ["Content.ComponentGalleryModalOk"] = "知道了",
        ["Content.ComponentGalleryConfirmTitle"] = "删除确认", ["Content.ComponentGalleryConfirmMsg"] = "此操作不可撤销，确定要删除该记录吗？",
        ["Content.ComponentGalleryConfirmText"] = "删除",
        ["Content.ComponentGalleryToastInfo"] = "信息提示", ["Content.ComponentGalleryToastSuccess"] = "操作成功",
        ["Content.ComponentGalleryToastWarning"] = "请注意", ["Content.ComponentGalleryToastError"] = "出错了",
        ["Content.ComponentGalleryToastDeleted"] = "已确认删除（演示）。",
        ["Content.ComponentGalleryGuardDesc"] = "AuthGuard 等待会话自举完成后渲染子内容；PermissionGuard 按权限码门控（当前未强制，见 NavMenuItems.EnforcePermissions）。",
        ["Content.ComponentGalleryGuardHasPerm"] = "你拥有 demo:write 权限",

        ["Content.ThemeEditorSampleMetric"] = "示例指标", ["Content.ThemeEditorBtnPrimary"] = "主要按钮",
        ["Content.ThemeEditorBtnSecondary"] = "次要", ["Content.ThemeEditorBadgePublished"] = "已发布",
        ["Content.ThemeEditorBadgeDraft"] = "草稿", ["Content.ThemeEditorSavedToast"] = "主题已保存（持久化接入 api/themes，P11.3 收口）。",

        ["Content.MetadataEntityLoading"] = "正在读取元数据实体…",
        ["Content.MetadataEntityIntro"] = "以下内容来自当前租户的实际元数据记录。",
        ["Content.MetadataEntityNotFound"] = "未找到对应数据（{0}）。",
        // M3-05 递延项（Task #83）：分析页正文键 zh-CN 基线
        ["Content.AskTitle"] = "Ask BI 智能问数（旗舰）",
        ["Content.AskRestoringSession"] = "正在恢复登录会话…",
        ["Content.AskLoginHint"] = "请先",
        ["Content.AskLoginLink"] = "登录",
        ["Content.AskLoginHint2"] = "后再问数。",
        ["Content.AskDataScope"] = "可访问数据范围",
        ["Content.AskDataLoading"] = "加载中…",
        ["Content.AskQuestionLabel"] = "自然语言问题",
        ["Content.AskQuestionPlaceholder"] = "例如：上个月各区域销售额对比",
        ["Content.AskButtonAsk"] = "提问",
        ["Content.AskButtonClear"] = "清空对话",
        ["Content.AskHistoryRestored"] = "已恢复 {0} 轮历史",
        ["Content.AskNoConversation"] = "还没有对话。试着问一个业务问题，例如「本月各品类销售额 Top 10」。",
        ["Content.AskDataSourceListEmpty"] = "数据源列表为空",
        ["Content.AskNoDataSources"] = "当前账号暂无可用数据源",
        ["Content.AskDataSourceLoadFailed"] = "数据源加载失败：{0}",
        ["Content.AskEnterQuestion"] = "请输入问题。",
        ["Content.AskNoAuthorizedDataSource"] = "当前账号尚未获授权任何数据源，请联系租户管理员。",
        ["Content.AskEnterRefineInstruction"] = "请输入细化指令。",
        ["Content.AskRefineBasedOnResult"] = "已基于本轮结果重新查询（多轮语义调整）。",
        ["Content.AskRefineException"] = "细化异常：{0}",
        ["Content.AskFailed"] = "问数失败。",
        ["Content.AskNoValidResult"] = "问数未返回有效结果。",
        ["Content.AskPublishedAppNamePrefix"] = "问数应用：",
        ["Content.AskChartTitleDefault"] = "结果图",
        ["Content.AskTableTitleDefault"] = "明细数据",
        ["Content.AskAiSummaryTitle"] = "AI 解读",
        ["Content.AskPublishSuccess"] = "已发布为应用：{0}",
        ["Content.AskPublishFailed"] = "发布失败：{0}",
        ["Content.AskPublishException"] = "发布异常：{0}",
        ["Content.DashboardsTitle"] = "仪表盘",
        ["Content.DashboardsDesc"] = "将 Ask 分析结果固化为可复用的可视化看板，支持团队共享与定时刷新。",
        ["Content.DashboardsSearchPlaceholder"] = "搜索名称 / 编码…",
        ["Content.DashboardsEmptyTitle"] = "暂无仪表盘",
        ["Content.DashboardsEmptyText"] = "将一次问数结果保存为仪表盘，即可在团队内复用这套分析视角与图表。",
        ["Content.DashboardsLoading"] = "正在加载仪表盘…",
        ["Content.DashboardsNew"] = "新建仪表盘",
        ["Content.DashboardsStatTotal"] = "仪表盘总数",
        ["Content.DashboardsStatTotalSub"] = "含全局模板",
        ["Content.DashboardsStatPublished"] = "已发布",
        ["Content.DashboardsStatPublishedSub"] = "可对外共享",
        ["Content.DashboardsStatDraft"] = "草稿",
        ["Content.DashboardsStatDraftSub"] = "编辑中",
        ["Content.DashboardsPublishFromAsk"] = "从 Ask 发布",
        ["Content.DashboardsRowDetail"] = "详情",
        ["Content.DashboardsRowDelete"] = "删除",
        ["Content.DashboardsDeleteConfirmMsg"] = "删除后不可恢复，确认要删除该仪表盘吗？",
        ["Content.DashboardsDeleteConfirmText"] = "删除",
        ["Content.DashboardsFieldStatus"] = "状态",
        ["Content.DashboardsStatusDraft"] = "草稿",
        ["Content.DashboardsStatusPublished"] = "已发布",
        ["Content.DashboardsFieldDslJson"] = "DslJson",
        ["Content.DashboardsFieldDslJsonHint"] = "声明式仪表盘定义（DashboardDsl JSON）。保存将校验后创建。",
        ["Content.DashboardsModalCancel"] = "取消",
        ["Content.DashboardsModalCreate"] = "创建",
        ["Content.DashboardsMissingIdOpen"] = "该记录缺少 id，无法打开详情。",
        ["Content.DashboardsMissingIdDelete"] = "该记录缺少 id，无法删除。",
        ["Content.DashboardsDeleteSuccess"] = "仪表盘已删除。",
        ["Content.DashboardsDeleteFailed"] = "删除失败。",
        ["Content.DashboardsBlueprintUnavailable"] = "未能获取蓝图骨架，请手动填写 DslJson。",
        ["Content.DashboardsDslJsonEmpty"] = "DslJson 不能为空。",
        ["Content.DashboardsCreateSuccess"] = "仪表盘已创建。",
        ["Content.DashboardsCreateFailed"] = "创建失败（HTTP {0}）。",
        ["Content.DashboardsLoadFailed"] = "加载失败。",
        ["Content.AppsTitle"] = "应用工厂",
        ["Content.AppsDesc"] = "将问数会话沉淀为可复用的 BI 应用：可视化编辑器、DSL 蓝图与一键发布。",
        ["Content.AppsSearchPlaceholder"] = "搜索应用编码 / 名称…",
        ["Content.AppsEmptyTitle"] = "还没有应用",
        ["Content.AppsEmptyText"] = "应用是问数能力的封装：把一次典型分析固化为卡片、图表与筛选器，分享给团队即可复用。",
        ["Content.AppsLoading"] = "正在加载应用…",
        ["Content.AppsPanelTitle"] = "应用清单",
        ["Content.AppsNew"] = "新建应用",
        ["Content.AppsFromAsk"] = "从 Ask 发布",
        ["Content.AppsStatTotal"] = "应用总数",
        ["Content.AppsStatTotalSub"] = "含全局模板",
        ["Content.AppsStatGlobal"] = "全局模板",
        ["Content.AppsStatGlobalSub"] = "平台内置",
        ["Content.AppsStatTenant"] = "租户应用",
        ["Content.AppsStatTenantSub"] = "本租户自建",
        ["Content.AppsNoDescription"] = "（暂无描述）",
        ["Content.AppsBadgeGlobal"] = "全局模板",
        ["Content.AppsBadgeTenant"] = "租户",
        ["Content.AppsEdit"] = "编辑",
        ["Content.AppsDelete"] = "删除",
        ["Content.AppsDeleteConfirmTitle"] = "删除应用",
        ["Content.AppsDeleteConfirmMsg"] = "删除后不可恢复，确认要删除该应用吗？",
        ["Content.AppsDeleteConfirmText"] = "删除",
        ["Content.AppsGenDescLabel"] = "应用描述",
        ["Content.AppsGenDescHint"] = "用一句话描述你想要的 BI 应用，AI 将生成 DSL。",
        ["Content.AppsGenDescPlaceholder"] = "例如：销售看板，按地区展示销售额与订单量",
        ["Content.AppsGenCodeLabel"] = "应用编码",
        ["Content.AppsGenCodeHint"] = "留空由系统生成（如 my-app-1733）。",
        ["Content.AppsGenCodePlaceholder"] = "可选",
        ["Content.AppsGenCancel"] = "取消",
        ["Content.AppsGenSubmit"] = "生成并创建",
        ["Content.AppsDslIntro"] = "应用 {0} 的声明式定义（AppDsl JSON）。保存将重新校验并覆盖。",
        ["Content.AppsDslGlobalWarning"] = "该应用为全局模板，不可修改。",
        ["Content.AppsDslFieldLabel"] = "DslJson",
        ["Content.AppsDslSave"] = "保存",
        ["Content.AppsMissingCodeOpen"] = "该记录缺少 code，无法打开详情。",
        ["Content.AppsMissingCodeEdit"] = "该记录缺少 code，无法编辑。",
        ["Content.AppsMissingCodeDelete"] = "该记录缺少 code，无法删除。",
        ["Content.AppsGenDescRequired"] = "应用描述必填。",
        ["Content.AppsGenSuccess"] = "应用已通过 AI 生成并创建。",
        ["Content.AppsGenFailed"] = "生成失败（HTTP {0}）。",
        ["Content.AppsReadFailed"] = "读取应用失败。",
        ["Content.AppsDslJsonEmpty"] = "DslJson 不能为空。",
        ["Content.AppsDslSaved"] = "应用 DSL 已保存。",
        ["Content.AppsDslSaveFailed"] = "保存失败（HTTP {0}）。",
        ["Content.AppsDeleteSuccess"] = "应用已删除。",
        ["Content.AppsDeleteFailed"] = "删除失败。",
        ["Content.AppsLoadFailed"] = "加载失败。",
        ["Content.AppDetailDesc"] = "应用详情：DSL 结构、页面与组件清单。",
        ["Content.AppDetailBack"] = "返回列表",
        ["Content.AppDetailRefresh"] = "刷新",
        ["Content.AppDetailDelete"] = "删除",
        ["Content.AppDetailLoading"] = "正在加载应用…",
        ["Content.AppDetailTabOverview"] = "概览",
        ["Content.AppDetailPanelBasic"] = "基本信息",
        ["Content.AppDetailTabComponents"] = "页面与组件",
        ["Content.AppDetailPanelComponents"] = "组件清单",
        ["Content.AppDetailEmptyComponentsTitle"] = "未解析到组件",
        ["Content.AppDetailEmptyComponentsText"] = "该应用的 DSL 中暂无可解析的组件定义。",
        ["Content.AppDetailTabDsl"] = "DSL",
        ["Content.AppDetailPanelDsl"] = "App DSL（只读）",
        ["Content.AppDetailDeleteConfirmTitle"] = "删除应用",
        ["Content.AppDetailDeleteConfirmMsg"] = "确认删除应用「{0}」？该操作不可撤销。",
        ["Content.AppDetailDeleteConfirmText"] = "删除",
        ["Content.AppDetailTitleDefault"] = "应用详情",
        ["Content.AppDetailDeleteSuccess"] = "应用已删除。",
        ["Content.AppDetailDeleteFailed"] = "删除失败（HTTP {0}）。",
        ["Content.AppDetailLoadFailed"] = "加载失败（{0}）。",
        ["Content.AskTurnMe"] = "我",
        ["Content.AskTurnAiSummary"] = "AI 解读",
        ["Content.AskTurnMeta"] = "耗时 {0} ms · 建议图表 {1} 个",
        ["Content.AskTurnChartDefault"] = "图表 {0}",
        ["Content.AskTurnViewLabel"] = "视图：",
        ["Content.AskTurnBar"] = "柱状",
        ["Content.AskTurnLine"] = "折线",
        ["Content.AskTurnPie"] = "饼图",
        ["Content.AskTurnHideLegend"] = "隐藏图例",
        ["Content.AskTurnShowLegend"] = "显示图例",
        ["Content.AskTurnCyclePalette"] = "换配色",
        ["Content.AskTurnStacked"] = "堆叠",
        ["Content.AskTurnUnstacked"] = "取消堆叠",
        ["Content.AskTurnArea"] = "面积",
        ["Content.AskTurnUnarea"] = "取消面积",
        ["Content.AskTurnMultiAxis"] = "多轴",
        ["Content.AskTurnUnmulti"] = "取消多轴",
        ["Content.AskTurnRowsTotal"] = "共 {0} 行",
        ["Content.AskTurnDrill"] = "下钻：",
        ["Content.AskTurnClearDrill"] = "清除下钻",
        ["Content.AskTurnExportCsv"] = "导出 CSV",
        ["Content.AskTurnExportExcel"] = "导出 Excel",
        ["Content.AskTurnQueryFailed"] = "查询失败：{0}",
        ["Content.AskTurnViewSql"] = "查看生成的 SQL",
        ["Content.AskTurnHideCompare"] = "隐藏对比",
        ["Content.AskTurnShowCompare"] = "对比原结果",
        ["Content.AskTurnOriginalResult"] = "原结果 · {0}",
        ["Content.AskTurnRefinedResult"] = "细化结果 · {0}",
        ["Content.AskTurnRefinePlaceholder"] = "例如：改成柱状图 / 隐藏图例 / 配色换成绿色",
        ["Content.AskTurnApplyAdjust"] = "应用调整",
        ["Content.AskTurnSemanticPlaceholder"] = "基于本轮结果继续追问，如：只看华东地区 / 改成按月统计",
        ["Content.AskTurnSemanticRefine"] = "语义细化",
        ["Content.AskTurnRefining"] = "查询中…",
        ["Content.AskTurnPublish"] = "发布为应用",
        ["Content.AskTurnPublishing"] = "发布中…",
        ["Content.AskTurnRefineNoCmd"] = "请输入调整指令。",
        ["Content.AskTurnRefineNoViz"] = "当前轮无可视化结果。",
        ["Content.AskTurnRefineUnrecognized"] = "未能识别指令（支持：改成柱状/折线/饼图、显示/隐藏图例、配色换绿/橙/红/灰/蓝）。",
        ["Content.AskTurnRefineApplied"] = "已应用视图调整（纯前端，未发起后端请求）。",
    };
}
