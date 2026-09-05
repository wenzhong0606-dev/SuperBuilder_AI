namespace SuperBuilder_AI.Models.Localization;

/// <summary>
/// 界面资源键权威注册表（M3-G0「稳定资源键」；M3-04 增补模块/页面/默认值/废弃元数据）。
/// <para>
/// 作为全平台资源键的单一事实来源：所有经 <c>UiTextResource</c> 持久化的平台基线键都应在此登记，
/// 新增键须先在此声明，避免散落硬编码与键漂移。键命名规范：<c>命名空间.语义</c>（如 <c>Login.Username</c>）。
/// 该注册表不负责取值——取值统一走 <c>ILocalizationService</c> / 前端 <c>LocalizationService.T</c>。
/// </para>
/// </summary>
public static class ResourceKeys
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

    /// <summary>表单验证消息（绑定 <c>Validation/Error</c> 共享组件）。</summary>
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

    /// <summary>通用操作动词（M3-05 续批），跨页面复用：按钮、菜单项。</summary>
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

    /// <summary>
    /// 页面页头标题与说明（M3-05 批2 起）。<c>PageHead</c> 以稳定标识 <c>Key</c> 解析 <c>Page.Title.{Key}</c> /
    /// <c>Page.Desc.{Key}</c>，避免旧约定 <c>Page.Title.{中文标题}</c> 退化成中文资源键。
    /// </summary>
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

    /// <summary>导航菜单文本（M3-04 增补，M3-05 扩展全量）。</summary>
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

    /// <summary>无障碍文本（M3-04 增补）。</summary>
    public static class Accessibility
    {
        public const string SkipToContent = "Accessibility.SkipToContent";
    }

    /// <summary>主题切换文本（M3-04 增补）。</summary>
    public static class Theme
    {
        public const string Light = "Theme.Light";
        public const string Dark = "Theme.Dark";
        public const string Switch = "Theme.Switch";
    }

    /// <summary>资源键元数据（M3-04）：归属模块、页面、平台默认（英文）值、是否已废弃。</summary>
    public sealed record ResourceKeyMeta(string Module, string? Page = null, string? DefaultValue = null, bool Deprecated = false);

    /// <summary>
    /// 资源键元数据目录：键 → 元数据。默认值取自平台 en-US 基线（<c>LocalizationSeedService.defaults</c>），
    /// 仅在此集中登记模块/页面归属与废弃状态；新增键须同步登记，废弃键置 <see cref="ResourceKeyMeta.Deprecated"/>=true。
    /// </summary>
    public static IReadOnlyDictionary<string, ResourceKeyMeta> Catalog { get; } = new Dictionary<string, ResourceKeyMeta>
    {
        [Common.Login] = new("Common", DefaultValue: "Sign in"),
        [Common.Confirm] = new("Common", DefaultValue: "Confirm"),
        [Common.Cancel] = new("Common", DefaultValue: "Cancel"),
        [Common.Save] = new("Common", DefaultValue: "Save"),
        [Common.Close] = new("Common", DefaultValue: "Close"),
        [Common.Settings] = new("Common", DefaultValue: "Settings"),
        [Common.Logout] = new("Common", DefaultValue: "Sign out"),
        [Common.Loading] = new("Common", DefaultValue: "Loading…"),

        [Login.Title] = new("Login", Page: "Login", DefaultValue: "Sign in / Select tenant"),
        [Login.Username] = new("Login", Page: "Login", DefaultValue: "Username"),
        [Login.Password] = new("Login", Page: "Login", DefaultValue: "Password"),
        [Login.TenantId] = new("Login", Page: "Login", DefaultValue: "Tenant ID"),
        [Login.Tagline] = new("Login", Page: "Login", DefaultValue: "Natural-language analytics — ask, analyze, and discover."),

        [App.Subtitle] = new("App", DefaultValue: "AI Analytics Platform"),
        [Document.OutboundOrder] = new("Document", DefaultValue: "Outbound order"),

        [Validation.Required] = new("Validation", DefaultValue: "This field is required."),
        [Validation.Email] = new("Validation", DefaultValue: "Please enter a valid email address."),
        [Validation.Format] = new("Validation", DefaultValue: "Invalid format."),

        [Error.Generic] = new("Error", DefaultValue: "An error occurred. Please try again later."),
        [Error.NotFound] = new("Error", DefaultValue: "The requested resource was not found."),
        [Empty.NoData] = new("Empty", DefaultValue: "No data available."),

        [Nav.Dashboard] = new("Nav", Page: "Dashboard", DefaultValue: "Dashboard"),
        [Nav.Ask] = new("Nav", Page: "Ask", DefaultValue: "Ask"),
        [Nav.DataSources] = new("Nav", Page: "DataSources", DefaultValue: "Data Sources"),
        [Nav.Dashboard] = new("Nav", Page: "Dashboard", DefaultValue: "Dashboard"),
        [Nav.Admin] = new("Nav", Page: "Admin", DefaultValue: "Admin"),
        [Nav.Admin] = new("Nav", Page: "Admin", DefaultValue: "Admin"),
        [Accessibility.SkipToContent] = new("Accessibility", DefaultValue: "Skip to content"),
        [Theme.Light] = new("Theme", DefaultValue: "Light"),
        [Theme.Dark] = new("Theme", DefaultValue: "Dark"),
        [Theme.Switch] = new("Theme", DefaultValue: "Toggle theme"),

        [Common.Menu] = new("Common", DefaultValue: "Menu"),
        [Common.Retry] = new("Common", DefaultValue: "Retry"),
        [Common.BackToWorkspace] = new("Common", DefaultValue: "Back to workspace"),
        [Common.CurrentUser] = new("Common", DefaultValue: "Current user"),
        [Common.Tenant] = new("Common", DefaultValue: "Tenant"),
        [Common.User] = new("Common", DefaultValue: "User"),
        [Common.AccountMenu] = new("Common", DefaultValue: "Account menu"),

        [Login.InitEntryClosed] = new("Login", Page: "Login", DefaultValue: "Initialization entry is closed"),
        [Login.InitEntryClosedHint] = new("Login", Page: "Login", DefaultValue: "Anonymous initialization is disabled in this environment. Create the first platform admin via deployment configuration."),
        [Login.InitPlatformAdmin] = new("Login", Page: "Login", DefaultValue: "Initialize platform administrator"),
        [Login.InitNotice] = new("Login", Page: "Login", DefaultValue: "This is a one-time initialization. It will be permanently closed after creation; keep the admin password safe."),
        [Login.AdminUsername] = new("Login", Page: "Login", DefaultValue: "Admin username"),
        [Login.DisplayName] = new("Login", Page: "Login", DefaultValue: "Display name"),
        [Login.AdminEmail] = new("Login", Page: "Login", DefaultValue: "Admin email"),
        [Login.AdminPassword] = new("Login", Page: "Login", DefaultValue: "Admin password"),
        [Login.ConfirmPassword] = new("Login", Page: "Login", DefaultValue: "Confirm password"),
        [Login.CreateAdmin] = new("Login", Page: "Login", DefaultValue: "Create platform admin"),
        [Login.TenantPlaceholder] = new("Login", Page: "Login", DefaultValue: "-- Select tenant --"),
        [Login.NoTenantRegister] = new("Login", Page: "Login", DefaultValue: "No tenant yet? Request access"),
        [Login.CheckingInit] = new("Login", Page: "Login", DefaultValue: "Checking platform initialization status…"),
        [Login.InitStatusError] = new("Login", Page: "Login", DefaultValue: "Unable to read platform initialization status."),
        [Login.AdminUsernameRequired] = new("Login", Page: "Login", DefaultValue: "Admin username is required."),
        [Login.AdminEmailInvalid] = new("Login", Page: "Login", DefaultValue: "Please enter a valid admin email."),
        [Login.AdminPasswordTooShort] = new("Login", Page: "Login", DefaultValue: "Admin password must be at least 8 characters."),
        [Login.AdminPasswordMismatch] = new("Login", Page: "Login", DefaultValue: "The two passwords do not match."),
        [Login.InitFailed] = new("Login", Page: "Login", DefaultValue: "Platform admin initialization failed."),
        [Login.SelectTenantFirst] = new("Login", Page: "Login", DefaultValue: "Please select a tenant first."),
        [Login.LoginFailed] = new("Login", Page: "Login", DefaultValue: "Sign-in failed: user does not exist or is disabled."),
        [Login.HeroAINativeBI] = new("Login", Page: "Login", DefaultValue: "AI Native BI"),
        [Login.HeroMultiTenant] = new("Login", Page: "Login", DefaultValue: "Multi-tenant"),
        [Login.HeroMultilingual] = new("Login", Page: "Login", DefaultValue: "Multilingual"),
        [Login.HeroLowCode] = new("Login", Page: "Login", DefaultValue: "Low-code"),
        [Login.HeroEnterpriseSaaS] = new("Login", Page: "Login", DefaultValue: "Enterprise SaaS"),

        [App.Subtitle] = new("App", DefaultValue: "AI Analytics Platform"),
        [Document.OutboundOrder] = new("Document", DefaultValue: "Outbound order"),
        [Validation.Required] = new("Validation", DefaultValue: "This field is required."),
        [Validation.Email] = new("Validation", DefaultValue: "Please enter a valid email address."),
        [Validation.Format] = new("Validation", DefaultValue: "Invalid format."),
        [Empty.NoData] = new("Empty", DefaultValue: "No data available."),

        [Action.New] = new("Action", DefaultValue: "New"),
        [Action.Create] = new("Action", DefaultValue: "Create"),
        [Action.Save] = new("Action", DefaultValue: "Save"),
        [Action.Cancel] = new("Action", DefaultValue: "Cancel"),
        [Action.Refresh] = new("Action", DefaultValue: "Refresh"),
        [Action.Delete] = new("Action", DefaultValue: "Delete"),
        [Action.Edit] = new("Action", DefaultValue: "Edit"),
        [Action.Search] = new("Action", DefaultValue: "Search"),
        [Action.Close] = new("Action", DefaultValue: "Close"),
        [Action.Reset] = new("Action", DefaultValue: "Reset"),

        [Error.Unauthorized] = new("Error", DefaultValue: "Unauthorized."),
        [Error.Forbidden] = new("Error", DefaultValue: "You do not have access to this resource."),
        [Error.Validation] = new("Error", DefaultValue: "Input validation failed."),
        [Error.Conflict] = new("Error", DefaultValue: "Conflict detected. Please refresh and retry."),
        [Error.RateLimited] = new("Error", DefaultValue: "Too many requests. Please try again later."),
        [Error.Maintenance] = new("Error", DefaultValue: "Under maintenance. Please try again later."),
        [Error.PageRender] = new("Error", Page: "Error", DefaultValue: "Page failed to render"),
        [Error.PageRenderDesc] = new("Error", Page: "Error", DefaultValue: "The page encountered a rendering error and was safely isolated without affecting other features. Retry the page or return to the workspace."),
        [Error.TechDetails] = new("Error", Page: "Error", DefaultValue: "Technical details"),
        [Error.LocalizationCultureInvalid] = new("Error", Page: "Localization", DefaultValue: "Invalid culture format."),
        [Error.LocalizationTextEmpty] = new("Error", Page: "Localization", DefaultValue: "Text cannot be empty."),
        [Error.LocalizationPlaceholderMismatch] = new("Error", Page: "Localization", DefaultValue: "Translation placeholders do not match the platform baseline."),
        [Error.LocalizationBaselineReset] = new("Error", Page: "Localization", DefaultValue: "The platform baseline cannot be reset via override."),
        [Error.OperationFailed] = new("Error", DefaultValue: "Operation failed."),
        [Error.CodeLabel] = new("Error", DefaultValue: "Error code: "),
        [Error.TraceIdLabel] = new("Error", DefaultValue: "Trace ID: "),

        [Page.TitleHome] = new("Page", Page: "Home", DefaultValue: "Workspace"),
        [Page.TitleProfile] = new("Page", Page: "Profile", DefaultValue: "Profile"),
        [Page.TitleTenants] = new("Page", Page: "Tenants", DefaultValue: "Tenants"),
        [Page.TitleTenantMembers] = new("Page", Page: "TenantMembers", DefaultValue: "Tenant Members"),
        [Page.TitleIdentity] = new("Page", Page: "Identity", DefaultValue: "Identity & Permissions"),
        [Page.TitleQuota] = new("Page", Page: "Quota", DefaultValue: "Quota"),
        [Page.TitlePlatformAdmins] = new("Page", Page: "PlatformAdmins", DefaultValue: "Platform Admins"),
        [Page.TitlePlatformAdminScopes] = new("Page", Page: "PlatformAdminScopes", DefaultValue: "Admin Tenant Scope"),
        [Page.TitleSelfRegistrationAdmin] = new("Page", Page: "SelfRegistrationAdmin", DefaultValue: "Self Registration"),
        [Page.TitleSystemStatus] = new("Page", Page: "SystemStatus", DefaultValue: "System Status"),
        [Page.TitleLocalization] = new("Page", Page: "Localization", DefaultValue: "Localization"),
        [Page.TitleThemes] = new("Page", Page: "Themes", DefaultValue: "Themes"),
        [Page.TitleDemoData] = new("Page", Page: "DemoData", DefaultValue: "Demo Data"),

        [Page.TitleBusinessModel] = new("Page", Page: "BusinessModel", DefaultValue: "Semantic Model"),
        [Page.TitleBusinessModelEntityDetail] = new("Page", Page: "BusinessModelEntityDetail", DefaultValue: "Entity Details"),
        [Page.TitleSemanticLabelDetail] = new("Page", Page: "SemanticLabelDetail", DefaultValue: "Semantic Label Details"),
        [Page.TitleComponentGallery] = new("Page", Page: "ComponentGallery", DefaultValue: "Component Gallery"),
        [Page.TitleThemeEditor] = new("Page", Page: "ThemeEditor", DefaultValue: "Theme Editor"),
        [Page.TitleDataSources] = new("Page", Page: "DataSources", DefaultValue: "Data Sources"),
        [Page.TitleModelAccounts] = new("Page", Page: "ModelAccounts", DefaultValue: "Models & Accounts (BYO)"),
        [Page.TitleMetadataEntityDetail] = new("Page", Page: "MetadataEntityDetail", DefaultValue: "Metadata Relation Details"),
        [Page.TitleDataSource] = new("Page", Page: "DataSource", DefaultValue: "Data Source Details"),

        [Page.DescBusinessModel] = new("Page", Page: "BusinessModel", DefaultValue: "Map physical tables into business language: entities, domains, and field synonyms for precise NL querying."),
        [Page.DescBusinessModelEntityDetail] = new("Page", Page: "BusinessModelEntityDetail", DefaultValue: "View entity mapping, field definitions, and resolution status."),
        [Page.DescSemanticLabelDetail] = new("Page", Page: "SemanticLabelDetail", DefaultValue: "View label definitions, synonyms, and recall strategy."),
        [Page.DescComponentGallery] = new("Page", Page: "ComponentGallery", DefaultValue: "A tour of the SuperBuilder design system: reusable components on a Chinese antique palette and Bootstrap."),
        [Page.DescThemeEditor] = new("Page", Page: "ThemeEditor", DefaultValue: "Customize your visual style on the antique palette: live preview, save and assign to a tenant or copy as a blueprint."),
        [Page.DescDataSources] = new("Page", Page: "DataSources", DefaultValue: "Manage tenant data connections, metadata overrides, and access status."),
        [Page.DescModelAccounts] = new("Page", Page: "ModelAccounts", DefaultValue: "Choose the current AI model, or bind your own model API key (BYO), tenant-isolated and masked."),
        [Page.DescMetadataEntityDetail] = new("Page", Page: "MetadataEntityDetail", DefaultValue: "View the actual data, owning objects, and vector index relations of a metadata entity."),
        [Page.DescDataSource] = new("Page", Page: "DataSource", DefaultValue: "The data source's authorization list and row-level security (RLS) policies."),

        [Page.ThemeEditorPreview] = new("Page", Page: "ThemeEditor", DefaultValue: "Live Preview"),
        [Page.ThemeEditorPalette] = new("Page", Page: "ThemeEditor", DefaultValue: "Palette"),

        [Nav.Home] = new("Nav", Page: "Home", DefaultValue: "Home"),
        [Nav.Ask] = new("Nav", Page: "Ask", DefaultValue: "Ask BI"),
        [Nav.Dashboards] = new("Nav", Page: "Dashboards", DefaultValue: "Dashboards"),
        [Nav.Apps] = new("Nav", Page: "Apps", DefaultValue: "App Factory"),
        [Nav.Agent] = new("Nav", Page: "Agent", DefaultValue: "Agent / Copilot"),
        [Nav.SemanticLabels] = new("Nav", Page: "SemanticLabels", DefaultValue: "Semantic Labels"),
        [Nav.BusinessModel] = new("Nav", Page: "BusinessModel", DefaultValue: "Semantic Model"),
        [Nav.Components] = new("Nav", Page: "Components", DefaultValue: "Component Library"),
        [Nav.ThemeEditor] = new("Nav", Page: "ThemeEditor", DefaultValue: "Theme Editor"),
        [Nav.ModelAccounts] = new("Nav", Page: "ModelAccounts", DefaultValue: "Models & Accounts"),
        [Nav.Tenants] = new("Nav", Page: "Tenants", DefaultValue: "Tenants"),
        [Nav.TenantMembers] = new("Nav", Page: "TenantMembers", DefaultValue: "Tenant Members"),
        [Nav.SelfRegistration] = new("Nav", Page: "SelfRegistration", DefaultValue: "Self Registration"),
        [Nav.DemoData] = new("Nav", Page: "DemoData", DefaultValue: "Demo Data"),
        [Nav.PlatformAdmins] = new("Nav", Page: "PlatformAdmins", DefaultValue: "Platform Admins"),
        [Nav.PlatformAdminScopes] = new("Nav", Page: "PlatformAdminScopes", DefaultValue: "Admin Tenant Scope"),
        [Nav.Identity] = new("Nav", Page: "Identity", DefaultValue: "Identity & Permissions"),
        [Nav.Audit] = new("Nav", Page: "Audit", DefaultValue: "Audit"),
        [Nav.Quota] = new("Nav", Page: "Quota", DefaultValue: "Quota"),
        [Nav.Localization] = new("Nav", Page: "Localization", DefaultValue: "Localization"),
        [Nav.Themes] = new("Nav", Page: "Themes", DefaultValue: "Themes"),
        [Nav.System] = new("Nav", Page: "System", DefaultValue: "System Status"),
        [Nav.GroupFlagship] = new("Nav", Page: "Flagship", DefaultValue: "Flagship"),
        [Nav.GroupAnalysis] = new("Nav", Page: "Analysis", DefaultValue: "Analysis"),
        [Nav.GroupCustom] = new("Nav", Page: "Custom", DefaultValue: "Custom"),
        [Nav.GroupPlatformExt] = new("Nav", Page: "PlatformExtensions", DefaultValue: "Platform Extensions"),
        [Nav.GroupAdmin] = new("Nav", Page: "Admin", DefaultValue: "Admin"),

        [Accessibility.SkipToContent] = new("Accessibility", DefaultValue: "Skip to content"),
    };

    /// <summary>返回全部已登记键（供种子覆盖校验与 CI 扫描使用）。</summary>
    public static IReadOnlyList<string> All()
    {
        var list = new List<string>();
        foreach (var prop in typeof(ResourceKeys).GetNestedTypes()
                     .SelectMany(t => t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)))
        {
            if (prop.FieldType == typeof(string) && prop.GetValue(null) is string v)
                list.Add(v);
        }
        return list;
    }
}
