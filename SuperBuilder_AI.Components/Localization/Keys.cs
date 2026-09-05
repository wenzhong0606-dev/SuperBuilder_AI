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

    /// <summary>通用操作动词（M3-05 续批），跨页面复用。</summary>
    public static class Action
    {
        public const string New = "Action.New";
        public const string Create = "Action.Create";
        public const string Save = "Action.Save";
        public const string Cancel = "Action.Cancel";
        public const string Refresh = "Action.Refresh";
        public const string Delete = "Action.Delete";
        public const string Edit = "Action.Edit";
        public const string Search = "Action.Search";
        public const string Close = "Action.Close";
        public const string Reset = "Action.Reset";
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

        // M3-05 续批：递延内容页（Analysis/Design/Platform）
        public const string TitleBusinessModel = "Page.Title.BusinessModel";
        public const string TitleBusinessModelEntityDetail = "Page.Title.BusinessModelEntityDetail";
        public const string TitleSemanticLabelDetail = "Page.Title.SemanticLabelDetail";
        public const string TitleComponentGallery = "Page.Title.ComponentGallery";
        public const string TitleThemeEditor = "Page.Title.ThemeEditor";
        public const string TitleDataSources = "Page.Title.DataSources";
        public const string TitleModelAccounts = "Page.Title.ModelAccounts";
        public const string TitleMetadataEntityDetail = "Page.Title.MetadataEntityDetail";
        public const string TitleDataSource = "Page.Title.DataSource";

        public const string DescBusinessModel = "Page.Desc.BusinessModel";
        public const string DescBusinessModelEntityDetail = "Page.Desc.BusinessModelEntityDetail";
        public const string DescSemanticLabelDetail = "Page.Desc.SemanticLabelDetail";
        public const string DescComponentGallery = "Page.Desc.ComponentGallery";
        public const string DescThemeEditor = "Page.Desc.ThemeEditor";
        public const string DescDataSources = "Page.Desc.DataSources";
        public const string DescModelAccounts = "Page.Desc.ModelAccounts";
        public const string DescMetadataEntityDetail = "Page.Desc.MetadataEntityDetail";
        public const string DescDataSource = "Page.Desc.DataSource";

        // M3-05 续批：ThemeEditor 面板标题
        public const string ThemeEditorPreview = "Page.ThemeEditor.Preview";
        public const string ThemeEditorPalette = "Page.ThemeEditor.Palette";
    }

    /// <summary>
    /// 重型内容页正文资源键镜像（M3-05 重内容页正文批次），与后端 <c>ResourceKeys.Content</c> 字符串值一一对应。
    /// 采用单级扁平常量（键名含页面前缀），与后端保持一致，避免二级嵌套被后端 <c>All()</c> 漏枚举。
    /// </summary>
    public static class Content
    {
        // BusinessModel（语义模型列表）
        public const string BusinessModelDomains = "Content.BusinessModelDomains";
        public const string BusinessModelDomainsSub = "Content.BusinessModelDomainsSub";
        public const string BusinessModelEntities = "Content.BusinessModelEntities";
        public const string BusinessModelEntitiesSub = "Content.BusinessModelEntitiesSub";
        public const string BusinessModelResolved = "Content.BusinessModelResolved";
        public const string BusinessModelResolvedSub = "Content.BusinessModelResolvedSub";
        public const string BusinessModelSearchPlaceholder = "Content.BusinessModelSearchPlaceholder";
        public const string BusinessModelEmptyDomainsTitle = "Content.BusinessModelEmptyDomainsTitle";
        public const string BusinessModelEmptyDomainsText = "Content.BusinessModelEmptyDomainsText";
        public const string BusinessModelEmptyEntitiesTitle = "Content.BusinessModelEmptyEntitiesTitle";
        public const string BusinessModelEmptyEntitiesText = "Content.BusinessModelEmptyEntitiesText";
        public const string BusinessModelNoMatch = "Content.BusinessModelNoMatch";
        public const string BusinessModelDetail = "Content.BusinessModelDetail";
        public const string BusinessModelEditorNotReady = "Content.BusinessModelEditorNotReady";

        // BusinessModelEntityDetail（实体详情）
        public const string BusinessModelEntityLoading = "Content.BusinessModelEntityLoading";
        public const string BusinessModelEntityPanelDetail = "Content.BusinessModelEntityPanelDetail";
        public const string BusinessModelEntityEmptyTitle = "Content.BusinessModelEntityEmptyTitle";
        public const string BusinessModelEntityEmptyText = "Content.BusinessModelEntityEmptyText";
        public const string BusinessModelEntityPanelInfo = "Content.BusinessModelEntityPanelInfo";
        public const string BusinessModelEntityBack = "Content.BusinessModelEntityBack";

        // DataSources（数据源列表）
        public const string DataSourcesNew = "Content.DataSourcesNew";
        public const string DataSourcesMetricTotal = "Content.DataSourcesMetricTotal";
        public const string DataSourcesMetricOnline = "Content.DataSourcesMetricOnline";
        public const string DataSourcesMetricTables = "Content.DataSourcesMetricTables";
        public const string DataSourcesMetricColumns = "Content.DataSourcesMetricColumns";
        public const string DataSourcesPanelAssets = "Content.DataSourcesPanelAssets";
        public const string DataSourcesSearchPlaceholder = "Content.DataSourcesSearchPlaceholder";
        public const string DataSourcesLoading = "Content.DataSourcesLoading";
        public const string DataSourcesEmptyTitle = "Content.DataSourcesEmptyTitle";
        public const string DataSourcesEmptyText = "Content.DataSourcesEmptyText";
        public const string DataSourcesColId = "Content.DataSourcesColId";
        public const string DataSourcesColName = "Content.DataSourcesColName";
        public const string DataSourcesColType = "Content.DataSourcesColType";
        public const string DataSourcesColStatus = "Content.DataSourcesColStatus";
        public const string DataSourcesColTables = "Content.DataSourcesColTables";
        public const string DataSourcesColColumns = "Content.DataSourcesColColumns";
        public const string DataSourcesTenantScoped = "Content.DataSourcesTenantScoped";
        public const string DataSourcesStatusOnline = "Content.DataSourcesStatusOnline";
        public const string DataSourcesStatusOffline = "Content.DataSourcesStatusOffline";
        public const string DataSourcesManage = "Content.DataSourcesManage";
        public const string DataSourcesModalTitle = "Content.DataSourcesModalTitle";
        public const string DataSourcesConnSqlServer = "Content.DataSourcesConnSqlServer";
        public const string DataSourcesConnMysql = "Content.DataSourcesConnMysql";
        public const string DataSourcesConnPostgres = "Content.DataSourcesConnPostgres";
        public const string DataSourcesConnOracle = "Content.DataSourcesConnOracle";
        public const string DataSourcesConnClickhouse = "Content.DataSourcesConnClickhouse";
        public const string DataSourcesConnMongodb = "Content.DataSourcesConnMongodb";
        public const string DataSourcesFieldName = "Content.DataSourcesFieldName";
        public const string DataSourcesFieldNameHint = "Content.DataSourcesFieldNameHint";
        public const string DataSourcesFieldNamePlaceholder = "Content.DataSourcesFieldNamePlaceholder";
        public const string DataSourcesFieldType = "Content.DataSourcesFieldType";
        public const string DataSourcesFieldConnStr = "Content.DataSourcesFieldConnStr";
        public const string DataSourcesFieldConnStrPlaceholder = "Content.DataSourcesFieldConnStrPlaceholder";
        public const string DataSourcesTestConn = "Content.DataSourcesTestConn";
        public const string DataSourcesSaveAndConnect = "Content.DataSourcesSaveAndConnect";
        public const string DataSourcesLoadFailed = "Content.DataSourcesLoadFailed";
        public const string DataSourcesConnectorSelected = "Content.DataSourcesConnectorSelected";
        public const string DataSourcesTestSubmitted = "Content.DataSourcesTestSubmitted";
        public const string DataSourcesRequiredError = "Content.DataSourcesRequiredError";
        public const string DataSourcesSaveFailed = "Content.DataSourcesSaveFailed";
        public const string DataSourcesSavedToast = "Content.DataSourcesSavedToast";
        public const string DataSourcesSaving = "Content.DataSourcesSaving";

        // DataSourceDetail（数据源详情）
        public const string DataSourceBackList = "Content.DataSourceBackList";
        public const string DataSourceTabMeta = "Content.DataSourceTabMeta";
        public const string DataSourceRescan = "Content.DataSourceRescan";
        public const string DataSourceScanning = "Content.DataSourceScanning";
        public const string DataSourceLoadingMeta = "Content.DataSourceLoadingMeta";
        public const string DataSourceEmptyMetaTitle = "Content.DataSourceEmptyMetaTitle";
        public const string DataSourceEmptyMetaText = "Content.DataSourceEmptyMetaText";
        public const string DataSourceFieldsBadge = "Content.DataSourceFieldsBadge";
        public const string DataSourceColFieldRel = "Content.DataSourceColFieldRel";
        public const string DataSourceColType = "Content.DataSourceColType";
        public const string DataSourceColNullable = "Content.DataSourceColNullable";
        public const string DataSourceColSemantic = "Content.DataSourceColSemantic";
        public const string DataSourceColVector = "Content.DataSourceColVector";
        public const string DataSourceColBusinessKey = "Content.DataSourceColBusinessKey";
        public const string DataSourceNotVectorized = "Content.DataSourceNotVectorized";
        public const string DataSourceTabGrants = "Content.DataSourceTabGrants";
        public const string DataSourceGrantUser = "Content.DataSourceGrantUser";
        public const string DataSourceGrantRole = "Content.DataSourceGrantRole";
        public const string DataSourceGrantSubjectPlaceholder = "Content.DataSourceGrantSubjectPlaceholder";
        public const string DataSourceGrantAccess = "Content.DataSourceGrantAccess";
        public const string DataSourceRefreshGrants = "Content.DataSourceRefreshGrants";
        public const string DataSourceLoadingGrants = "Content.DataSourceLoadingGrants";
        public const string DataSourceEmptyGrantsTitle = "Content.DataSourceEmptyGrantsTitle";
        public const string DataSourceEmptyGrantsText = "Content.DataSourceEmptyGrantsText";
        public const string DataSourcePanelGrants = "Content.DataSourcePanelGrants";
        public const string DataSourceColSubjectType = "Content.DataSourceColSubjectType";
        public const string DataSourceColSubject = "Content.DataSourceColSubject";
        public const string DataSourceColCreated = "Content.DataSourceColCreated";
        public const string DataSourceColActions = "Content.DataSourceColActions";
        public const string DataSourceRevoke = "Content.DataSourceRevoke";
        public const string DataSourceTabRls = "Content.DataSourceTabRls";
        public const string DataSourceLoadingPolicies = "Content.DataSourceLoadingPolicies";
        public const string DataSourcePanelRls = "Content.DataSourcePanelRls";
        public const string DataSourceEmptyRlsTitle = "Content.DataSourceEmptyRlsTitle";
        public const string DataSourceEmptyRlsText = "Content.DataSourceEmptyRlsText";
        public const string DataSourcePanelPolicies = "Content.DataSourcePanelPolicies";
        public const string DataSourceLoadMetaFailed = "Content.DataSourceLoadMetaFailed";
        public const string DataSourceScanFailed = "Content.DataSourceScanFailed";
        public const string DataSourceScanDone = "Content.DataSourceScanDone";
        public const string DataSourceSelectSubject = "Content.DataSourceSelectSubject";
        public const string DataSourceGrantFailed = "Content.DataSourceGrantFailed";
        public const string DataSourceGranted = "Content.DataSourceGranted";
        public const string DataSourceRevokeFailed = "Content.DataSourceRevokeFailed";
        public const string DataSourceRevoked = "Content.DataSourceRevoked";
        public const string DataSourceLoadPolicyFailed = "Content.DataSourceLoadPolicyFailed";
        public const string DataSourcePolicyDeleted = "Content.DataSourcePolicyDeleted";
        public const string DataSourceDeleteFailed = "Content.DataSourceDeleteFailed";
        public const string DataSourceLoadGrantsFailed = "Content.DataSourceLoadGrantsFailed";
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

        [Action.New] = new("新建", "New"),
        [Action.Create] = new("创建", "Create"),
        [Action.Save] = new("保存", "Save"),
        [Action.Cancel] = new("取消", "Cancel"),
        [Action.Refresh] = new("刷新", "Refresh"),
        [Action.Delete] = new("删除", "Delete"),
        [Action.Edit] = new("编辑", "Edit"),
        [Action.Search] = new("搜索", "Search"),
        [Action.Close] = new("关闭", "Close"),
        [Action.Reset] = new("重置", "Reset"),

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
        [Page.TitleBusinessModel] = new("语义模型", "Semantic Model"),
        [Page.TitleBusinessModelEntityDetail] = new("实体详情", "Entity Details"),
        [Page.TitleSemanticLabelDetail] = new("语义标签详情", "Semantic Label Details"),
        [Page.TitleComponentGallery] = new("组件库", "Component Gallery"),
        [Page.TitleThemeEditor] = new("主题编辑器", "Theme Editor"),
        [Page.TitleDataSources] = new("数据源管理", "Data Sources"),
        [Page.TitleModelAccounts] = new("模型与账号（BYO）", "Models & Accounts (BYO)"),
        [Page.TitleMetadataEntityDetail] = new("元数据关系详情", "Metadata Relation Details"),
        [Page.TitleDataSource] = new("数据源详情", "Data Source Details"),

        [Page.DescBusinessModel] = new("把物理表结构映射为业务语言：实体、业务域与字段同义词，让自然语言问数更精准。", "Map physical tables into business language: entities, domains, and field synonyms for precise NL querying."),
        [Page.DescBusinessModelEntityDetail] = new("查看实体映射、字段口径与解析状态。", "View entity mapping, field definitions, and resolution status."),
        [Page.DescSemanticLabelDetail] = new("查看标签口径、同义词与召回策略。", "View label definitions, synonyms, and recall strategy."),
        [Page.DescComponentGallery] = new("SuperBuilder 设计系统一览：基于中国古风色板与 Bootstrap 的通用组件，全站统一复用。", "A tour of the SuperBuilder design system: reusable components on a Chinese antique palette and Bootstrap."),
        [Page.DescThemeEditor] = new("基于古风色板自定义你的视觉风格：实时预览、保存并指派给租户或复制为蓝图。", "Customize your visual style on the antique palette: live preview, save and assign to a tenant or copy as a blueprint."),
        [Page.DescDataSources] = new("统一管理租户数据连接、元数据覆盖与访问状态。", "Manage tenant data connections, metadata overrides, and access status."),
        [Page.DescModelAccounts] = new("选择当前 AI 模型，或绑定你自有的模型 API Key（BYO），按租户隔离、密钥掩码存储。", "Choose the current AI model, or bind your own model API key (BYO), tenant-isolated and masked."),
        [Page.DescMetadataEntityDetail] = new("查看元数据实体的实际数据、所属对象及向量索引关系。", "View the actual data, owning objects, and vector index relations of a metadata entity."),
        [Page.DescDataSource] = new("数据源的授权清单与行级安全策略（RLS）。", "The data source's authorization list and row-level security (RLS) policies."),

        [Page.ThemeEditorPreview] = new("实时预览", "Live Preview"),
        [Page.ThemeEditorPalette] = new("调色板", "Palette"),

        // M3-05 重内容页正文批次：Content.* 镜像默认值（ZhCn / EnUs）
        [Content.BusinessModelDomains] = new("业务域", "Domains"),
        [Content.BusinessModelDomainsSub] = new("用于分类", "For categorization"),
        [Content.BusinessModelEntities] = new("实体", "Entities"),
        [Content.BusinessModelEntitiesSub] = new("对应业务表", "Map to business tables"),
        [Content.BusinessModelResolved] = new("已解析语义", "Semantics Resolved"),
        [Content.BusinessModelResolvedSub] = new("可在 Ask 提问", "Queryable in Ask"),
        [Content.BusinessModelSearchPlaceholder] = new("搜索实体…", "Search entities…"),
        [Content.BusinessModelEmptyDomainsTitle] = new("暂无业务域", "No business domains yet"),
        [Content.BusinessModelEmptyDomainsText] = new("业务域用于组织对实体的分类，例如「销售」「库存」「财务」。", "Domains categorize entities, e.g. Sales, Inventory, Finance."),
        [Content.BusinessModelEmptyEntitiesTitle] = new("暂无实体", "No entities yet"),
        [Content.BusinessModelEmptyEntitiesText] = new("实体对应一张业务表（如「客户」「订单」），解析后可在 Ask 中直接以业务名提问。", "An entity maps to a business table (e.g. Customer, Order); once resolved it can be queried by name in Ask."),
        [Content.BusinessModelNoMatch] = new("无匹配实体", "No matching entities"),
        [Content.BusinessModelDetail] = new("详情", "Details"),
        [Content.BusinessModelEditorNotReady] = new("实体编辑器将在 S2 阶段接入。", "The entity editor will be wired in stage S2."),

        [Content.BusinessModelEntityLoading] = new("正在加载实体详情…", "Loading entity details…"),
        [Content.BusinessModelEntityPanelDetail] = new("详情", "Details"),
        [Content.BusinessModelEntityEmptyTitle] = new("未找到该实体", "Entity not found"),
        [Content.BusinessModelEntityEmptyText] = new("实体可能已被删除，或后端详情端点尚未接入。", "The entity may have been deleted, or the backend detail endpoint is not yet connected."),
        [Content.BusinessModelEntityPanelInfo] = new("实体信息", "Entity information"),
        [Content.BusinessModelEntityBack] = new("返回语义模型", "Back to semantic model"),

        [Content.DataSourcesNew] = new("新增数据源", "Add data source"),
        [Content.DataSourcesMetricTotal] = new("数据源总数", "Total data sources"),
        [Content.DataSourcesMetricOnline] = new("在线连接", "Online connections"),
        [Content.DataSourcesMetricTables] = new("元数据表", "Metadata tables"),
        [Content.DataSourcesMetricColumns] = new("已识别字段", "Recognized fields"),
        [Content.DataSourcesPanelAssets] = new("连接资产", "Connections"),
        [Content.DataSourcesSearchPlaceholder] = new("搜索名称或数据库类型…", "Search by name or engine type…"),
        [Content.DataSourcesLoading] = new("正在加载数据源…", "Loading data sources…"),
        [Content.DataSourcesEmptyTitle] = new("暂无数据源", "No data sources yet"),
        [Content.DataSourcesEmptyText] = new("请在下方创建第一个数据源。", "Create your first data source below."),
        [Content.DataSourcesColId] = new("ID", "ID"),
        [Content.DataSourcesColName] = new("名称", "Name"),
        [Content.DataSourcesColType] = new("类型", "Type"),
        [Content.DataSourcesColStatus] = new("状态", "Status"),
        [Content.DataSourcesColTables] = new("元数据表", "Tables"),
        [Content.DataSourcesColColumns] = new("字段", "Fields"),
        [Content.DataSourcesTenantScoped] = new("租户专属连接", "Tenant-scoped connection"),
        [Content.DataSourcesStatusOnline] = new("运行中", "Running"),
        [Content.DataSourcesStatusOffline] = new("已停用", "Disabled"),
        [Content.DataSourcesManage] = new("管理连接 →", "Manage connection →"),
        [Content.DataSourcesModalTitle] = new("接入新的数据源", "Connect a new data source"),
        [Content.DataSourcesConnSqlServer] = new("企业主流关系型数据库", "Mainstream enterprise RDBMS"),
        [Content.DataSourcesConnMysql] = new("WMS 等业务常用", "Common for WMS and similar"),
        [Content.DataSourcesConnPostgres] = new("开源关系型数据库", "Open-source RDBMS"),
        [Content.DataSourcesConnOracle] = new("大型事务系统", "Large-scale OLTP systems"),
        [Content.DataSourcesConnClickhouse] = new("列式分析型数据库", "Columnar analytics database"),
        [Content.DataSourcesConnMongodb] = new("文档型 NoSQL", "Document NoSQL"),
        [Content.DataSourcesFieldName] = new("数据源名称", "Data source name"),
        [Content.DataSourcesFieldNameHint] = new("给团队一个易识别的名称", "A recognizable name for your team"),
        [Content.DataSourcesFieldNamePlaceholder] = new("如：WMS 生产库", "e.g. WMS Production"),
        [Content.DataSourcesFieldType] = new("连接器类型", "Connector type"),
        [Content.DataSourcesFieldConnStr] = new("连接串", "Connection string"),
        [Content.DataSourcesFieldConnStrPlaceholder] = new("Server=host;Database=db;User=...;", "Server=host;Database=db;User=...;"),
        [Content.DataSourcesTestConn] = new("测试连接", "Test connection"),
        [Content.DataSourcesSaveAndConnect] = new("保存并接入", "Save & connect"),
        [Content.DataSourcesLoadFailed] = new("加载数据源失败。", "Failed to load data sources."),
        [Content.DataSourcesConnectorSelected] = new("已选择连接器：{0}", "Connector selected: {0}"),
        [Content.DataSourcesTestSubmitted] = new("连通性测试已提交（Multi-DB Connector 后端计划于 P12 实现）。", "Connectivity test submitted (Multi-DB Connector backend planned for P12)."),
        [Content.DataSourcesRequiredError] = new("数据源名称和连接串必填。", "Data source name and connection string are required."),
        [Content.DataSourcesSaveFailed] = new("保存数据源失败。", "Failed to save data source."),
        [Content.DataSourcesSavedToast] = new("数据源已保存，并已授权给当前管理员。Ask 页面现在可以选择它。", "Data source saved and authorized for the current admin. It is now selectable on the Ask page."),
        [Content.DataSourcesSaving] = new("正在保存…", "Saving…"),

        [Content.DataSourceBackList] = new("返回列表", "Back to list"),
        [Content.DataSourceTabMeta] = new("元数据", "Metadata"),
        [Content.DataSourceRescan] = new("重新扫描", "Re-scan"),
        [Content.DataSourceScanning] = new("扫描中…", "Scanning…"),
        [Content.DataSourceLoadingMeta] = new("正在加载元数据…", "Loading metadata…"),
        [Content.DataSourceEmptyMetaTitle] = new("尚未扫描到元数据", "No metadata scanned yet"),
        [Content.DataSourceEmptyMetaText] = new("点击重新扫描，从业务数据库读取表和字段结构。", "Click re-scan to read table and column structures from the business database."),
        [Content.DataSourceFieldsBadge] = new("{0} 字段", "{0} fields"),
        [Content.DataSourceColFieldRel] = new("字段 / 关系", "Field / Relation"),
        [Content.DataSourceColType] = new("类型", "Type"),
        [Content.DataSourceColNullable] = new("可空", "Nullable"),
        [Content.DataSourceColSemantic] = new("Semantic", "Semantic"),
        [Content.DataSourceColVector] = new("Vector", "Vector"),
        [Content.DataSourceColBusinessKey] = new("业务键", "Business key"),
        [Content.DataSourceNotVectorized] = new("未向量化", "Not vectorized"),
        [Content.DataSourceTabGrants] = new("访问授权", "Access grants"),
        [Content.DataSourceGrantUser] = new("用户", "User"),
        [Content.DataSourceGrantRole] = new("角色", "Role"),
        [Content.DataSourceGrantSubjectPlaceholder] = new("选择授权主体…", "Select a subject…"),
        [Content.DataSourceGrantAccess] = new("授予访问", "Grant access"),
        [Content.DataSourceRefreshGrants] = new("刷新授权", "Refresh grants"),
        [Content.DataSourceLoadingGrants] = new("正在加载授权…", "Loading grants…"),
        [Content.DataSourceEmptyGrantsTitle] = new("暂无显式授权", "No explicit grants"),
        [Content.DataSourceEmptyGrantsText] = new("选择用户或角色并授予该数据源访问权限。", "Select a user or role and grant access to this data source."),
        [Content.DataSourcePanelGrants] = new("授权清单", "Grant list"),
        [Content.DataSourceColSubjectType] = new("主体类型", "Subject type"),
        [Content.DataSourceColSubject] = new("主体", "Subject"),
        [Content.DataSourceColCreated] = new("授权时间", "Granted at"),
        [Content.DataSourceColActions] = new("操作", "Actions"),
        [Content.DataSourceRevoke] = new("撤销", "Revoke"),
        [Content.DataSourceTabRls] = new("行级安全", "Row-level security"),
        [Content.DataSourceLoadingPolicies] = new("正在加载策略…", "Loading policies…"),
        [Content.DataSourcePanelRls] = new("行级安全策略", "Row-level security policy"),
        [Content.DataSourceEmptyRlsTitle] = new("未配置行级安全策略", "No row-level security policy"),
        [Content.DataSourceEmptyRlsText] = new("配置后可按用户/角色限制可见数据行，控制越权取数风险。", "Once configured, visible rows are restricted by user/role, mitigating unauthorized data access."),
        [Content.DataSourcePanelPolicies] = new("策略清单", "Policy list"),
        [Content.DataSourceLoadMetaFailed] = new("加载元数据失败（{0}）。", "Failed to load metadata ({0})."),
        [Content.DataSourceScanFailed] = new("元数据扫描失败。", "Metadata scan failed."),
        [Content.DataSourceScanDone] = new("元数据扫描完成。", "Metadata scan complete."),
        [Content.DataSourceSelectSubject] = new("请选择用户或角色。", "Please select a user or role."),
        [Content.DataSourceGrantFailed] = new("授权失败。", "Authorization failed."),
        [Content.DataSourceGranted] = new("数据源访问权限已授予。", "Data source access granted."),
        [Content.DataSourceRevokeFailed] = new("撤销失败。", "Revoke failed."),
        [Content.DataSourceRevoked] = new("授权已撤销。", "Access revoked."),
        [Content.DataSourceLoadPolicyFailed] = new("加载策略失败（{0}）。", "Failed to load policies ({0})."),
        [Content.DataSourcePolicyDeleted] = new("策略已删除。", "Policy deleted."),
        [Content.DataSourceDeleteFailed] = new("删除失败（{0}）。", "Delete failed ({0})."),
        [Content.DataSourceLoadGrantsFailed] = new("加载授权失败（{0}）。", "Failed to load grants ({0})."),
    };
}
