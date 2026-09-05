namespace SuperBuilder_AI.Components.Localization;

/// <summary>
/// 前端资源键常量（RCL 内部副本）。
/// <para>
/// 与后端 <c>SuperBuilder_AI.Models.Localization.ResourceKeys</c> 的<b>字符串值保持一一对应</b>，
/// 但为避免 RCL 引用 EF 重型后端程序集，这里在 RCL 内自持一份轻量常量。
/// 键命名规范沿用后端：<c>命名空间.语义</c>（如 <c>Login.Username</c>）。
/// </para>
/// <para>
/// 取值统一走 <c>LocalizationService.T(Keys.X, fallback)</c>；运行时由 <c>/api/localization/texts</c>
/// 覆盖，离线时回退到 <see cref="Defaults"/>（zh-CN/en-US），再回退到页面传入的 fallback。
/// </para>
/// </summary>
public static class Keys
{
    /// <summary>通用操作与状态文本（登录/保存/设置/退出等）。</summary>
    public static class Common
    {
        public const string Login = "Common.Login";
        public const string Confirm = "Common.Confirm";
        public const string Cancel = "Common.Cancel";
        public const string Save = "Common.Save";
        public const string Close = "Common.Close";
        public const string Settings = "Common.Settings";
        public const string Logout = "Common.Logout";
        public const string Loading = "Common.Loading";
        public const string Menu = "Common.Menu";
        public const string Retry = "Common.Retry";
        public const string BackToWorkspace = "Common.BackToWorkspace";
        public const string CurrentUser = "Common.CurrentUser";
        public const string Tenant = "Common.Tenant";
        public const string User = "Common.User";
        public const string AccountMenu = "Common.AccountMenu";
    }

    /// <summary>登录页相关文本。</summary>
    public static class Login
    {
        public const string Title = "Login.Title";
        public const string Username = "Login.Username";
        public const string Password = "Login.Password";
        public const string TenantId = "Login.TenantId";
        public const string Tagline = "Login.Tagline";
        public const string InitEntryClosed = "Login.InitEntryClosed";
        public const string InitEntryClosedHint = "Login.InitEntryClosedHint";
        public const string InitPlatformAdmin = "Login.InitPlatformAdmin";
        public const string InitNotice = "Login.InitNotice";
        public const string AdminUsername = "Login.AdminUsername";
        public const string DisplayName = "Login.DisplayName";
        public const string AdminEmail = "Login.AdminEmail";
        public const string AdminPassword = "Login.AdminPassword";
        public const string ConfirmPassword = "Login.ConfirmPassword";
        public const string CreateAdmin = "Login.CreateAdmin";
        public const string TenantPlaceholder = "Login.TenantPlaceholder";
        public const string NoTenantRegister = "Login.NoTenantRegister";
        public const string CheckingInit = "Login.CheckingInit";
        public const string InitStatusError = "Login.InitStatusError";
        public const string AdminUsernameRequired = "Login.AdminUsernameRequired";
        public const string AdminEmailInvalid = "Login.AdminEmailInvalid";
        public const string AdminPasswordTooShort = "Login.AdminPasswordTooShort";
        public const string AdminPasswordMismatch = "Login.AdminPasswordMismatch";
        public const string InitFailed = "Login.InitFailed";
        public const string SelectTenantFirst = "Login.SelectTenantFirst";
        public const string LoginFailed = "Login.LoginFailed";
        public const string HeroAINativeBI = "Login.Hero.AINativeBI";
        public const string HeroMultiTenant = "Login.Hero.MultiTenant";
        public const string HeroMultilingual = "Login.Hero.Multilingual";
        public const string HeroLowCode = "Login.Hero.LowCode";
        public const string HeroEnterpriseSaaS = "Login.Hero.EnterpriseSaaS";
    }

    /// <summary>应用级标题/副标题。</summary>
    public static class App
    {
        public const string Subtitle = "App.Subtitle";
    }

    /// <summary>业务文档类型文本。</summary>
    public static class Document
    {
        public const string OutboundOrder = "Document.OutboundOrder";
    }

    /// <summary>表单验证消息。</summary>
    public static class Validation
    {
        public const string Required = "Validation.Required";
        public const string Email = "Validation.Email";
        public const string Format = "Validation.Format";
    }

    /// <summary>通用错误提示。</summary>
    public static class Error
    {
        public const string Generic = "Error.Generic";
        public const string NotFound = "Error.NotFound";
        public const string Unauthorized = "Error.Unauthorized";
        public const string Forbidden = "Error.Forbidden";
        public const string Validation = "Error.Validation";
        public const string Conflict = "Error.Conflict";
        public const string RateLimited = "Error.RateLimited";
        public const string Maintenance = "Error.Maintenance";
        public const string PageRender = "Error.PageRender";
        public const string PageRenderDesc = "Error.PageRenderDesc";
        public const string TechDetails = "Error.TechDetails";
        public const string LocalizationCultureInvalid = "Error.Localization.CultureInvalid";
        public const string LocalizationTextEmpty = "Error.Localization.TextEmpty";
        public const string LocalizationPlaceholderMismatch = "Error.Localization.PlaceholderMismatch";
        public const string LocalizationBaselineReset = "Error.Localization.BaselineReset";
        public const string OperationFailed = "Error.OperationFailed";
        public const string CodeLabel = "Error.CodeLabel";
        public const string TraceIdLabel = "Error.TraceIdLabel";
    }

    /// <summary>空状态文案。</summary>
    public static class Empty
    {
        public const string NoData = "Empty.NoData";
    }

    /// <summary>导航菜单文本。</summary>
    public static class Nav
    {
        public const string Home = "Nav.Home";
        public const string Ask = "Nav.Ask";
        public const string Dashboards = "Nav.Dashboards";
        public const string Apps = "Nav.Apps";
        public const string Agent = "Nav.Agent";
        public const string SemanticLabels = "Nav.SemanticLabels";
        public const string BusinessModel = "Nav.BusinessModel";
        public const string Components = "Nav.Components";
        public const string ThemeEditor = "Nav.ThemeEditor";
        public const string DataSources = "Nav.DataSources";
        // 历史别名：早期种子/菜单沿用，保留以与后端 ResourceKeys 保持键集合一致（勿删，删则镜像漂移）。
        public const string Dashboard = "Nav.Dashboard";
        public const string Admin = "Nav.Admin";
        public const string ModelAccounts = "Nav.ModelAccounts";
        public const string Tenants = "Nav.Tenants";
        public const string TenantMembers = "Nav.TenantMembers";
        public const string SelfRegistration = "Nav.SelfRegistration";
        public const string DemoData = "Nav.DemoData";
        public const string PlatformAdmins = "Nav.PlatformAdmins";
        public const string PlatformAdminScopes = "Nav.PlatformAdminScopes";
        public const string Identity = "Nav.Identity";
        public const string Audit = "Nav.Audit";
        public const string Quota = "Nav.Quota";
        public const string Localization = "Nav.Localization";
        public const string Themes = "Nav.Themes";
        public const string System = "Nav.System";
        public const string GroupFlagship = "Nav.Group.Flagship";
        public const string GroupAnalysis = "Nav.Group.Analysis";
        public const string GroupCustom = "Nav.Group.Custom";
        public const string GroupPlatformExt = "Nav.Group.PlatformExt";
        public const string GroupAdmin = "Nav.Group.Admin";
    }

    /// <summary>页面页头标题（M3-05 批2），与后端 ResourceKeys.Page 一一对应。</summary>
    public static class Page
    {
        public const string TitleHome = "Page.Title.Home";
        public const string TitleProfile = "Page.Title.Profile";
        public const string TitleTenants = "Page.Title.Tenants";
        public const string TitleTenantMembers = "Page.Title.TenantMembers";
        public const string TitleIdentity = "Page.Title.Identity";
        public const string TitleQuota = "Page.Title.Quota";
        public const string TitlePlatformAdmins = "Page.Title.PlatformAdmins";
        public const string TitlePlatformAdminScopes = "Page.Title.PlatformAdminScopes";
        public const string TitleSelfRegistrationAdmin = "Page.Title.SelfRegistrationAdmin";
        public const string TitleSystemStatus = "Page.Title.SystemStatus";
        public const string TitleLocalization = "Page.Title.Localization";
        public const string TitleThemes = "Page.Title.Themes";
        public const string TitleDemoData = "Page.Title.DemoData";
    }

    /// <summary>无障碍文本。</summary>
    public static class Accessibility
    {
        public const string SkipToContent = "Accessibility.SkipToContent";
    }

    /// <summary>主题切换文本。</summary>
    public static class Theme
    {
        public const string Light = "Theme.Light";
        public const string Dark = "Theme.Dark";
        public const string Switch = "Theme.Switch";
    }

    /// <summary>离线回退默认值：zh-CN / en-US。运行时由 API 覆盖；离线且未命中 API 时回退到此。</summary>
    public sealed record L10nDefault(string ZhCn, string EnUs);

    public static IReadOnlyDictionary<string, L10nDefault> Defaults { get; } = new Dictionary<string, L10nDefault>
    {
        [Common.Login] = new("登录", "Sign in"),
        [Common.Confirm] = new("确认", "Confirm"),
        [Common.Cancel] = new("取消", "Cancel"),
        [Common.Save] = new("保存", "Save"),
        [Common.Close] = new("关闭", "Close"),
        [Common.Settings] = new("个人设置", "Settings"),
        [Common.Logout] = new("退出登录", "Sign out"),
        [Common.Loading] = new("加载中…", "Loading…"),
        [Common.Menu] = new("菜单", "Menu"),
        [Common.Retry] = new("重试", "Retry"),
        [Common.BackToWorkspace] = new("返回工作台", "Back to workspace"),
        [Common.CurrentUser] = new("当前用户", "Current user"),
        [Common.Tenant] = new("租户", "Tenant"),
        [Common.User] = new("用户", "User"),
        [Common.AccountMenu] = new("账号菜单", "Account menu"),

        [Login.Title] = new("登录 / 租户选择", "Sign in / Select tenant"),
        [Login.Username] = new("用户名", "Username"),
        [Login.Password] = new("口令", "Password"),
        [Login.TenantId] = new("租户 ID", "Tenant ID"),
        [Login.Tagline] = new("自然语言驱动的智能问数平台 —— 对话即分析，所见即洞察。", "Natural-language analytics — ask, analyze, and discover."),
        [Login.InitEntryClosed] = new("初始化入口已关闭", "Initialization entry is closed"),
        [Login.InitEntryClosedHint] = new("当前环境已禁用匿名初始化。请由部署配置完成首位平台管理员创建。", "Anonymous initialization is disabled in this environment. Create the first platform admin via deployment configuration."),
        [Login.InitPlatformAdmin] = new("初始化平台系统管理员", "Initialize platform administrator"),
        [Login.InitNotice] = new("这是一次性初始化入口。创建完成后将永久关闭，请妥善保管管理员口令。", "This is a one-time initialization. It will be permanently closed after creation; keep the admin password safe."),
        [Login.AdminUsername] = new("管理员用户名", "Admin username"),
        [Login.DisplayName] = new("显示名", "Display name"),
        [Login.AdminEmail] = new("管理员邮箱", "Admin email"),
        [Login.AdminPassword] = new("管理员口令", "Admin password"),
        [Login.ConfirmPassword] = new("确认口令", "Confirm password"),
        [Login.CreateAdmin] = new("创建平台管理员", "Create platform admin"),
        [Login.TenantPlaceholder] = new("-- 请选择租户 --", "-- Select tenant --"),
        [Login.NoTenantRegister] = new("还没有租户？申请开通", "No tenant yet? Request access"),
        [Login.CheckingInit] = new("正在检查平台初始化状态…", "Checking platform initialization status…"),
        [Login.InitStatusError] = new("无法读取平台初始化状态。", "Unable to read platform initialization status."),
        [Login.AdminUsernameRequired] = new("管理员用户名必填。", "Admin username is required."),
        [Login.AdminEmailInvalid] = new("请输入有效的管理员邮箱。", "Please enter a valid admin email."),
        [Login.AdminPasswordTooShort] = new("管理员口令至少 8 位。", "Admin password must be at least 8 characters."),
        [Login.AdminPasswordMismatch] = new("两次输入的口令不一致。", "The two passwords do not match."),
        [Login.InitFailed] = new("平台管理员初始化失败。", "Platform admin initialization failed."),
        [Login.SelectTenantFirst] = new("请先选择租户。", "Please select a tenant first."),
        [Login.LoginFailed] = new("登录失败：用户不存在或已禁用。", "Sign-in failed: user does not exist or is disabled."),
        [Login.HeroAINativeBI] = new("AI Native BI", "AI Native BI"),
        [Login.HeroMultiTenant] = new("多租户", "Multi-tenant"),
        [Login.HeroMultilingual] = new("多语言", "Multilingual"),
        [Login.HeroLowCode] = new("低代码", "Low-code"),
        [Login.HeroEnterpriseSaaS] = new("企业级 SaaS", "Enterprise SaaS"),

        [App.Subtitle] = new("智能问数平台", "AI Analytics Platform"),
        [Document.OutboundOrder] = new("出库单", "Outbound order"),

        [Validation.Required] = new("此项为必填。", "This field is required."),
        [Validation.Email] = new("请输入有效的邮箱地址。", "Please enter a valid email address."),
        [Validation.Format] = new("格式不正确。", "Invalid format."),

        [Error.Generic] = new("发生错误，请稍后重试。", "An error occurred. Please try again later."),
        [Error.NotFound] = new("未找到请求的资源。", "The requested resource was not found."),
        [Error.Unauthorized] = new("未授权。", "Unauthorized."),
        [Error.Forbidden] = new("无权访问该资源。", "You do not have access to this resource."),
        [Error.Validation] = new("输入校验未通过。", "Input validation failed."),
        [Error.Conflict] = new("操作冲突，请刷新后重试。", "Conflict detected. Please refresh and retry."),
        [Error.RateLimited] = new("请求过于频繁，请稍后再试。", "Too many requests. Please try again later."),
        [Error.Maintenance] = new("系统维护中，请稍后访问。", "Under maintenance. Please try again later."),
        [Error.PageRender] = new("页面渲染出错", "Page failed to render"),
        [Error.PageRenderDesc] = new("该页面在渲染时发生异常，已被安全隔离，未影响其它功能。可重试当前页面或返回工作台。", "The page encountered a rendering error and was safely isolated without affecting other features. Retry the page or return to the workspace."),
        [Error.TechDetails] = new("技术详情", "Technical details"),
        [Error.LocalizationCultureInvalid] = new("文化格式无效。", "Invalid culture format."),
        [Error.LocalizationTextEmpty] = new("文本不能为空。", "Text cannot be empty."),
        [Error.LocalizationPlaceholderMismatch] = new("译文占位符与平台基线不一致。", "Translation placeholders do not match the platform baseline."),
        [Error.LocalizationBaselineReset] = new("平台基线不能使用重置覆盖操作。", "The platform baseline cannot be reset via override."),
        [Error.OperationFailed] = new("操作失败。", "Operation failed."),
        [Error.CodeLabel] = new("错误码：", "Error code: "),
        [Error.TraceIdLabel] = new("追踪 ID：", "Trace ID: "),

        [Empty.NoData] = new("暂无数据。", "No data available."),

        [Nav.Home] = new("首页", "Home"),
        [Nav.Ask] = new("Ask BI 智能问数", "Ask BI"),
        [Nav.Dashboards] = new("仪表盘", "Dashboards"),
        [Nav.Apps] = new("应用工厂", "App Factory"),
        [Nav.Agent] = new("智能体 / Copilot", "Agent / Copilot"),
        [Nav.SemanticLabels] = new("语义标签", "Semantic Labels"),
        [Nav.BusinessModel] = new("语义模型", "Semantic Model"),
        [Nav.Components] = new("组件库", "Component Library"),
        [Nav.ThemeEditor] = new("主题编辑器", "Theme Editor"),
        [Nav.DataSources] = new("数据源", "Data Sources"),
        [Nav.Dashboard] = new("仪表盘", "Dashboard"),
        [Nav.Admin] = new("管理", "Admin"),
        [Nav.ModelAccounts] = new("模型与账号", "Models & Accounts"),
        [Nav.Tenants] = new("租户", "Tenants"),
        [Nav.TenantMembers] = new("租户成员", "Tenant Members"),
        [Nav.SelfRegistration] = new("自助注册", "Self Registration"),
        [Nav.DemoData] = new("演示数据", "Demo Data"),
        [Nav.PlatformAdmins] = new("平台管理员", "Platform Admins"),
        [Nav.PlatformAdminScopes] = new("管理员租户范围", "Admin Tenant Scope"),
        [Nav.Identity] = new("身份权限", "Identity & Permissions"),
        [Nav.Audit] = new("审计", "Audit"),
        [Nav.Quota] = new("配额", "Quota"),
        [Nav.Localization] = new("多语言", "Localization"),
        [Nav.Themes] = new("主题", "Themes"),
        [Nav.System] = new("系统状态", "System Status"),
        [Nav.GroupFlagship] = new("旗舰", "Flagship"),
        [Nav.GroupAnalysis] = new("分析", "Analysis"),
        [Nav.GroupCustom] = new("自定义", "Custom"),
        [Nav.GroupPlatformExt] = new("平台扩展", "Platform Extensions"),
        [Nav.GroupAdmin] = new("管理后台", "Admin"),

        [Accessibility.SkipToContent] = new("跳到主内容", "Skip to content"),
        [Theme.Light] = new("浅色", "Light"),
        [Theme.Dark] = new("深色", "Dark"),
        [Theme.Switch] = new("切换主题", "Toggle theme"),

        [Page.TitleHome] = new("工作台", "Workspace"),
        [Page.TitleProfile] = new("个人设置", "Profile"),
        [Page.TitleTenants] = new("租户管理", "Tenants"),
        [Page.TitleTenantMembers] = new("租户成员", "Tenant Members"),
        [Page.TitleIdentity] = new("身份与权限", "Identity & Permissions"),
        [Page.TitleQuota] = new("配额管理", "Quota"),
        [Page.TitlePlatformAdmins] = new("平台管理员", "Platform Admins"),
        [Page.TitlePlatformAdminScopes] = new("管理员租户范围", "Admin Tenant Scope"),
        [Page.TitleSelfRegistrationAdmin] = new("自助注册", "Self Registration"),
        [Page.TitleSystemStatus] = new("系统状态", "System Status"),
        [Page.TitleLocalization] = new("多语言中心", "Localization"),
        [Page.TitleThemes] = new("主题（租户级）", "Themes"),
        [Page.TitleDemoData] = new("演示数据", "Demo Data"),
    };
}
