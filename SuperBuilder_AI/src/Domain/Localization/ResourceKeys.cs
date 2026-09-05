namespace SuperBuilder_AI.Models.Localization;

/// <summary>
/// 界面资源键权威注册表（M3-G0「稳定资源键」）。
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
