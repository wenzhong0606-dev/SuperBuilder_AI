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
    }

    /// <summary>登录页相关文本。</summary>
    public static class Login
    {
        public const string Title = "Login.Title";
        public const string Username = "Login.Username";
        public const string Password = "Login.Password";
        public const string TenantId = "Login.TenantId";
        public const string Tagline = "Login.Tagline";
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
    }

    /// <summary>空状态文案。</summary>
    public static class Empty
    {
        public const string NoData = "Empty.NoData";
    }

    /// <summary>导航菜单文本（M3-04 增补）。</summary>
    public static class Nav
    {
        public const string Dashboard = "Nav.Dashboard";
        public const string Ask = "Nav.Ask";
        public const string DataSources = "Nav.DataSources";
        public const string Admin = "Nav.Admin";
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
        [Nav.Admin] = new("Nav", Page: "Admin", DefaultValue: "Admin"),
        [Accessibility.SkipToContent] = new("Accessibility", DefaultValue: "Skip to content"),
        [Theme.Light] = new("Theme", DefaultValue: "Light"),
        [Theme.Dark] = new("Theme", DefaultValue: "Dark"),
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
