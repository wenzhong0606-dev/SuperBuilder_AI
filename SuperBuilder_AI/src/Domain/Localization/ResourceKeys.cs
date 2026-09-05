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

        // M3-05 递延项：后端统一错误码（与 Api/Errors/ErrorCodes.cs 的 SB_* 常量一一对应）。
        // 单级扁平常量（键名 = "Error." + 错误码），供前端 L10n.T("Error."+code, serverMessage) 按码取本地化文案。
        public const string SB_BAD_REQUEST = "Error.SB_BAD_REQUEST";
        public const string SB_UNAUTHORIZED = "Error.SB_UNAUTHORIZED";
        public const string SB_FORBIDDEN = "Error.SB_FORBIDDEN";
        public const string SB_NOT_FOUND = "Error.SB_NOT_FOUND";
        public const string SB_UNSUPPORTED = "Error.SB_UNSUPPORTED";
        public const string SB_INTERNAL = "Error.SB_INTERNAL";
        public const string SB_SERVICE_UNAVAILABLE = "Error.SB_SERVICE_UNAVAILABLE";
        public const string SB_TOO_MANY_REQUESTS = "Error.SB_TOO_MANY_REQUESTS";
        public const string SB_AUTH_001 = "Error.SB_AUTH_001";
        public const string SB_BI_001 = "Error.SB_BI_001";
        public const string SB_BI_002 = "Error.SB_BI_002";
        public const string SB_BI_003 = "Error.SB_BI_003";
        public const string SB_BI_004 = "Error.SB_BI_004";
        public const string SB_BI_005 = "Error.SB_BI_005";
        public const string SB_BI_006 = "Error.SB_BI_006";
        public const string SB_APP_001 = "Error.SB_APP_001";
        public const string SB_AGENT_001 = "Error.SB_AGENT_001";
        public const string SB_PFM_001 = "Error.SB_PFM_001";
        public const string SB_PFM_002 = "Error.SB_PFM_002";
        public const string SB_AUTHZ_001 = "Error.SB_AUTHZ_001";
        public const string SB_AUTHZ_002 = "Error.SB_AUTHZ_002";
        public const string SB_SECURITY_001 = "Error.SB_SECURITY_001";
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

    /// <summary>
    /// 重型内容页正文资源键（M3-05 重内容页正文批次）。
    /// <para>
    /// 覆盖 BusinessModel / DataSources 及其详情页的静态正文、表格列、状态标签、空状态、模态框字段与主要提示。
    /// 采用单级扁平常量（键名含页面前缀，如 <c>Content.BusinessModelDomains</c>），以保证 <see cref="All"/> 反射枚举能一层取到，
    /// 避免二级嵌套类被 <c>GetNestedTypes().SelectMany(GetFields)</c> 漏枚举。
    /// </para>
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

        // ModelAccounts（模型与账号 BYO）
        public const string ModelAccountsBound = "Content.ModelAccountsBound";
        public const string ModelAccountsUnbound = "Content.ModelAccountsUnbound";
        public const string ModelAccountsDefault = "Content.ModelAccountsDefault";
        public const string ModelAccountsSetDefault = "Content.ModelAccountsSetDefault";
        public const string ModelAccountsBindPanel = "Content.ModelAccountsBindPanel";
        public const string ModelAccountsFieldModel = "Content.ModelAccountsFieldModel";
        public const string ModelAccountsFieldApiKey = "Content.ModelAccountsFieldApiKey";
        public const string ModelAccountsApiKeyPlaceholder = "Content.ModelAccountsApiKeyPlaceholder";
        public const string ModelAccountsFieldNote = "Content.ModelAccountsFieldNote";
        public const string ModelAccountsNotePlaceholder = "Content.ModelAccountsNotePlaceholder";
        public const string ModelAccountsSaveBind = "Content.ModelAccountsSaveBind";
        public const string ModelAccountsSetDefaultToast = "Content.ModelAccountsSetDefaultToast";
        public const string ModelAccountsBindSubmitted = "Content.ModelAccountsBindSubmitted";

        // SemanticLabelDetail（语义标签详情）
        public const string SemanticLabelLoading = "Content.SemanticLabelLoading";
        public const string SemanticLabelPanelDetail = "Content.SemanticLabelPanelDetail";
        public const string SemanticLabelNotFoundTitle = "Content.SemanticLabelNotFoundTitle";
        public const string SemanticLabelNotFoundText = "Content.SemanticLabelNotFoundText";
        public const string SemanticLabelInfoPanel = "Content.SemanticLabelInfoPanel";
        public const string SemanticLabelBack = "Content.SemanticLabelBack";

        // ComponentGallery（组件库巡展，含大量演示标签/状态/正文）
        public const string ComponentGallerySecButtons = "Content.ComponentGallerySecButtons";
        public const string ComponentGallerySecBadges = "Content.ComponentGallerySecBadges";
        public const string ComponentGallerySecForms = "Content.ComponentGallerySecForms";
        public const string ComponentGallerySecProgress = "Content.ComponentGallerySecProgress";
        public const string ComponentGallerySecSegmented = "Content.ComponentGallerySecSegmented";
        public const string ComponentGallerySecAlerts = "Content.ComponentGallerySecAlerts";
        public const string ComponentGallerySecTabs = "Content.ComponentGallerySecTabs";
        public const string ComponentGallerySecDataTable = "Content.ComponentGallerySecDataTable";
        public const string ComponentGallerySecModal = "Content.ComponentGallerySecModal";
        public const string ComponentGallerySecToast = "Content.ComponentGallerySecToast";
        public const string ComponentGallerySecGuard = "Content.ComponentGallerySecGuard";
        public const string ComponentGalleryBtnPrimary = "Content.ComponentGalleryBtnPrimary";
        public const string ComponentGalleryBtnSecondary = "Content.ComponentGalleryBtnSecondary";
        public const string ComponentGalleryBtnGhost = "Content.ComponentGalleryBtnGhost";
        public const string ComponentGalleryBtnDanger = "Content.ComponentGalleryBtnDanger";
        public const string ComponentGalleryBtnDisabled = "Content.ComponentGalleryBtnDisabled";
        public const string ComponentGalleryBadgePrimary = "Content.ComponentGalleryBadgePrimary";
        public const string ComponentGalleryBadgePublished = "Content.ComponentGalleryBadgePublished";
        public const string ComponentGalleryBadgeDraft = "Content.ComponentGalleryBadgeDraft";
        public const string ComponentGalleryBadgeFailed = "Content.ComponentGalleryBadgeFailed";
        public const string ComponentGalleryBadgeGlobal = "Content.ComponentGalleryBadgeGlobal";
        public const string ComponentGalleryBadgeDefault = "Content.ComponentGalleryBadgeDefault";
        public const string ComponentGalleryFormTextbox = "Content.ComponentGalleryFormTextbox";
        public const string ComponentGalleryFormDropdown = "Content.ComponentGalleryFormDropdown";
        public const string ComponentGalleryOptA = "Content.ComponentGalleryOptA";
        public const string ComponentGalleryOptB = "Content.ComponentGalleryOptB";
        public const string ComponentGalleryProgressUsed = "Content.ComponentGalleryProgressUsed";
        public const string ComponentGallerySegDay = "Content.ComponentGallerySegDay";
        public const string ComponentGallerySegWeek = "Content.ComponentGallerySegWeek";
        public const string ComponentGallerySegMonth = "Content.ComponentGallerySegMonth";
        public const string ComponentGalleryAlertInfo = "Content.ComponentGalleryAlertInfo";
        public const string ComponentGalleryAlertWarning = "Content.ComponentGalleryAlertWarning";
        public const string ComponentGalleryStatQuestions = "Content.ComponentGalleryStatQuestions";
        public const string ComponentGalleryStatHitRate = "Content.ComponentGalleryStatHitRate";
        public const string ComponentGalleryStatPending = "Content.ComponentGalleryStatPending";
        public const string ComponentGalleryStatActiveTenants = "Content.ComponentGalleryStatActiveTenants";
        public const string ComponentGallerySampleTable = "Content.ComponentGallerySampleTable";
        public const string ComponentGalleryColCode = "Content.ComponentGalleryColCode";
        public const string ComponentGalleryColName = "Content.ComponentGalleryColName";
        public const string ComponentGalleryColStatus = "Content.ComponentGalleryColStatus";
        public const string ComponentGalleryColTenant = "Content.ComponentGalleryColTenant";
        public const string ComponentGalleryCardAskTitle = "Content.ComponentGalleryCardAskTitle";
        public const string ComponentGalleryCardAskDesc = "Content.ComponentGalleryCardAskDesc";
        public const string ComponentGalleryCardDashTitle = "Content.ComponentGalleryCardDashTitle";
        public const string ComponentGalleryCardDashDesc = "Content.ComponentGalleryCardDashDesc";
        public const string ComponentGalleryCardAppTitle = "Content.ComponentGalleryCardAppTitle";
        public const string ComponentGalleryCardAppDesc = "Content.ComponentGalleryCardAppDesc";
        public const string ComponentGalleryTabOverview = "Content.ComponentGalleryTabOverview";
        public const string ComponentGalleryTabDetail = "Content.ComponentGalleryTabDetail";
        public const string ComponentGalleryTabSettings = "Content.ComponentGalleryTabSettings";
        public const string ComponentGalleryDataEmpty = "Content.ComponentGalleryDataEmpty";
        public const string ComponentGalleryModalTitle = "Content.ComponentGalleryModalTitle";
        public const string ComponentGalleryOpenModal = "Content.ComponentGalleryOpenModal";
        public const string ComponentGalleryModalBody = "Content.ComponentGalleryModalBody";
        public const string ComponentGalleryModalOk = "Content.ComponentGalleryModalOk";
        public const string ComponentGalleryConfirmTitle = "Content.ComponentGalleryConfirmTitle";
        public const string ComponentGalleryConfirmMsg = "Content.ComponentGalleryConfirmMsg";
        public const string ComponentGalleryConfirmText = "Content.ComponentGalleryConfirmText";
        public const string ComponentGalleryToastInfo = "Content.ComponentGalleryToastInfo";
        public const string ComponentGalleryToastSuccess = "Content.ComponentGalleryToastSuccess";
        public const string ComponentGalleryToastWarning = "Content.ComponentGalleryToastWarning";
        public const string ComponentGalleryToastError = "Content.ComponentGalleryToastError";
        public const string ComponentGalleryToastDeleted = "Content.ComponentGalleryToastDeleted";
        public const string ComponentGalleryGuardDesc = "Content.ComponentGalleryGuardDesc";
        public const string ComponentGalleryGuardHasPerm = "Content.ComponentGalleryGuardHasPerm";

        // ThemeEditor（主题编辑器补充正文）
        public const string ThemeEditorSampleMetric = "Content.ThemeEditorSampleMetric";
        public const string ThemeEditorBtnPrimary = "Content.ThemeEditorBtnPrimary";
        public const string ThemeEditorBtnSecondary = "Content.ThemeEditorBtnSecondary";
        public const string ThemeEditorBadgePublished = "Content.ThemeEditorBadgePublished";
        public const string ThemeEditorBadgeDraft = "Content.ThemeEditorBadgeDraft";
        public const string ThemeEditorSavedToast = "Content.ThemeEditorSavedToast";

        // MetadataEntityDetail（元数据关系详情）
        public const string MetadataEntityLoading = "Content.MetadataEntityLoading";
        public const string MetadataEntityIntro = "Content.MetadataEntityIntro";
        public const string MetadataEntityNotFound = "Content.MetadataEntityNotFound";
        // M3-05 递延项（Task #83）：分析页正文键（Ask/Dashboards/Apps/AppDetail/AskTurn），单级扁平以匹配 All() 反射枚举。
        public const string AskTitle = "Content.AskTitle";
        public const string AskRestoringSession = "Content.AskRestoringSession";
        public const string AskLoginHint = "Content.AskLoginHint";
        public const string AskLoginLink = "Content.AskLoginLink";
        public const string AskLoginHint2 = "Content.AskLoginHint2";
        public const string AskDataScope = "Content.AskDataScope";
        public const string AskDataLoading = "Content.AskDataLoading";
        public const string AskQuestionLabel = "Content.AskQuestionLabel";
        public const string AskQuestionPlaceholder = "Content.AskQuestionPlaceholder";
        public const string AskButtonAsk = "Content.AskButtonAsk";
        public const string AskButtonClear = "Content.AskButtonClear";
        public const string AskHistoryRestored = "Content.AskHistoryRestored";
        public const string AskNoConversation = "Content.AskNoConversation";
        public const string AskDataSourceListEmpty = "Content.AskDataSourceListEmpty";
        public const string AskNoDataSources = "Content.AskNoDataSources";
        public const string AskDataSourceLoadFailed = "Content.AskDataSourceLoadFailed";
        public const string AskEnterQuestion = "Content.AskEnterQuestion";
        public const string AskNoAuthorizedDataSource = "Content.AskNoAuthorizedDataSource";
        public const string AskEnterRefineInstruction = "Content.AskEnterRefineInstruction";
        public const string AskRefineBasedOnResult = "Content.AskRefineBasedOnResult";
        public const string AskRefineException = "Content.AskRefineException";
        public const string AskFailed = "Content.AskFailed";
        public const string AskNoValidResult = "Content.AskNoValidResult";
        public const string AskPublishedAppNamePrefix = "Content.AskPublishedAppNamePrefix";
        public const string AskChartTitleDefault = "Content.AskChartTitleDefault";
        public const string AskTableTitleDefault = "Content.AskTableTitleDefault";
        public const string AskAiSummaryTitle = "Content.AskAiSummaryTitle";
        public const string AskPublishSuccess = "Content.AskPublishSuccess";
        public const string AskPublishFailed = "Content.AskPublishFailed";
        public const string AskPublishException = "Content.AskPublishException";
        public const string DashboardsTitle = "Content.DashboardsTitle";
        public const string DashboardsDesc = "Content.DashboardsDesc";
        public const string DashboardsSearchPlaceholder = "Content.DashboardsSearchPlaceholder";
        public const string DashboardsEmptyTitle = "Content.DashboardsEmptyTitle";
        public const string DashboardsEmptyText = "Content.DashboardsEmptyText";
        public const string DashboardsLoading = "Content.DashboardsLoading";
        public const string DashboardsNew = "Content.DashboardsNew";
        public const string DashboardsStatTotal = "Content.DashboardsStatTotal";
        public const string DashboardsStatTotalSub = "Content.DashboardsStatTotalSub";
        public const string DashboardsStatPublished = "Content.DashboardsStatPublished";
        public const string DashboardsStatPublishedSub = "Content.DashboardsStatPublishedSub";
        public const string DashboardsStatDraft = "Content.DashboardsStatDraft";
        public const string DashboardsStatDraftSub = "Content.DashboardsStatDraftSub";
        public const string DashboardsPublishFromAsk = "Content.DashboardsPublishFromAsk";
        public const string DashboardsRowDetail = "Content.DashboardsRowDetail";
        public const string DashboardsRowDelete = "Content.DashboardsRowDelete";
        public const string DashboardsDeleteConfirmMsg = "Content.DashboardsDeleteConfirmMsg";
        public const string DashboardsDeleteConfirmText = "Content.DashboardsDeleteConfirmText";
        public const string DashboardsFieldStatus = "Content.DashboardsFieldStatus";
        public const string DashboardsStatusDraft = "Content.DashboardsStatusDraft";
        public const string DashboardsStatusPublished = "Content.DashboardsStatusPublished";
        public const string DashboardsFieldDslJson = "Content.DashboardsFieldDslJson";
        public const string DashboardsFieldDslJsonHint = "Content.DashboardsFieldDslJsonHint";
        public const string DashboardsModalCancel = "Content.DashboardsModalCancel";
        public const string DashboardsModalCreate = "Content.DashboardsModalCreate";
        public const string DashboardsMissingIdOpen = "Content.DashboardsMissingIdOpen";
        public const string DashboardsMissingIdDelete = "Content.DashboardsMissingIdDelete";
        public const string DashboardsDeleteSuccess = "Content.DashboardsDeleteSuccess";
        public const string DashboardsDeleteFailed = "Content.DashboardsDeleteFailed";
        public const string DashboardsBlueprintUnavailable = "Content.DashboardsBlueprintUnavailable";
        public const string DashboardsDslJsonEmpty = "Content.DashboardsDslJsonEmpty";
        public const string DashboardsCreateSuccess = "Content.DashboardsCreateSuccess";
        public const string DashboardsCreateFailed = "Content.DashboardsCreateFailed";
        public const string DashboardsLoadFailed = "Content.DashboardsLoadFailed";
        public const string AppsTitle = "Content.AppsTitle";
        public const string AppsDesc = "Content.AppsDesc";
        public const string AppsSearchPlaceholder = "Content.AppsSearchPlaceholder";
        public const string AppsEmptyTitle = "Content.AppsEmptyTitle";
        public const string AppsEmptyText = "Content.AppsEmptyText";
        public const string AppsLoading = "Content.AppsLoading";
        public const string AppsPanelTitle = "Content.AppsPanelTitle";
        public const string AppsNew = "Content.AppsNew";
        public const string AppsFromAsk = "Content.AppsFromAsk";
        public const string AppsStatTotal = "Content.AppsStatTotal";
        public const string AppsStatTotalSub = "Content.AppsStatTotalSub";
        public const string AppsStatGlobal = "Content.AppsStatGlobal";
        public const string AppsStatGlobalSub = "Content.AppsStatGlobalSub";
        public const string AppsStatTenant = "Content.AppsStatTenant";
        public const string AppsStatTenantSub = "Content.AppsStatTenantSub";
        public const string AppsNoDescription = "Content.AppsNoDescription";
        public const string AppsBadgeGlobal = "Content.AppsBadgeGlobal";
        public const string AppsBadgeTenant = "Content.AppsBadgeTenant";
        public const string AppsEdit = "Content.AppsEdit";
        public const string AppsDelete = "Content.AppsDelete";
        public const string AppsDeleteConfirmTitle = "Content.AppsDeleteConfirmTitle";
        public const string AppsDeleteConfirmMsg = "Content.AppsDeleteConfirmMsg";
        public const string AppsDeleteConfirmText = "Content.AppsDeleteConfirmText";
        public const string AppsGenDescLabel = "Content.AppsGenDescLabel";
        public const string AppsGenDescHint = "Content.AppsGenDescHint";
        public const string AppsGenDescPlaceholder = "Content.AppsGenDescPlaceholder";
        public const string AppsGenCodeLabel = "Content.AppsGenCodeLabel";
        public const string AppsGenCodeHint = "Content.AppsGenCodeHint";
        public const string AppsGenCodePlaceholder = "Content.AppsGenCodePlaceholder";
        public const string AppsGenCancel = "Content.AppsGenCancel";
        public const string AppsGenSubmit = "Content.AppsGenSubmit";
        public const string AppsDslIntro = "Content.AppsDslIntro";
        public const string AppsDslGlobalWarning = "Content.AppsDslGlobalWarning";
        public const string AppsDslFieldLabel = "Content.AppsDslFieldLabel";
        public const string AppsDslSave = "Content.AppsDslSave";
        public const string AppsMissingCodeOpen = "Content.AppsMissingCodeOpen";
        public const string AppsMissingCodeEdit = "Content.AppsMissingCodeEdit";
        public const string AppsMissingCodeDelete = "Content.AppsMissingCodeDelete";
        public const string AppsGenDescRequired = "Content.AppsGenDescRequired";
        public const string AppsGenSuccess = "Content.AppsGenSuccess";
        public const string AppsGenFailed = "Content.AppsGenFailed";
        public const string AppsReadFailed = "Content.AppsReadFailed";
        public const string AppsDslJsonEmpty = "Content.AppsDslJsonEmpty";
        public const string AppsDslSaved = "Content.AppsDslSaved";
        public const string AppsDslSaveFailed = "Content.AppsDslSaveFailed";
        public const string AppsDeleteSuccess = "Content.AppsDeleteSuccess";
        public const string AppsDeleteFailed = "Content.AppsDeleteFailed";
        public const string AppsLoadFailed = "Content.AppsLoadFailed";
        public const string AppDetailDesc = "Content.AppDetailDesc";
        public const string AppDetailBack = "Content.AppDetailBack";
        public const string AppDetailRefresh = "Content.AppDetailRefresh";
        public const string AppDetailDelete = "Content.AppDetailDelete";
        public const string AppDetailLoading = "Content.AppDetailLoading";
        public const string AppDetailTabOverview = "Content.AppDetailTabOverview";
        public const string AppDetailPanelBasic = "Content.AppDetailPanelBasic";
        public const string AppDetailTabComponents = "Content.AppDetailTabComponents";
        public const string AppDetailPanelComponents = "Content.AppDetailPanelComponents";
        public const string AppDetailEmptyComponentsTitle = "Content.AppDetailEmptyComponentsTitle";
        public const string AppDetailEmptyComponentsText = "Content.AppDetailEmptyComponentsText";
        public const string AppDetailTabDsl = "Content.AppDetailTabDsl";
        public const string AppDetailPanelDsl = "Content.AppDetailPanelDsl";
        public const string AppDetailDeleteConfirmTitle = "Content.AppDetailDeleteConfirmTitle";
        public const string AppDetailDeleteConfirmMsg = "Content.AppDetailDeleteConfirmMsg";
        public const string AppDetailDeleteConfirmText = "Content.AppDetailDeleteConfirmText";
        public const string AppDetailTitleDefault = "Content.AppDetailTitleDefault";
        public const string AppDetailDeleteSuccess = "Content.AppDetailDeleteSuccess";
        public const string AppDetailDeleteFailed = "Content.AppDetailDeleteFailed";
        public const string AppDetailLoadFailed = "Content.AppDetailLoadFailed";
        public const string AskTurnMe = "Content.AskTurnMe";
        public const string AskTurnAiSummary = "Content.AskTurnAiSummary";
        public const string AskTurnMeta = "Content.AskTurnMeta";
        public const string AskTurnChartDefault = "Content.AskTurnChartDefault";
        public const string AskTurnViewLabel = "Content.AskTurnViewLabel";
        public const string AskTurnBar = "Content.AskTurnBar";
        public const string AskTurnLine = "Content.AskTurnLine";
        public const string AskTurnPie = "Content.AskTurnPie";
        public const string AskTurnHideLegend = "Content.AskTurnHideLegend";
        public const string AskTurnShowLegend = "Content.AskTurnShowLegend";
        public const string AskTurnCyclePalette = "Content.AskTurnCyclePalette";
        public const string AskTurnStacked = "Content.AskTurnStacked";
        public const string AskTurnUnstacked = "Content.AskTurnUnstacked";
        public const string AskTurnArea = "Content.AskTurnArea";
        public const string AskTurnUnarea = "Content.AskTurnUnarea";
        public const string AskTurnMultiAxis = "Content.AskTurnMultiAxis";
        public const string AskTurnUnmulti = "Content.AskTurnUnmulti";
        public const string AskTurnRowsTotal = "Content.AskTurnRowsTotal";
        public const string AskTurnDrill = "Content.AskTurnDrill";
        public const string AskTurnClearDrill = "Content.AskTurnClearDrill";
        public const string AskTurnExportCsv = "Content.AskTurnExportCsv";
        public const string AskTurnExportExcel = "Content.AskTurnExportExcel";
        public const string AskTurnQueryFailed = "Content.AskTurnQueryFailed";
        public const string AskTurnViewSql = "Content.AskTurnViewSql";
        public const string AskTurnHideCompare = "Content.AskTurnHideCompare";
        public const string AskTurnShowCompare = "Content.AskTurnShowCompare";
        public const string AskTurnOriginalResult = "Content.AskTurnOriginalResult";
        public const string AskTurnRefinedResult = "Content.AskTurnRefinedResult";
        public const string AskTurnRefinePlaceholder = "Content.AskTurnRefinePlaceholder";
        public const string AskTurnApplyAdjust = "Content.AskTurnApplyAdjust";
        public const string AskTurnSemanticPlaceholder = "Content.AskTurnSemanticPlaceholder";
        public const string AskTurnSemanticRefine = "Content.AskTurnSemanticRefine";
        public const string AskTurnRefining = "Content.AskTurnRefining";
        public const string AskTurnPublish = "Content.AskTurnPublish";
        public const string AskTurnPublishing = "Content.AskTurnPublishing";
        public const string AskTurnRefineNoCmd = "Content.AskTurnRefineNoCmd";
        public const string AskTurnRefineNoViz = "Content.AskTurnRefineNoViz";
        public const string AskTurnRefineUnrecognized = "Content.AskTurnRefineUnrecognized";
        public const string AskTurnRefineApplied = "Content.AskTurnRefineApplied";
        public const string CommonRefresh = "Content.CommonRefresh";
        public const string CommonCancel = "Content.CommonCancel";
        public const string CommonCreate = "Content.CommonCreate";
        public const string CommonSave = "Content.CommonSave";
        public const string CommonAdd = "Content.CommonAdd";
        public const string CommonUsername = "Content.CommonUsername";
        public const string CommonDisplayName = "Content.CommonDisplayName";
        public const string CommonEmail = "Content.CommonEmail";
        public const string CommonName = "Content.CommonName";
        public const string CommonDescription = "Content.CommonDescription";
        public const string CommonRole = "Content.CommonRole";
        public const string CommonUser = "Content.CommonUser";
        public const string CommonPermission = "Content.CommonPermission";
        public const string CommonPermissions = "Content.CommonPermissions";
        public const string CommonRoleCode = "Content.CommonRoleCode";
        public const string CommonSelectPlaceholder = "Content.CommonSelectPlaceholder";
        public const string CommonOptional = "Content.CommonOptional";
        public const string AdminIdentityTitle = "Content.AdminIdentityTitle";
        public const string AdminIdentityDesc = "Content.AdminIdentityDesc";
        public const string AdminIdentityCreateUser = "Content.AdminIdentityCreateUser";
        public const string AdminIdentityCreateRole = "Content.AdminIdentityCreateRole";
        public const string AdminIdentityLoading = "Content.AdminIdentityLoading";
        public const string AdminIdentityStatUsers = "Content.AdminIdentityStatUsers";
        public const string AdminIdentityStatUsersSub = "Content.AdminIdentityStatUsersSub";
        public const string AdminIdentityStatRoles = "Content.AdminIdentityStatRoles";
        public const string AdminIdentityStatRolesSub = "Content.AdminIdentityStatRolesSub";
        public const string AdminIdentityStatPerms = "Content.AdminIdentityStatPerms";
        public const string AdminIdentityStatPermsSub = "Content.AdminIdentityStatPermsSub";
        public const string AdminIdentityPanelUsers = "Content.AdminIdentityPanelUsers";
        public const string AdminIdentityEmptyUsers = "Content.AdminIdentityEmptyUsers";
        public const string AdminIdentityEmptyUsersText = "Content.AdminIdentityEmptyUsersText";
        public const string AdminIdentityNoMatchUsers = "Content.AdminIdentityNoMatchUsers";
        public const string AdminIdentityAssignRole = "Content.AdminIdentityAssignRole";
        public const string AdminIdentityPanelRoles = "Content.AdminIdentityPanelRoles";
        public const string AdminIdentityEmptyRoles = "Content.AdminIdentityEmptyRoles";
        public const string AdminIdentityEmptyRolesText = "Content.AdminIdentityEmptyRolesText";
        public const string AdminIdentityNoMatchRoles = "Content.AdminIdentityNoMatchRoles";
        public const string AdminIdentityEditPerms = "Content.AdminIdentityEditPerms";
        public const string AdminIdentityUsernamePh = "Content.AdminIdentityUsernamePh";
        public const string AdminIdentityInitialRole = "Content.AdminIdentityInitialRole";
        public const string AdminIdentityNoRoles = "Content.AdminIdentityNoRoles";
        public const string AdminIdentityRoleCodePh = "Content.AdminIdentityRoleCodePh";
        public const string AdminIdentityNoPerms = "Content.AdminIdentityNoPerms";
        public const string AdminIdentitySelectRole = "Content.AdminIdentitySelectRole";
        public const string AdminIdentityModalEditPerms = "Content.AdminIdentityModalEditPerms";
        public const string AdminIdentityConfirmSavePermsTitle = "Content.AdminIdentityConfirmSavePermsTitle";
        public const string AdminIdentityConfirmSavePermsMsg = "Content.AdminIdentityConfirmSavePermsMsg";
        public const string AdminIdentityConfirmAssignTitle = "Content.AdminIdentityConfirmAssignTitle";
        public const string AdminIdentityConfirmAssignMsg = "Content.AdminIdentityConfirmAssignMsg";
        public const string AdminIdentityAssignHint = "Content.AdminIdentityAssignHint";
        public const string AdminIdentityPermHint = "Content.AdminIdentityPermHint";
        public const string AdminIdentityUsernameRequired = "Content.AdminIdentityUsernameRequired";
        public const string AdminIdentityUserCreated = "Content.AdminIdentityUserCreated";
        public const string AdminIdentityCreateFailed = "Content.AdminIdentityCreateFailed";
        public const string AdminIdentityRoleCodeRequired = "Content.AdminIdentityRoleCodeRequired";
        public const string AdminIdentityRoleCreated = "Content.AdminIdentityRoleCreated";
        public const string AdminIdentitySelectRoleRequired = "Content.AdminIdentitySelectRoleRequired";
        public const string AdminIdentityAssigned = "Content.AdminIdentityAssigned";
        public const string AdminIdentityAssignFailed = "Content.AdminIdentityAssignFailed";
        public const string AdminIdentityPermsUpdated = "Content.AdminIdentityPermsUpdated";
        public const string AdminIdentitySaveFailed = "Content.AdminIdentitySaveFailed";
        public const string AdminPlatformAdminsDesc = "Content.AdminPlatformAdminsDesc";
        public const string AdminPlatformAdminsCreate = "Content.AdminPlatformAdminsCreate";
        public const string AdminPlatformAdminsTabAdmins = "Content.AdminPlatformAdminsTabAdmins";
        public const string AdminPlatformAdminsPanelAdmins = "Content.AdminPlatformAdminsPanelAdmins";
        public const string AdminPlatformAdminsEmptyAdminsTitle = "Content.AdminPlatformAdminsEmptyAdminsTitle";
        public const string AdminPlatformAdminsEmptyAdminsText = "Content.AdminPlatformAdminsEmptyAdminsText";
        public const string AdminPlatformAdminsTabAudit = "Content.AdminPlatformAdminsTabAudit";
        public const string AdminPlatformAdminsPanelAudit = "Content.AdminPlatformAdminsPanelAudit";
        public const string AdminPlatformAdminsEmptyAuditTitle = "Content.AdminPlatformAdminsEmptyAuditTitle";
        public const string AdminPlatformAdminsEmptyAuditText = "Content.AdminPlatformAdminsEmptyAuditText";
        public const string AdminPlatformAdminsModalCreate = "Content.AdminPlatformAdminsModalCreate";
        public const string AdminPlatformAdminsUsernamePh = "Content.AdminPlatformAdminsUsernamePh";
        public const string AdminPlatformAdminsInitialPassword = "Content.AdminPlatformAdminsInitialPassword";
        public const string AdminPlatformAdminsPasswordPh = "Content.AdminPlatformAdminsPasswordPh";
        public const string AdminPlatformAdminsModalReset = "Content.AdminPlatformAdminsModalReset";
        public const string AdminPlatformAdminsResetHint = "Content.AdminPlatformAdminsResetHint";
        public const string AdminPlatformAdminsNewPassword = "Content.AdminPlatformAdminsNewPassword";
        public const string AdminPlatformAdminsResetButton = "Content.AdminPlatformAdminsResetButton";
        public const string AdminPlatformAdminsResetPassword = "Content.AdminPlatformAdminsResetPassword";
        public const string AdminPlatformAdminsDisableTitle = "Content.AdminPlatformAdminsDisableTitle";
        public const string AdminPlatformAdminsDisableMsg = "Content.AdminPlatformAdminsDisableMsg";
        public const string AdminPlatformAdminsDisableBtn = "Content.AdminPlatformAdminsDisableBtn";
        public const string AdminPlatformAdminsEnableTitle = "Content.AdminPlatformAdminsEnableTitle";
        public const string AdminPlatformAdminsEnableMsg = "Content.AdminPlatformAdminsEnableMsg";
        public const string AdminPlatformAdminsEnableBtn = "Content.AdminPlatformAdminsEnableBtn";
        public const string AdminPlatformAdminsColStatus = "Content.AdminPlatformAdminsColStatus";
        public const string AdminPlatformAdminsColCreatedTime = "Content.AdminPlatformAdminsColCreatedTime";
        public const string AdminPlatformAdminsColAction = "Content.AdminPlatformAdminsColAction";
        public const string AdminPlatformAdminsColActor = "Content.AdminPlatformAdminsColActor";
        public const string AdminPlatformAdminsColResult = "Content.AdminPlatformAdminsColResult";
        public const string AdminPlatformAdminsColTime = "Content.AdminPlatformAdminsColTime";
        public const string AdminPlatformAdminsCreateRequired = "Content.AdminPlatformAdminsCreateRequired";
        public const string AdminPlatformAdminsInitPasswordTooShort = "Content.AdminPlatformAdminsInitPasswordTooShort";
        public const string AdminPlatformAdminsCreated = "Content.AdminPlatformAdminsCreated";
        public const string AdminPlatformAdminsCreateFailed = "Content.AdminPlatformAdminsCreateFailed";
        public const string AdminPlatformAdminsNewPasswordTooShort = "Content.AdminPlatformAdminsNewPasswordTooShort";
        public const string AdminPlatformAdminsResetDone = "Content.AdminPlatformAdminsResetDone";
        public const string AdminPlatformAdminsResetFailed = "Content.AdminPlatformAdminsResetFailed";
        public const string AdminPlatformAdminsDisabled = "Content.AdminPlatformAdminsDisabled";
        public const string AdminPlatformAdminsDisableFailed = "Content.AdminPlatformAdminsDisableFailed";
        public const string AdminPlatformAdminsEnabled = "Content.AdminPlatformAdminsEnabled";
        public const string AdminPlatformAdminsEnableFailed = "Content.AdminPlatformAdminsEnableFailed";
        public const string AdminPlatformAdminsActAdd = "Content.AdminPlatformAdminsActAdd";
        public const string AdminPlatformAdminsActDisable = "Content.AdminPlatformAdminsActDisable";
        public const string AdminPlatformAdminsActEnable = "Content.AdminPlatformAdminsActEnable";
        public const string AdminPlatformAdminsActReset = "Content.AdminPlatformAdminsActReset";
        public const string AdminTenantsDesc = "Content.AdminTenantsDesc";
        public const string AdminTenantsListTitle = "Content.AdminTenantsListTitle";
        public const string AdminTenantsSearchPlaceholder = "Content.AdminTenantsSearchPlaceholder";
        public const string AdminTenantsEmptyTitle = "Content.AdminTenantsEmptyTitle";
        public const string AdminTenantsEmptyText = "Content.AdminTenantsEmptyText";
        public const string AdminTenantsLoadingText = "Content.AdminTenantsLoadingText";
        public const string AdminTenantsNew = "Content.AdminTenantsNew";
        public const string AdminTenantsStatTotal = "Content.AdminTenantsStatTotal";
        public const string AdminTenantsStatTotalSub = "Content.AdminTenantsStatTotalSub";
        public const string AdminTenantsStatEnabled = "Content.AdminTenantsStatEnabled";
        public const string AdminTenantsStatEnabledSub = "Content.AdminTenantsStatEnabledSub";
        public const string AdminTenantsStatDisabled = "Content.AdminTenantsStatDisabled";
        public const string AdminTenantsStatDisabledSub = "Content.AdminTenantsStatDisabledSub";
        public const string AdminTenantsEdit = "Content.AdminTenantsEdit";
        public const string AdminTenantsDisable = "Content.AdminTenantsDisable";
        public const string AdminTenantsEnable = "Content.AdminTenantsEnable";
        public const string AdminTenantsEnableTitle = "Content.AdminTenantsEnableTitle";
        public const string AdminTenantsDisableTitle = "Content.AdminTenantsDisableTitle";
        public const string AdminTenantsSettings = "Content.AdminTenantsSettings";
        public const string AdminTenantsCreateTitle = "Content.AdminTenantsCreateTitle";
        public const string AdminTenantsCodeLabel = "Content.AdminTenantsCodeLabel";
        public const string AdminTenantsCodePlaceholder = "Content.AdminTenantsCodePlaceholder";
        public const string AdminTenantsNameLabel = "Content.AdminTenantsNameLabel";
        public const string AdminTenantsNamePlaceholder = "Content.AdminTenantsNamePlaceholder";
        public const string AdminTenantsAdminUserLabel = "Content.AdminTenantsAdminUserLabel";
        public const string AdminTenantsAdminUserPlaceholder = "Content.AdminTenantsAdminUserPlaceholder";
        public const string AdminTenantsAdminDisplayNameLabel = "Content.AdminTenantsAdminDisplayNameLabel";
        public const string AdminTenantsAdminDisplayNamePlaceholder = "Content.AdminTenantsAdminDisplayNamePlaceholder";
        public const string AdminTenantsAdminEmailLabel = "Content.AdminTenantsAdminEmailLabel";
        public const string AdminTenantsAdminPasswordLabel = "Content.AdminTenantsAdminPasswordLabel";
        public const string AdminTenantsAdminPasswordHint = "Content.AdminTenantsAdminPasswordHint";
        public const string AdminTenantsLanguagesLabel = "Content.AdminTenantsLanguagesLabel";
        public const string AdminTenantsLanguagesHint = "Content.AdminTenantsLanguagesHint";
        public const string AdminTenantsDefaultLangLabel = "Content.AdminTenantsDefaultLangLabel";
        public const string AdminTenantsEditTitle = "Content.AdminTenantsEditTitle";
        public const string AdminTenantsEditLanguagesLabel = "Content.AdminTenantsEditLanguagesLabel";
        public const string AdminTenantsEditLanguagesHint = "Content.AdminTenantsEditLanguagesHint";
        public const string AdminTenantsEditDefaultLangLabel = "Content.AdminTenantsEditDefaultLangLabel";
        public const string AdminTenantsSaveEdit = "Content.AdminTenantsSaveEdit";
        public const string AdminTenantsSettingTitle = "Content.AdminTenantsSettingTitle";
        public const string AdminTenantsSettingEmptyTitle = "Content.AdminTenantsSettingEmptyTitle";
        public const string AdminTenantsSettingEmptyText = "Content.AdminTenantsSettingEmptyText";
        public const string AdminTenantsSettingKeyLabel = "Content.AdminTenantsSettingKeyLabel";
        public const string AdminTenantsSettingKeyPlaceholder = "Content.AdminTenantsSettingKeyPlaceholder";
        public const string AdminTenantsSettingValueLabel = "Content.AdminTenantsSettingValueLabel";
        public const string AdminTenantsSettingValuePlaceholder = "Content.AdminTenantsSettingValuePlaceholder";
        public const string AdminTenantsSettingTypeLabel = "Content.AdminTenantsSettingTypeLabel";
        public const string AdminTenantsClose = "Content.AdminTenantsClose";
        public const string AdminTenantsSaveSetting = "Content.AdminTenantsSaveSetting";
        public const string AdminTenantsCodeRequired = "Content.AdminTenantsCodeRequired";
        public const string AdminTenantsAdminUserRequired = "Content.AdminTenantsAdminUserRequired";
        public const string AdminTenantsPasswordTooShort = "Content.AdminTenantsPasswordTooShort";
        public const string AdminTenantsLangRequired = "Content.AdminTenantsLangRequired";
        public const string AdminTenantsCodeExists = "Content.AdminTenantsCodeExists";
        public const string AdminTenantsSaveFailed = "Content.AdminTenantsSaveFailed";
        public const string AdminTenantsCreated = "Content.AdminTenantsCreated";
        public const string AdminTenantsEditLangRequired = "Content.AdminTenantsEditLangRequired";
        public const string AdminTenantsUpdated = "Content.AdminTenantsUpdated";
        public const string AdminTenantsMissingId = "Content.AdminTenantsMissingId";
        public const string AdminTenantsOpFailed = "Content.AdminTenantsOpFailed";
        public const string AdminTenantsEnabled = "Content.AdminTenantsEnabled";
        public const string AdminTenantsDisabled = "Content.AdminTenantsDisabled";
        public const string AdminTenantsSettingMissingId = "Content.AdminTenantsSettingMissingId";
        public const string AdminTenantsSettingKeyRequired = "Content.AdminTenantsSettingKeyRequired";
        public const string AdminTenantsSettingKeyExists = "Content.AdminTenantsSettingKeyExists";
        public const string AdminTenantsSettingSaved = "Content.AdminTenantsSettingSaved";
        public const string AdminTenantsConfirmMsg = "Content.AdminTenantsConfirmMsg";

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

        // M3-05 递延项：后端统一错误码（Error.SB_*），en-US 平台基线 = Catalog.DefaultValue，zh-CN = LocalizationSeedService.ZhCnDefaults。
        [Error.SB_BAD_REQUEST] = new("Error", DefaultValue: "The request parameters are invalid. Please check your input and try again."),
        [Error.SB_UNAUTHORIZED] = new("Error", DefaultValue: "Authentication failed. Please sign in again."),
        [Error.SB_FORBIDDEN] = new("Error", DefaultValue: "Insufficient permissions. Your account is not authorized for this operation."),
        [Error.SB_NOT_FOUND] = new("Error", DefaultValue: "The requested resource does not exist or has been deleted."),
        [Error.SB_UNSUPPORTED] = new("Error", DefaultValue: "This operation is not supported."),
        [Error.SB_INTERNAL] = new("Error", DefaultValue: "The service is temporarily unavailable. Please retry later; if it persists, contact the administrator with the error code."),
        [Error.SB_SERVICE_UNAVAILABLE] = new("Error", DefaultValue: "The platform is not ready (database unreachable or initialization incomplete). Please retry later or contact the administrator."),
        [Error.SB_TOO_MANY_REQUESTS] = new("Error", DefaultValue: "Too many requests. Please try again later."),
        [Error.SB_AUTH_001] = new("Error", DefaultValue: "The username or tenant does not exist, or the account is disabled."),
        [Error.SB_BI_001] = new("Error", DefaultValue: "Could not identify a queryable table from your question. Try rephrasing or naming a specific business object (e.g. 'orders', 'inventory')."),
        [Error.SB_BI_002] = new("Error", DefaultValue: "Could not identify an analyzable field. Please add a metric or dimension (e.g. 'sales amount', 'by region')."),
        [Error.SB_BI_003] = new("Error", DefaultValue: "This analysis is not supported by the current business semantics (e.g. the aggregation is unsupported). Please rephrase."),
        [Error.SB_BI_004] = new("Error", DefaultValue: "Multiple metric fields may match. Please specify clearly (e.g. 'order amount' instead of 'amount')."),
        [Error.SB_BI_005] = new("Error", DefaultValue: "The model's confidence in this query is low. Please add clearer metrics, dimensions, or filters and retry."),
        [Error.SB_BI_006] = new("Error", DefaultValue: "The data source is temporarily unreachable. Please retry later or contact the administrator to check the connection."),
        [Error.SB_APP_001] = new("Error", DefaultValue: "The application definition (DSL) is invalid. Please check the component configuration and retry."),
        [Error.SB_AGENT_001] = new("Error", DefaultValue: "The agent returned no valid content. Please re-describe the task."),
        [Error.SB_PFM_001] = new("Error", DefaultValue: "The current tenant quota is exhausted. Please upgrade the plan or contact the administrator."),
        [Error.SB_PFM_002] = new("Error", DefaultValue: "The operation crossed the tenant boundary and was rejected by the security policy."),
        [Error.SB_AUTHZ_001] = new("Error", DefaultValue: "Your account is not authorized to access the selected data source."),
        [Error.SB_AUTHZ_002] = new("Error", DefaultValue: "Your account has no access scope satisfying the row-level data policy."),
        [Error.SB_SECURITY_001] = new("Error", DefaultValue: "The query plan failed the final security validation and was blocked before execution."),

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

        // M3-05 重内容页正文批次：Content.* 键（en-US 基线）
        [Content.BusinessModelDomains] = new("Content", Page: "BusinessModel", DefaultValue: "Domains"),
        [Content.BusinessModelDomainsSub] = new("Content", Page: "BusinessModel", DefaultValue: "For categorization"),
        [Content.BusinessModelEntities] = new("Content", Page: "BusinessModel", DefaultValue: "Entities"),
        [Content.BusinessModelEntitiesSub] = new("Content", Page: "BusinessModel", DefaultValue: "Map to business tables"),
        [Content.BusinessModelResolved] = new("Content", Page: "BusinessModel", DefaultValue: "Semantics Resolved"),
        [Content.BusinessModelResolvedSub] = new("Content", Page: "BusinessModel", DefaultValue: "Queryable in Ask"),
        [Content.BusinessModelSearchPlaceholder] = new("Content", Page: "BusinessModel", DefaultValue: "Search entities…"),
        [Content.BusinessModelEmptyDomainsTitle] = new("Content", Page: "BusinessModel", DefaultValue: "No business domains yet"),
        [Content.BusinessModelEmptyDomainsText] = new("Content", Page: "BusinessModel", DefaultValue: "Domains categorize entities, e.g. Sales, Inventory, Finance."),
        [Content.BusinessModelEmptyEntitiesTitle] = new("Content", Page: "BusinessModel", DefaultValue: "No entities yet"),
        [Content.BusinessModelEmptyEntitiesText] = new("Content", Page: "BusinessModel", DefaultValue: "An entity maps to a business table (e.g. Customer, Order); once resolved it can be queried by name in Ask."),
        [Content.BusinessModelNoMatch] = new("Content", Page: "BusinessModel", DefaultValue: "No matching entities"),
        [Content.BusinessModelDetail] = new("Content", Page: "BusinessModel", DefaultValue: "Details"),
        [Content.BusinessModelEditorNotReady] = new("Content", Page: "BusinessModel", DefaultValue: "The entity editor will be wired in stage S2."),

        [Content.BusinessModelEntityLoading] = new("Content", Page: "BusinessModelEntityDetail", DefaultValue: "Loading entity details…"),
        [Content.BusinessModelEntityPanelDetail] = new("Content", Page: "BusinessModelEntityDetail", DefaultValue: "Details"),
        [Content.BusinessModelEntityEmptyTitle] = new("Content", Page: "BusinessModelEntityDetail", DefaultValue: "Entity not found"),
        [Content.BusinessModelEntityEmptyText] = new("Content", Page: "BusinessModelEntityDetail", DefaultValue: "The entity may have been deleted, or the backend detail endpoint is not yet connected."),
        [Content.BusinessModelEntityPanelInfo] = new("Content", Page: "BusinessModelEntityDetail", DefaultValue: "Entity information"),
        [Content.BusinessModelEntityBack] = new("Content", Page: "BusinessModelEntityDetail", DefaultValue: "Back to semantic model"),

        [Content.DataSourcesNew] = new("Content", Page: "DataSources", DefaultValue: "Add data source"),
        [Content.DataSourcesMetricTotal] = new("Content", Page: "DataSources", DefaultValue: "Total data sources"),
        [Content.DataSourcesMetricOnline] = new("Content", Page: "DataSources", DefaultValue: "Online connections"),
        [Content.DataSourcesMetricTables] = new("Content", Page: "DataSources", DefaultValue: "Metadata tables"),
        [Content.DataSourcesMetricColumns] = new("Content", Page: "DataSources", DefaultValue: "Recognized fields"),
        [Content.DataSourcesPanelAssets] = new("Content", Page: "DataSources", DefaultValue: "Connections"),
        [Content.DataSourcesSearchPlaceholder] = new("Content", Page: "DataSources", DefaultValue: "Search by name or engine type…"),
        [Content.DataSourcesLoading] = new("Content", Page: "DataSources", DefaultValue: "Loading data sources…"),
        [Content.DataSourcesEmptyTitle] = new("Content", Page: "DataSources", DefaultValue: "No data sources yet"),
        [Content.DataSourcesEmptyText] = new("Content", Page: "DataSources", DefaultValue: "Create your first data source below."),
        [Content.DataSourcesColId] = new("Content", Page: "DataSources", DefaultValue: "ID"),
        [Content.DataSourcesColName] = new("Content", Page: "DataSources", DefaultValue: "Name"),
        [Content.DataSourcesColType] = new("Content", Page: "DataSources", DefaultValue: "Type"),
        [Content.DataSourcesColStatus] = new("Content", Page: "DataSources", DefaultValue: "Status"),
        [Content.DataSourcesColTables] = new("Content", Page: "DataSources", DefaultValue: "Tables"),
        [Content.DataSourcesColColumns] = new("Content", Page: "DataSources", DefaultValue: "Fields"),
        [Content.DataSourcesTenantScoped] = new("Content", Page: "DataSources", DefaultValue: "Tenant-scoped connection"),
        [Content.DataSourcesStatusOnline] = new("Content", Page: "DataSources", DefaultValue: "Running"),
        [Content.DataSourcesStatusOffline] = new("Content", Page: "DataSources", DefaultValue: "Disabled"),
        [Content.DataSourcesManage] = new("Content", Page: "DataSources", DefaultValue: "Manage connection →"),
        [Content.DataSourcesModalTitle] = new("Content", Page: "DataSources", DefaultValue: "Connect a new data source"),
        [Content.DataSourcesConnSqlServer] = new("Content", Page: "DataSources", DefaultValue: "Mainstream enterprise RDBMS"),
        [Content.DataSourcesConnMysql] = new("Content", Page: "DataSources", DefaultValue: "Common for WMS and similar"),
        [Content.DataSourcesConnPostgres] = new("Content", Page: "DataSources", DefaultValue: "Open-source RDBMS"),
        [Content.DataSourcesConnOracle] = new("Content", Page: "DataSources", DefaultValue: "Large-scale OLTP systems"),
        [Content.DataSourcesConnClickhouse] = new("Content", Page: "DataSources", DefaultValue: "Columnar analytics database"),
        [Content.DataSourcesConnMongodb] = new("Content", Page: "DataSources", DefaultValue: "Document NoSQL"),
        [Content.DataSourcesFieldName] = new("Content", Page: "DataSources", DefaultValue: "Data source name"),
        [Content.DataSourcesFieldNameHint] = new("Content", Page: "DataSources", DefaultValue: "A recognizable name for your team"),
        [Content.DataSourcesFieldNamePlaceholder] = new("Content", Page: "DataSources", DefaultValue: "e.g. WMS Production"),
        [Content.DataSourcesFieldType] = new("Content", Page: "DataSources", DefaultValue: "Connector type"),
        [Content.DataSourcesFieldConnStr] = new("Content", Page: "DataSources", DefaultValue: "Connection string"),
        [Content.DataSourcesFieldConnStrPlaceholder] = new("Content", Page: "DataSources", DefaultValue: "Server=host;Database=db;User=...;"),
        [Content.DataSourcesTestConn] = new("Content", Page: "DataSources", DefaultValue: "Test connection"),
        [Content.DataSourcesSaveAndConnect] = new("Content", Page: "DataSources", DefaultValue: "Save & connect"),
        [Content.DataSourcesLoadFailed] = new("Content", Page: "DataSources", DefaultValue: "Failed to load data sources."),
        [Content.DataSourcesConnectorSelected] = new("Content", Page: "DataSources", DefaultValue: "Connector selected: {0}"),
        [Content.DataSourcesTestSubmitted] = new("Content", Page: "DataSources", DefaultValue: "Connectivity test submitted (Multi-DB Connector backend planned for P12)."),
        [Content.DataSourcesRequiredError] = new("Content", Page: "DataSources", DefaultValue: "Data source name and connection string are required."),
        [Content.DataSourcesSaveFailed] = new("Content", Page: "DataSources", DefaultValue: "Failed to save data source."),
        [Content.DataSourcesSavedToast] = new("Content", Page: "DataSources", DefaultValue: "Data source saved and authorized for the current admin. It is now selectable on the Ask page."),
        [Content.DataSourcesSaving] = new("Content", Page: "DataSources", DefaultValue: "Saving…"),

        [Content.DataSourceBackList] = new("Content", Page: "DataSource", DefaultValue: "Back to list"),
        [Content.DataSourceTabMeta] = new("Content", Page: "DataSource", DefaultValue: "Metadata"),
        [Content.DataSourceRescan] = new("Content", Page: "DataSource", DefaultValue: "Re-scan"),
        [Content.DataSourceScanning] = new("Content", Page: "DataSource", DefaultValue: "Scanning…"),
        [Content.DataSourceLoadingMeta] = new("Content", Page: "DataSource", DefaultValue: "Loading metadata…"),
        [Content.DataSourceEmptyMetaTitle] = new("Content", Page: "DataSource", DefaultValue: "No metadata scanned yet"),
        [Content.DataSourceEmptyMetaText] = new("Content", Page: "DataSource", DefaultValue: "Click re-scan to read table and column structures from the business database."),
        [Content.DataSourceFieldsBadge] = new("Content", Page: "DataSource", DefaultValue: "{0} fields"),
        [Content.DataSourceColFieldRel] = new("Content", Page: "DataSource", DefaultValue: "Field / Relation"),
        [Content.DataSourceColType] = new("Content", Page: "DataSource", DefaultValue: "Type"),
        [Content.DataSourceColNullable] = new("Content", Page: "DataSource", DefaultValue: "Nullable"),
        [Content.DataSourceColSemantic] = new("Content", Page: "DataSource", DefaultValue: "Semantic"),
        [Content.DataSourceColVector] = new("Content", Page: "DataSource", DefaultValue: "Vector"),
        [Content.DataSourceColBusinessKey] = new("Content", Page: "DataSource", DefaultValue: "Business key"),
        [Content.DataSourceNotVectorized] = new("Content", Page: "DataSource", DefaultValue: "Not vectorized"),
        [Content.DataSourceTabGrants] = new("Content", Page: "DataSource", DefaultValue: "Access grants"),
        [Content.DataSourceGrantUser] = new("Content", Page: "DataSource", DefaultValue: "User"),
        [Content.DataSourceGrantRole] = new("Content", Page: "DataSource", DefaultValue: "Role"),
        [Content.DataSourceGrantSubjectPlaceholder] = new("Content", Page: "DataSource", DefaultValue: "Select a subject…"),
        [Content.DataSourceGrantAccess] = new("Content", Page: "DataSource", DefaultValue: "Grant access"),
        [Content.DataSourceRefreshGrants] = new("Content", Page: "DataSource", DefaultValue: "Refresh grants"),
        [Content.DataSourceLoadingGrants] = new("Content", Page: "DataSource", DefaultValue: "Loading grants…"),
        [Content.DataSourceEmptyGrantsTitle] = new("Content", Page: "DataSource", DefaultValue: "No explicit grants"),
        [Content.DataSourceEmptyGrantsText] = new("Content", Page: "DataSource", DefaultValue: "Select a user or role and grant access to this data source."),
        [Content.DataSourcePanelGrants] = new("Content", Page: "DataSource", DefaultValue: "Grant list"),
        [Content.DataSourceColSubjectType] = new("Content", Page: "DataSource", DefaultValue: "Subject type"),
        [Content.DataSourceColSubject] = new("Content", Page: "DataSource", DefaultValue: "Subject"),
        [Content.DataSourceColCreated] = new("Content", Page: "DataSource", DefaultValue: "Granted at"),
        [Content.DataSourceColActions] = new("Content", Page: "DataSource", DefaultValue: "Actions"),
        [Content.DataSourceRevoke] = new("Content", Page: "DataSource", DefaultValue: "Revoke"),
        [Content.DataSourceTabRls] = new("Content", Page: "DataSource", DefaultValue: "Row-level security"),
        [Content.DataSourceLoadingPolicies] = new("Content", Page: "DataSource", DefaultValue: "Loading policies…"),
        [Content.DataSourcePanelRls] = new("Content", Page: "DataSource", DefaultValue: "Row-level security policy"),
        [Content.DataSourceEmptyRlsTitle] = new("Content", Page: "DataSource", DefaultValue: "No row-level security policy"),
        [Content.DataSourceEmptyRlsText] = new("Content", Page: "DataSource", DefaultValue: "Once configured, visible rows are restricted by user/role, mitigating unauthorized data access."),
        [Content.DataSourcePanelPolicies] = new("Content", Page: "DataSource", DefaultValue: "Policy list"),
        [Content.DataSourceLoadMetaFailed] = new("Content", Page: "DataSource", DefaultValue: "Failed to load metadata ({0})."),
        [Content.DataSourceScanFailed] = new("Content", Page: "DataSource", DefaultValue: "Metadata scan failed."),
        [Content.DataSourceScanDone] = new("Content", Page: "DataSource", DefaultValue: "Metadata scan complete."),
        [Content.DataSourceSelectSubject] = new("Content", Page: "DataSource", DefaultValue: "Please select a user or role."),
        [Content.DataSourceGrantFailed] = new("Content", Page: "DataSource", DefaultValue: "Authorization failed."),
        [Content.DataSourceGranted] = new("Content", Page: "DataSource", DefaultValue: "Data source access granted."),
        [Content.DataSourceRevokeFailed] = new("Content", Page: "DataSource", DefaultValue: "Revoke failed."),
        [Content.DataSourceRevoked] = new("Content", Page: "DataSource", DefaultValue: "Access revoked."),
        [Content.DataSourceLoadPolicyFailed] = new("Content", Page: "DataSource", DefaultValue: "Failed to load policies ({0})."),
        [Content.DataSourcePolicyDeleted] = new("Content", Page: "DataSource", DefaultValue: "Policy deleted."),
        [Content.DataSourceDeleteFailed] = new("Content", Page: "DataSource", DefaultValue: "Delete failed ({0})."),
        [Content.DataSourceLoadGrantsFailed] = new("Content", Page: "DataSource", DefaultValue: "Failed to load grants ({0})."),

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

        // M3-05 重内容页正文批次（续）：ModelAccounts / SemanticLabel / ComponentGallery / ThemeEditor 补充 / MetadataEntity
        [Content.ModelAccountsBound] = new("Content", Page: "ModelAccounts", DefaultValue: "Bound"),
        [Content.ModelAccountsUnbound] = new("Content", Page: "ModelAccounts", DefaultValue: "Not bound"),
        [Content.ModelAccountsDefault] = new("Content", Page: "ModelAccounts", DefaultValue: "Default"),
        [Content.ModelAccountsSetDefault] = new("Content", Page: "ModelAccounts", DefaultValue: "Set as default"),
        [Content.ModelAccountsBindPanel] = new("Content", Page: "ModelAccounts", DefaultValue: "Bind your own API Key"),
        [Content.ModelAccountsFieldModel] = new("Content", Page: "ModelAccounts", DefaultValue: "Model"),
        [Content.ModelAccountsFieldApiKey] = new("Content", Page: "ModelAccounts", DefaultValue: "API Key"),
        [Content.ModelAccountsApiKeyPlaceholder] = new("Content", Page: "ModelAccounts", DefaultValue: "sk-••••••"),
        [Content.ModelAccountsFieldNote] = new("Content", Page: "ModelAccounts", DefaultValue: "Note"),
        [Content.ModelAccountsNotePlaceholder] = new("Content", Page: "ModelAccounts", DefaultValue: "e.g. Production only"),
        [Content.ModelAccountsSaveBind] = new("Content", Page: "ModelAccounts", DefaultValue: "Save binding"),
        [Content.ModelAccountsSetDefaultToast] = new("Content", Page: "ModelAccounts", DefaultValue: "Set {0} as default model"),
        [Content.ModelAccountsBindSubmitted] = new("Content", Page: "ModelAccounts", DefaultValue: "Binding submitted (ILLMProvider + UserModelBinding planned for P13; key will be stored masked)."),

        [Content.SemanticLabelLoading] = new("Content", Page: "SemanticLabelDetail", DefaultValue: "Loading label details…"),
        [Content.SemanticLabelPanelDetail] = new("Content", Page: "SemanticLabelDetail", DefaultValue: "Details"),
        [Content.SemanticLabelNotFoundTitle] = new("Content", Page: "SemanticLabelDetail", DefaultValue: "Label not found"),
        [Content.SemanticLabelNotFoundText] = new("Content", Page: "SemanticLabelDetail", DefaultValue: "The label may have been deleted, or the backend detail endpoint is not yet connected."),
        [Content.SemanticLabelInfoPanel] = new("Content", Page: "SemanticLabelDetail", DefaultValue: "Label information"),
        [Content.SemanticLabelBack] = new("Content", Page: "SemanticLabelDetail", DefaultValue: "Back to list"),

        [Content.ComponentGallerySecButtons] = new("Content", Page: "ComponentGallery", DefaultValue: "Buttons"),
        [Content.ComponentGallerySecBadges] = new("Content", Page: "ComponentGallery", DefaultValue: "Badges"),
        [Content.ComponentGallerySecForms] = new("Content", Page: "ComponentGallery", DefaultValue: "Form controls"),
        [Content.ComponentGallerySecProgress] = new("Content", Page: "ComponentGallery", DefaultValue: "Progress bar"),
        [Content.ComponentGallerySecSegmented] = new("Content", Page: "ComponentGallery", DefaultValue: "Segmented control"),
        [Content.ComponentGallerySecAlerts] = new("Content", Page: "ComponentGallery", DefaultValue: "Alerts"),
        [Content.ComponentGallerySecTabs] = new("Content", Page: "ComponentGallery", DefaultValue: "Tabs SbTabs"),
        [Content.ComponentGallerySecDataTable] = new("Content", Page: "ComponentGallery", DefaultValue: "Data table SbDataTable"),
        [Content.ComponentGallerySecModal] = new("Content", Page: "ComponentGallery", DefaultValue: "Modal SbModal / SbConfirm"),
        [Content.ComponentGallerySecToast] = new("Content", Page: "ComponentGallery", DefaultValue: "Toast"),
        [Content.ComponentGallerySecGuard] = new("Content", Page: "ComponentGallery", DefaultValue: "Guard"),
        [Content.ComponentGalleryBtnPrimary] = new("Content", Page: "ComponentGallery", DefaultValue: "Primary"),
        [Content.ComponentGalleryBtnSecondary] = new("Content", Page: "ComponentGallery", DefaultValue: "Secondary"),
        [Content.ComponentGalleryBtnGhost] = new("Content", Page: "ComponentGallery", DefaultValue: "Ghost"),
        [Content.ComponentGalleryBtnDanger] = new("Content", Page: "ComponentGallery", DefaultValue: "Danger"),
        [Content.ComponentGalleryBtnDisabled] = new("Content", Page: "ComponentGallery", DefaultValue: "Disabled"),
        [Content.ComponentGalleryBadgePrimary] = new("Content", Page: "ComponentGallery", DefaultValue: "Primary"),
        [Content.ComponentGalleryBadgePublished] = new("Content", Page: "ComponentGallery", DefaultValue: "Published"),
        [Content.ComponentGalleryBadgeDraft] = new("Content", Page: "ComponentGallery", DefaultValue: "Draft"),
        [Content.ComponentGalleryBadgeFailed] = new("Content", Page: "ComponentGallery", DefaultValue: "Failed"),
        [Content.ComponentGalleryBadgeGlobal] = new("Content", Page: "ComponentGallery", DefaultValue: "Global"),
        [Content.ComponentGalleryBadgeDefault] = new("Content", Page: "ComponentGallery", DefaultValue: "Default"),
        [Content.ComponentGalleryFormTextbox] = new("Content", Page: "ComponentGallery", DefaultValue: "Text box"),
        [Content.ComponentGalleryFormDropdown] = new("Content", Page: "ComponentGallery", DefaultValue: "Dropdown"),
        [Content.ComponentGalleryOptA] = new("Content", Page: "ComponentGallery", DefaultValue: "Option A"),
        [Content.ComponentGalleryOptB] = new("Content", Page: "ComponentGallery", DefaultValue: "Option B"),
        [Content.ComponentGalleryProgressUsed] = new("Content", Page: "ComponentGallery", DefaultValue: "Used {0}"),
        [Content.ComponentGallerySegDay] = new("Content", Page: "ComponentGallery", DefaultValue: "Day"),
        [Content.ComponentGallerySegWeek] = new("Content", Page: "ComponentGallery", DefaultValue: "Week"),
        [Content.ComponentGallerySegMonth] = new("Content", Page: "ComponentGallery", DefaultValue: "Month"),
        [Content.ComponentGalleryAlertInfo] = new("Content", Page: "ComponentGallery", DefaultValue: "Information"),
        [Content.ComponentGalleryAlertWarning] = new("Content", Page: "ComponentGallery", DefaultValue: "Warning"),
        [Content.ComponentGalleryStatQuestions] = new("Content", Page: "ComponentGallery", DefaultValue: "Questions today"),
        [Content.ComponentGalleryStatHitRate] = new("Content", Page: "ComponentGallery", DefaultValue: "Hit rate"),
        [Content.ComponentGalleryStatPending] = new("Content", Page: "ComponentGallery", DefaultValue: "Pending review"),
        [Content.ComponentGalleryStatActiveTenants] = new("Content", Page: "ComponentGallery", DefaultValue: "Active tenants"),
        [Content.ComponentGallerySampleTable] = new("Content", Page: "ComponentGallery", DefaultValue: "Example table"),
        [Content.ComponentGalleryColCode] = new("Content", Page: "ComponentGallery", DefaultValue: "Code"),
        [Content.ComponentGalleryColName] = new("Content", Page: "ComponentGallery", DefaultValue: "Name"),
        [Content.ComponentGalleryColStatus] = new("Content", Page: "ComponentGallery", DefaultValue: "Status"),
        [Content.ComponentGalleryColTenant] = new("Content", Page: "ComponentGallery", DefaultValue: "Tenant"),
        [Content.ComponentGalleryCardAskTitle] = new("Content", Page: "ComponentGallery", DefaultValue: "Ask BI"),
        [Content.ComponentGalleryCardAskDesc] = new("Content", Page: "ComponentGallery", DefaultValue: "The flagship entry for natural-language analytics."),
        [Content.ComponentGalleryCardDashTitle] = new("Content", Page: "ComponentGallery", DefaultValue: "Dashboards"),
        [Content.ComponentGalleryCardDashDesc] = new("Content", Page: "ComponentGallery", DefaultValue: "Reusable visualization dashboards."),
        [Content.ComponentGalleryCardAppTitle] = new("Content", Page: "ComponentGallery", DefaultValue: "App Factory"),
        [Content.ComponentGalleryCardAppDesc] = new("Content", Page: "ComponentGallery", DefaultValue: "Turn conversations into BI apps."),
        [Content.ComponentGalleryTabOverview] = new("Content", Page: "ComponentGallery", DefaultValue: "Overview"),
        [Content.ComponentGalleryTabDetail] = new("Content", Page: "ComponentGallery", DefaultValue: "Details"),
        [Content.ComponentGalleryTabSettings] = new("Content", Page: "ComponentGallery", DefaultValue: "Settings"),
        [Content.ComponentGalleryDataEmpty] = new("Content", Page: "ComponentGallery", DefaultValue: "No data"),
        [Content.ComponentGalleryModalTitle] = new("Content", Page: "ComponentGallery", DefaultValue: "Example modal"),
        [Content.ComponentGalleryOpenModal] = new("Content", Page: "ComponentGallery", DefaultValue: "Open modal"),
        [Content.ComponentGalleryModalBody] = new("Content", Page: "ComponentGallery", DefaultValue: "Modals host forms or details, supporting backdrop-click close and custom footer actions."),
        [Content.ComponentGalleryModalOk] = new("Content", Page: "ComponentGallery", DefaultValue: "Got it"),
        [Content.ComponentGalleryConfirmTitle] = new("Content", Page: "ComponentGallery", DefaultValue: "Delete confirmation"),
        [Content.ComponentGalleryConfirmMsg] = new("Content", Page: "ComponentGallery", DefaultValue: "This action cannot be undone. Are you sure you want to delete this record?"),
        [Content.ComponentGalleryConfirmText] = new("Content", Page: "ComponentGallery", DefaultValue: "Delete"),
        [Content.ComponentGalleryToastInfo] = new("Content", Page: "ComponentGallery", DefaultValue: "Information"),
        [Content.ComponentGalleryToastSuccess] = new("Content", Page: "ComponentGallery", DefaultValue: "Operation succeeded"),
        [Content.ComponentGalleryToastWarning] = new("Content", Page: "ComponentGallery", DefaultValue: "Please note"),
        [Content.ComponentGalleryToastError] = new("Content", Page: "ComponentGallery", DefaultValue: "Something went wrong"),
        [Content.ComponentGalleryToastDeleted] = new("Content", Page: "ComponentGallery", DefaultValue: "Delete confirmed (demo)."),
        [Content.ComponentGalleryGuardDesc] = new("Content", Page: "ComponentGallery", DefaultValue: "AuthGuard renders children after session bootstrap; PermissionGuard gates by permission code (not enforced yet; see NavMenuItems.EnforcePermissions)."),
        [Content.ComponentGalleryGuardHasPerm] = new("Content", Page: "ComponentGallery", DefaultValue: "You have demo:write permission"),

        [Content.ThemeEditorSampleMetric] = new("Content", Page: "ThemeEditor", DefaultValue: "Sample metric"),
        [Content.ThemeEditorBtnPrimary] = new("Content", Page: "ThemeEditor", DefaultValue: "Primary button"),
        [Content.ThemeEditorBtnSecondary] = new("Content", Page: "ThemeEditor", DefaultValue: "Secondary"),
        [Content.ThemeEditorBadgePublished] = new("Content", Page: "ThemeEditor", DefaultValue: "Published"),
        [Content.ThemeEditorBadgeDraft] = new("Content", Page: "ThemeEditor", DefaultValue: "Draft"),
        [Content.ThemeEditorSavedToast] = new("Content", Page: "ThemeEditor", DefaultValue: "Theme saved (persistence to api/themes, closing in P11.3)."),

        [Content.MetadataEntityLoading] = new("Content", Page: "MetadataEntityDetail", DefaultValue: "Reading metadata entity…"),
        [Content.MetadataEntityIntro] = new("Content", Page: "MetadataEntityDetail", DefaultValue: "The following content comes from the current tenant's actual metadata records."),
        [Content.MetadataEntityNotFound] = new("Content", Page: "MetadataEntityDetail", DefaultValue: "No corresponding data found ({0})."),
        // M3-05 递延项（Task #83）：分析页正文键 en-US 基线
        [Content.AskTitle] = new("Content", Page: "Ask", DefaultValue: "Ask BI — Natural-language Analytics (Flagship)"),
        [Content.AskRestoringSession] = new("Content", Page: "Ask", DefaultValue: "Restoring sign-in session…"),
        [Content.AskLoginHint] = new("Content", Page: "Ask", DefaultValue: "Please "),
        [Content.AskLoginLink] = new("Content", Page: "Ask", DefaultValue: "sign in"),
        [Content.AskLoginHint2] = new("Content", Page: "Ask", DefaultValue: " to ask a question."),
        [Content.AskDataScope] = new("Content", Page: "Ask", DefaultValue: "Accessible data scope"),
        [Content.AskDataLoading] = new("Content", Page: "Ask", DefaultValue: "Loading…"),
        [Content.AskQuestionLabel] = new("Content", Page: "Ask", DefaultValue: "Natural-language question"),
        [Content.AskQuestionPlaceholder] = new("Content", Page: "Ask", DefaultValue: "e.g. Compare regional sales last month"),
        [Content.AskButtonAsk] = new("Content", Page: "Ask", DefaultValue: "Ask"),
        [Content.AskButtonClear] = new("Content", Page: "Ask", DefaultValue: "Clear chat"),
        [Content.AskHistoryRestored] = new("Content", Page: "Ask", DefaultValue: "Restored {0} prior turn(s)"),
        [Content.AskNoConversation] = new("Content", Page: "Ask", DefaultValue: "No conversation yet. Try asking a business question, e.g. \"Top 10 product categories this month\"."),
        [Content.AskDataSourceListEmpty] = new("Content", Page: "Ask", DefaultValue: "No data sources available"),
        [Content.AskNoDataSources] = new("Content", Page: "Ask", DefaultValue: "Your account has no data sources available yet"),
        [Content.AskDataSourceLoadFailed] = new("Content", Page: "Ask", DefaultValue: "Failed to load data sources: {0}"),
        [Content.AskEnterQuestion] = new("Content", Page: "Ask", DefaultValue: "Please enter a question."),
        [Content.AskNoAuthorizedDataSource] = new("Content", Page: "Ask", DefaultValue: "Your account is not authorized for any data source. Please contact your tenant administrator."),
        [Content.AskEnterRefineInstruction] = new("Content", Page: "Ask", DefaultValue: "Please enter a refinement instruction."),
        [Content.AskRefineBasedOnResult] = new("Content", Page: "Ask", DefaultValue: "Re-queried based on this turn's result (multi-turn semantic refinement)."),
        [Content.AskRefineException] = new("Content", Page: "Ask", DefaultValue: "Refinement error: {0}"),
        [Content.AskFailed] = new("Content", Page: "Ask", DefaultValue: "Query failed."),
        [Content.AskNoValidResult] = new("Content", Page: "Ask", DefaultValue: "The query returned no valid result."),
        [Content.AskPublishedAppNamePrefix] = new("Content", Page: "Ask", DefaultValue: "Ask app: "),
        [Content.AskChartTitleDefault] = new("Content", Page: "Ask", DefaultValue: "Result chart"),
        [Content.AskTableTitleDefault] = new("Content", Page: "Ask", DefaultValue: "Detail data"),
        [Content.AskAiSummaryTitle] = new("Content", Page: "Ask", DefaultValue: "AI insight"),
        [Content.AskPublishSuccess] = new("Content", Page: "Ask", DefaultValue: "Published as app: {0}"),
        [Content.AskPublishFailed] = new("Content", Page: "Ask", DefaultValue: "Publish failed: {0}"),
        [Content.AskPublishException] = new("Content", Page: "Ask", DefaultValue: "Publish error: {0}"),
        [Content.DashboardsTitle] = new("Content", Page: "Dashboards", DefaultValue: "Dashboards"),
        [Content.DashboardsDesc] = new("Content", Page: "Dashboards", DefaultValue: "Turn Ask analysis results into reusable visual dashboards, with team sharing and scheduled refresh."),
        [Content.DashboardsSearchPlaceholder] = new("Content", Page: "Dashboards", DefaultValue: "Search by name / code…"),
        [Content.DashboardsEmptyTitle] = new("Content", Page: "Dashboards", DefaultValue: "No dashboards yet"),
        [Content.DashboardsEmptyText] = new("Content", Page: "Dashboards", DefaultValue: "Save an Ask result as a dashboard to reuse this analysis perspective and charts across your team."),
        [Content.DashboardsLoading] = new("Content", Page: "Dashboards", DefaultValue: "Loading dashboards…"),
        [Content.DashboardsNew] = new("Content", Page: "Dashboards", DefaultValue: "New dashboard"),
        [Content.DashboardsStatTotal] = new("Content", Page: "Dashboards", DefaultValue: "Total dashboards"),
        [Content.DashboardsStatTotalSub] = new("Content", Page: "Dashboards", DefaultValue: "Includes global templates"),
        [Content.DashboardsStatPublished] = new("Content", Page: "Dashboards", DefaultValue: "Published"),
        [Content.DashboardsStatPublishedSub] = new("Content", Page: "Dashboards", DefaultValue: "Shareable externally"),
        [Content.DashboardsStatDraft] = new("Content", Page: "Dashboards", DefaultValue: "Draft"),
        [Content.DashboardsStatDraftSub] = new("Content", Page: "Dashboards", DefaultValue: "In editing"),
        [Content.DashboardsPublishFromAsk] = new("Content", Page: "Dashboards", DefaultValue: "Publish from Ask"),
        [Content.DashboardsRowDetail] = new("Content", Page: "Dashboards", DefaultValue: "Details"),
        [Content.DashboardsRowDelete] = new("Content", Page: "Dashboards", DefaultValue: "Delete"),
        [Content.DashboardsDeleteConfirmMsg] = new("Content", Page: "Dashboards", DefaultValue: "This cannot be undone. Are you sure you want to delete this dashboard?"),
        [Content.DashboardsDeleteConfirmText] = new("Content", Page: "Dashboards", DefaultValue: "Delete"),
        [Content.DashboardsFieldStatus] = new("Content", Page: "Dashboards", DefaultValue: "Status"),
        [Content.DashboardsStatusDraft] = new("Content", Page: "Dashboards", DefaultValue: "Draft"),
        [Content.DashboardsStatusPublished] = new("Content", Page: "Dashboards", DefaultValue: "Published"),
        [Content.DashboardsFieldDslJson] = new("Content", Page: "Dashboards", DefaultValue: "DslJson"),
        [Content.DashboardsFieldDslJsonHint] = new("Content", Page: "Dashboards", DefaultValue: "Declarative dashboard definition (DashboardDsl JSON). It will be validated before creation."),
        [Content.DashboardsModalCancel] = new("Content", Page: "Dashboards", DefaultValue: "Cancel"),
        [Content.DashboardsModalCreate] = new("Content", Page: "Dashboards", DefaultValue: "Create"),
        [Content.DashboardsMissingIdOpen] = new("Content", Page: "Dashboards", DefaultValue: "This record is missing an id and cannot be opened."),
        [Content.DashboardsMissingIdDelete] = new("Content", Page: "Dashboards", DefaultValue: "This record is missing an id and cannot be deleted."),
        [Content.DashboardsDeleteSuccess] = new("Content", Page: "Dashboards", DefaultValue: "Dashboard deleted."),
        [Content.DashboardsDeleteFailed] = new("Content", Page: "Dashboards", DefaultValue: "Deletion failed."),
        [Content.DashboardsBlueprintUnavailable] = new("Content", Page: "Dashboards", DefaultValue: "Could not fetch the blueprint skeleton. Please fill in the DslJson manually."),
        [Content.DashboardsDslJsonEmpty] = new("Content", Page: "Dashboards", DefaultValue: "DslJson cannot be empty."),
        [Content.DashboardsCreateSuccess] = new("Content", Page: "Dashboards", DefaultValue: "Dashboard created."),
        [Content.DashboardsCreateFailed] = new("Content", Page: "Dashboards", DefaultValue: "Creation failed (HTTP {0})."),
        [Content.DashboardsLoadFailed] = new("Content", Page: "Dashboards", DefaultValue: "Failed to load."),
        [Content.AppsTitle] = new("Content", Page: "Apps", DefaultValue: "App Factory"),
        [Content.AppsDesc] = new("Content", Page: "Apps", DefaultValue: "Distill ask sessions into reusable BI apps: visual editor, DSL blueprint, and one-click publishing."),
        [Content.AppsSearchPlaceholder] = new("Content", Page: "Apps", DefaultValue: "Search by app code / name…"),
        [Content.AppsEmptyTitle] = new("Content", Page: "Apps", DefaultValue: "No apps yet"),
        [Content.AppsEmptyText] = new("Content", Page: "Apps", DefaultValue: "An app wraps ask capability: freeze a typical analysis into cards, charts, and filters to share and reuse with your team."),
        [Content.AppsLoading] = new("Content", Page: "Apps", DefaultValue: "Loading apps…"),
        [Content.AppsPanelTitle] = new("Content", Page: "Apps", DefaultValue: "App list"),
        [Content.AppsNew] = new("Content", Page: "Apps", DefaultValue: "New app"),
        [Content.AppsFromAsk] = new("Content", Page: "Apps", DefaultValue: "Publish from Ask"),
        [Content.AppsStatTotal] = new("Content", Page: "Apps", DefaultValue: "Total apps"),
        [Content.AppsStatTotalSub] = new("Content", Page: "Apps", DefaultValue: "Includes global templates"),
        [Content.AppsStatGlobal] = new("Content", Page: "Apps", DefaultValue: "Global template"),
        [Content.AppsStatGlobalSub] = new("Content", Page: "Apps", DefaultValue: "Built-in platform"),
        [Content.AppsStatTenant] = new("Content", Page: "Apps", DefaultValue: "Tenant apps"),
        [Content.AppsStatTenantSub] = new("Content", Page: "Apps", DefaultValue: "Built by this tenant"),
        [Content.AppsNoDescription] = new("Content", Page: "Apps", DefaultValue: "(No description)"),
        [Content.AppsBadgeGlobal] = new("Content", Page: "Apps", DefaultValue: "Global template"),
        [Content.AppsBadgeTenant] = new("Content", Page: "Apps", DefaultValue: "Tenant"),
        [Content.AppsEdit] = new("Content", Page: "Apps", DefaultValue: "Edit"),
        [Content.AppsDelete] = new("Content", Page: "Apps", DefaultValue: "Delete"),
        [Content.AppsDeleteConfirmTitle] = new("Content", Page: "Apps", DefaultValue: "Delete app"),
        [Content.AppsDeleteConfirmMsg] = new("Content", Page: "Apps", DefaultValue: "This cannot be undone. Are you sure you want to delete this app?"),
        [Content.AppsDeleteConfirmText] = new("Content", Page: "Apps", DefaultValue: "Delete"),
        [Content.AppsGenDescLabel] = new("Content", Page: "Apps", DefaultValue: "App description"),
        [Content.AppsGenDescHint] = new("Content", Page: "Apps", DefaultValue: "Describe the BI app you want in one sentence; the AI will generate the DSL."),
        [Content.AppsGenDescPlaceholder] = new("Content", Page: "Apps", DefaultValue: "e.g. Sales dashboard showing sales and order volume by region"),
        [Content.AppsGenCodeLabel] = new("Content", Page: "Apps", DefaultValue: "App code"),
        [Content.AppsGenCodeHint] = new("Content", Page: "Apps", DefaultValue: "Leave blank to let the system generate one (e.g. my-app-1733)."),
        [Content.AppsGenCodePlaceholder] = new("Content", Page: "Apps", DefaultValue: "Optional"),
        [Content.AppsGenCancel] = new("Content", Page: "Apps", DefaultValue: "Cancel"),
        [Content.AppsGenSubmit] = new("Content", Page: "Apps", DefaultValue: "Generate & create"),
        [Content.AppsDslIntro] = new("Content", Page: "Apps", DefaultValue: "Declarative definition of app {0} (AppDsl JSON). Saving re-validates and overwrites."),
        [Content.AppsDslGlobalWarning] = new("Content", Page: "Apps", DefaultValue: "This app is a global template and cannot be modified."),
        [Content.AppsDslFieldLabel] = new("Content", Page: "Apps", DefaultValue: "DslJson"),
        [Content.AppsDslSave] = new("Content", Page: "Apps", DefaultValue: "Save"),
        [Content.AppsMissingCodeOpen] = new("Content", Page: "Apps", DefaultValue: "This record is missing a code and cannot be opened."),
        [Content.AppsMissingCodeEdit] = new("Content", Page: "Apps", DefaultValue: "This record is missing a code and cannot be edited."),
        [Content.AppsMissingCodeDelete] = new("Content", Page: "Apps", DefaultValue: "This record is missing a code and cannot be deleted."),
        [Content.AppsGenDescRequired] = new("Content", Page: "Apps", DefaultValue: "App description is required."),
        [Content.AppsGenSuccess] = new("Content", Page: "Apps", DefaultValue: "App generated by AI and created."),
        [Content.AppsGenFailed] = new("Content", Page: "Apps", DefaultValue: "Generation failed (HTTP {0})."),
        [Content.AppsReadFailed] = new("Content", Page: "Apps", DefaultValue: "Failed to read the app."),
        [Content.AppsDslJsonEmpty] = new("Content", Page: "Apps", DefaultValue: "DslJson cannot be empty."),
        [Content.AppsDslSaved] = new("Content", Page: "Apps", DefaultValue: "App DSL saved."),
        [Content.AppsDslSaveFailed] = new("Content", Page: "Apps", DefaultValue: "Save failed (HTTP {0})."),
        [Content.AppsDeleteSuccess] = new("Content", Page: "Apps", DefaultValue: "App deleted."),
        [Content.AppsDeleteFailed] = new("Content", Page: "Apps", DefaultValue: "Delete failed."),
        [Content.AppsLoadFailed] = new("Content", Page: "Apps", DefaultValue: "Failed to load."),
        [Content.AppDetailDesc] = new("Content", Page: "AppDetail", DefaultValue: "App details: DSL structure, pages, and component manifest."),
        [Content.AppDetailBack] = new("Content", Page: "AppDetail", DefaultValue: "Back to list"),
        [Content.AppDetailRefresh] = new("Content", Page: "AppDetail", DefaultValue: "Refresh"),
        [Content.AppDetailDelete] = new("Content", Page: "AppDetail", DefaultValue: "Delete"),
        [Content.AppDetailLoading] = new("Content", Page: "AppDetail", DefaultValue: "Loading app…"),
        [Content.AppDetailTabOverview] = new("Content", Page: "AppDetail", DefaultValue: "Overview"),
        [Content.AppDetailPanelBasic] = new("Content", Page: "AppDetail", DefaultValue: "Basic info"),
        [Content.AppDetailTabComponents] = new("Content", Page: "AppDetail", DefaultValue: "Pages & components"),
        [Content.AppDetailPanelComponents] = new("Content", Page: "AppDetail", DefaultValue: "Component manifest"),
        [Content.AppDetailEmptyComponentsTitle] = new("Content", Page: "AppDetail", DefaultValue: "No components resolved"),
        [Content.AppDetailEmptyComponentsText] = new("Content", Page: "AppDetail", DefaultValue: "The app's DSL contains no resolvable component definitions."),
        [Content.AppDetailTabDsl] = new("Content", Page: "AppDetail", DefaultValue: "DSL"),
        [Content.AppDetailPanelDsl] = new("Content", Page: "AppDetail", DefaultValue: "App DSL (read-only)"),
        [Content.AppDetailDeleteConfirmTitle] = new("Content", Page: "AppDetail", DefaultValue: "Delete app"),
        [Content.AppDetailDeleteConfirmMsg] = new("Content", Page: "AppDetail", DefaultValue: "Delete app \"{0}\"? This action cannot be undone."),
        [Content.AppDetailDeleteConfirmText] = new("Content", Page: "AppDetail", DefaultValue: "Delete"),
        [Content.AppDetailTitleDefault] = new("Content", Page: "AppDetail", DefaultValue: "App details"),
        [Content.AppDetailDeleteSuccess] = new("Content", Page: "AppDetail", DefaultValue: "App deleted."),
        [Content.AppDetailDeleteFailed] = new("Content", Page: "AppDetail", DefaultValue: "Delete failed (HTTP {0})."),
        [Content.AppDetailLoadFailed] = new("Content", Page: "AppDetail", DefaultValue: "Failed to load (HTTP {0})."),
        [Content.AskTurnMe] = new("Content", Page: "AskTurn", DefaultValue: "Me"),
        [Content.AskTurnAiSummary] = new("Content", Page: "AskTurn", DefaultValue: "AI insight"),
        [Content.AskTurnMeta] = new("Content", Page: "AskTurn", DefaultValue: "Elapsed {0} ms · {1} suggested chart(s)"),
        [Content.AskTurnChartDefault] = new("Content", Page: "AskTurn", DefaultValue: "Chart {0}"),
        [Content.AskTurnViewLabel] = new("Content", Page: "AskTurn", DefaultValue: "View: "),
        [Content.AskTurnBar] = new("Content", Page: "AskTurn", DefaultValue: "Bar"),
        [Content.AskTurnLine] = new("Content", Page: "AskTurn", DefaultValue: "Line"),
        [Content.AskTurnPie] = new("Content", Page: "AskTurn", DefaultValue: "Pie"),
        [Content.AskTurnHideLegend] = new("Content", Page: "AskTurn", DefaultValue: "Hide legend"),
        [Content.AskTurnShowLegend] = new("Content", Page: "AskTurn", DefaultValue: "Show legend"),
        [Content.AskTurnCyclePalette] = new("Content", Page: "AskTurn", DefaultValue: "Cycle palette"),
        [Content.AskTurnStacked] = new("Content", Page: "AskTurn", DefaultValue: "Stacked"),
        [Content.AskTurnUnstacked] = new("Content", Page: "AskTurn", DefaultValue: "Unstacked"),
        [Content.AskTurnArea] = new("Content", Page: "AskTurn", DefaultValue: "Area"),
        [Content.AskTurnUnarea] = new("Content", Page: "AskTurn", DefaultValue: "Unarea"),
        [Content.AskTurnMultiAxis] = new("Content", Page: "AskTurn", DefaultValue: "Multi-axis"),
        [Content.AskTurnUnmulti] = new("Content", Page: "AskTurn", DefaultValue: "Unmulti"),
        [Content.AskTurnRowsTotal] = new("Content", Page: "AskTurn", DefaultValue: "Total {0} row(s)"),
        [Content.AskTurnDrill] = new("Content", Page: "AskTurn", DefaultValue: "Drill down: "),
        [Content.AskTurnClearDrill] = new("Content", Page: "AskTurn", DefaultValue: "Clear drill"),
        [Content.AskTurnExportCsv] = new("Content", Page: "AskTurn", DefaultValue: "Export CSV"),
        [Content.AskTurnExportExcel] = new("Content", Page: "AskTurn", DefaultValue: "Export Excel"),
        [Content.AskTurnQueryFailed] = new("Content", Page: "AskTurn", DefaultValue: "Query failed: {0}"),
        [Content.AskTurnViewSql] = new("Content", Page: "AskTurn", DefaultValue: "View generated SQL"),
        [Content.AskTurnHideCompare] = new("Content", Page: "AskTurn", DefaultValue: "Hide compare"),
        [Content.AskTurnShowCompare] = new("Content", Page: "AskTurn", DefaultValue: "Compare with original"),
        [Content.AskTurnOriginalResult] = new("Content", Page: "AskTurn", DefaultValue: "Original · {0}"),
        [Content.AskTurnRefinedResult] = new("Content", Page: "AskTurn", DefaultValue: "Refined · {0}"),
        [Content.AskTurnRefinePlaceholder] = new("Content", Page: "AskTurn", DefaultValue: "e.g. change to bar / hide legend / switch palette to green"),
        [Content.AskTurnApplyAdjust] = new("Content", Page: "AskTurn", DefaultValue: "Apply adjustment"),
        [Content.AskTurnSemanticPlaceholder] = new("Content", Page: "AskTurn", DefaultValue: "Follow up on this result, e.g. only East China / group by month"),
        [Content.AskTurnSemanticRefine] = new("Content", Page: "AskTurn", DefaultValue: "Semantic refine"),
        [Content.AskTurnRefining] = new("Content", Page: "AskTurn", DefaultValue: "Querying…"),
        [Content.AskTurnPublish] = new("Content", Page: "AskTurn", DefaultValue: "Publish as app"),
        [Content.AskTurnPublishing] = new("Content", Page: "AskTurn", DefaultValue: "Publishing…"),
        [Content.AskTurnRefineNoCmd] = new("Content", Page: "AskTurn", DefaultValue: "Please enter an adjustment instruction."),
        [Content.AskTurnRefineNoViz] = new("Content", Page: "AskTurn", DefaultValue: "This turn has no visualization result."),
        [Content.AskTurnRefineUnrecognized] = new("Content", Page: "AskTurn", DefaultValue: "Unrecognized instruction (supported: bar/line/pie, show/hide legend, palette green/orange/red/gray/blue)."),
        [Content.AskTurnRefineApplied] = new("Content", Page: "AskTurn", DefaultValue: "View adjustment applied (front-end only, no backend request)."),
        [Content.CommonRefresh] = new("Content", Page: "Common", DefaultValue: "Refresh"),
        [Content.CommonCancel] = new("Content", Page: "Common", DefaultValue: "Cancel"),
        [Content.CommonCreate] = new("Content", Page: "Common", DefaultValue: "Create"),
        [Content.CommonSave] = new("Content", Page: "Common", DefaultValue: "Save"),
        [Content.CommonAdd] = new("Content", Page: "Common", DefaultValue: "Add"),
        [Content.CommonUsername] = new("Content", Page: "Common", DefaultValue: "Username"),
        [Content.CommonDisplayName] = new("Content", Page: "Common", DefaultValue: "Display Name"),
        [Content.CommonEmail] = new("Content", Page: "Common", DefaultValue: "Email"),
        [Content.CommonName] = new("Content", Page: "Common", DefaultValue: "Name"),
        [Content.CommonDescription] = new("Content", Page: "Common", DefaultValue: "Description"),
        [Content.CommonRole] = new("Content", Page: "Common", DefaultValue: "Role"),
        [Content.CommonUser] = new("Content", Page: "Common", DefaultValue: "User"),
        [Content.CommonPermission] = new("Content", Page: "Common", DefaultValue: "Permission"),
        [Content.CommonPermissions] = new("Content", Page: "Common", DefaultValue: "Permissions"),
        [Content.CommonRoleCode] = new("Content", Page: "Common", DefaultValue: "Role Code"),
        [Content.CommonSelectPlaceholder] = new("Content", Page: "Common", DefaultValue: "— Select —"),
        [Content.CommonOptional] = new("Content", Page: "Common", DefaultValue: "Optional"),
        [Content.AdminIdentityTitle] = new("Content", Page: "Identity", DefaultValue: "Identity & Permissions"),
        [Content.AdminIdentityDesc] = new("Content", Page: "Identity", DefaultValue: "Manage users, roles and permission assignment with tenant-scoped RBAC."),
        [Content.AdminIdentityCreateUser] = new("Content", Page: "Identity", DefaultValue: "New User"),
        [Content.AdminIdentityCreateRole] = new("Content", Page: "Identity", DefaultValue: "New Role"),
        [Content.AdminIdentityLoading] = new("Content", Page: "Identity", DefaultValue: "Loading identity & permissions…"),
        [Content.AdminIdentityStatUsers] = new("Content", Page: "Identity", DefaultValue: "Users"),
        [Content.AdminIdentityStatUsersSub] = new("Content", Page: "Identity", DefaultValue: "Current tenant"),
        [Content.AdminIdentityStatRoles] = new("Content", Page: "Identity", DefaultValue: "Roles"),
        [Content.AdminIdentityStatRolesSub] = new("Content", Page: "Identity", DefaultValue: "RBAC"),
        [Content.AdminIdentityStatPerms] = new("Content", Page: "Identity", DefaultValue: "Permissions"),
        [Content.AdminIdentityStatPermsSub] = new("Content", Page: "Identity", DefaultValue: "Grantable"),
        [Content.AdminIdentityPanelUsers] = new("Content", Page: "Identity", DefaultValue: "Users"),
        [Content.AdminIdentityEmptyUsers] = new("Content", Page: "Identity", DefaultValue: "No users yet"),
        [Content.AdminIdentityEmptyUsersText] = new("Content", Page: "Identity", DefaultValue: "Users are the accounts that can log in and operate within the tenant."),
        [Content.AdminIdentityNoMatchUsers] = new("Content", Page: "Identity", DefaultValue: "No matching users"),
        [Content.AdminIdentityAssignRole] = new("Content", Page: "Identity", DefaultValue: "Assign Role"),
        [Content.AdminIdentityPanelRoles] = new("Content", Page: "Identity", DefaultValue: "Roles"),
        [Content.AdminIdentityEmptyRoles] = new("Content", Page: "Identity", DefaultValue: "No roles yet"),
        [Content.AdminIdentityEmptyRolesText] = new("Content", Page: "Identity", DefaultValue: "A role aggregates a set of permissions that can be granted to users in bulk."),
        [Content.AdminIdentityNoMatchRoles] = new("Content", Page: "Identity", DefaultValue: "No matching roles"),
        [Content.AdminIdentityEditPerms] = new("Content", Page: "Identity", DefaultValue: "Edit Permissions"),
        [Content.AdminIdentityUsernamePh] = new("Content", Page: "Identity", DefaultValue: "Login account"),
        [Content.AdminIdentityInitialRole] = new("Content", Page: "Identity", DefaultValue: "Initial Role"),
        [Content.AdminIdentityNoRoles] = new("Content", Page: "Identity", DefaultValue: "No roles available"),
        [Content.AdminIdentityRoleCodePh] = new("Content", Page: "Identity", DefaultValue: "e.g. order.manager"),
        [Content.AdminIdentityNoPerms] = new("Content", Page: "Identity", DefaultValue: "No permissions available"),
        [Content.AdminIdentitySelectRole] = new("Content", Page: "Identity", DefaultValue: "— Select role —"),
        [Content.AdminIdentityModalEditPerms] = new("Content", Page: "Identity", DefaultValue: "Edit Role Permissions"),
        [Content.AdminIdentityConfirmSavePermsTitle] = new("Content", Page: "Identity", DefaultValue: "Save Role Permissions"),
        [Content.AdminIdentityConfirmSavePermsMsg] = new("Content", Page: "Identity", DefaultValue: "Overwrite this role's permission set? Changes take effect immediately for users with this role, and require re-login to apply on the frontend."),
        [Content.AdminIdentityConfirmAssignTitle] = new("Content", Page: "Identity", DefaultValue: "Assign Role"),
        [Content.AdminIdentityConfirmAssignMsg] = new("Content", Page: "Identity", DefaultValue: "Confirm assigning role '{1}' to user '{0}'?"),
        [Content.AdminIdentityAssignHint] = new("Content", Page: "Identity", DefaultValue: "Assign roles to user {0} (roles can be global or tenant-scoped)."),
        [Content.AdminIdentityPermHint] = new("Content", Page: "Identity", DefaultValue: "Permission set for role {0} ({1}) (full replacement)."),
        [Content.AdminIdentityUsernameRequired] = new("Content", Page: "Identity", DefaultValue: "Username is required."),
        [Content.AdminIdentityUserCreated] = new("Content", Page: "Identity", DefaultValue: "User {0} created."),
        [Content.AdminIdentityCreateFailed] = new("Content", Page: "Identity", DefaultValue: "Creation failed (HTTP {0})."),
        [Content.AdminIdentityRoleCodeRequired] = new("Content", Page: "Identity", DefaultValue: "Role code is required."),
        [Content.AdminIdentityRoleCreated] = new("Content", Page: "Identity", DefaultValue: "Role {0} created."),
        [Content.AdminIdentitySelectRoleRequired] = new("Content", Page: "Identity", DefaultValue: "Please select a role."),
        [Content.AdminIdentityAssigned] = new("Content", Page: "Identity", DefaultValue: "Assigned role {1} to {0}."),
        [Content.AdminIdentityAssignFailed] = new("Content", Page: "Identity", DefaultValue: "Assignment failed (HTTP {0})."),
        [Content.AdminIdentityPermsUpdated] = new("Content", Page: "Identity", DefaultValue: "Permissions for role {0} updated."),
        [Content.AdminIdentitySaveFailed] = new("Content", Page: "Identity", DefaultValue: "Save failed (HTTP {0})."),
        [Content.AdminPlatformAdminsDesc] = new("Content", Page: "PlatformAdmins", DefaultValue: "Manage platform governance accounts: create, disable / enable, reset passwords, and audit all governance operations. Always keep at least one active administrator."),
        [Content.AdminPlatformAdminsCreate] = new("Content", Page: "PlatformAdmins", DefaultValue: "New Admin"),
        [Content.AdminPlatformAdminsTabAdmins] = new("Content", Page: "PlatformAdmins", DefaultValue: "Admins"),
        [Content.AdminPlatformAdminsPanelAdmins] = new("Content", Page: "PlatformAdmins", DefaultValue: "Platform Admins"),
        [Content.AdminPlatformAdminsEmptyAdminsTitle] = new("Content", Page: "PlatformAdmins", DefaultValue: "No platform admins yet"),
        [Content.AdminPlatformAdminsEmptyAdminsText] = new("Content", Page: "PlatformAdmins", DefaultValue: "Use 'New Admin' to add the first or an additional platform governance account."),
        [Content.AdminPlatformAdminsTabAudit] = new("Content", Page: "PlatformAdmins", DefaultValue: "Audit Log"),
        [Content.AdminPlatformAdminsPanelAudit] = new("Content", Page: "PlatformAdmins", DefaultValue: "Governance Audit"),
        [Content.AdminPlatformAdminsEmptyAuditTitle] = new("Content", Page: "PlatformAdmins", DefaultValue: "No audit records yet"),
        [Content.AdminPlatformAdminsEmptyAuditText] = new("Content", Page: "PlatformAdmins", DefaultValue: "Add, disable, enable, and password-reset operations on platform admins are recorded here."),
        [Content.AdminPlatformAdminsModalCreate] = new("Content", Page: "PlatformAdmins", DefaultValue: "New Platform Admin"),
        [Content.AdminPlatformAdminsUsernamePh] = new("Content", Page: "PlatformAdmins", DefaultValue: "Login account"),
        [Content.AdminPlatformAdminsInitialPassword] = new("Content", Page: "PlatformAdmins", DefaultValue: "Initial password"),
        [Content.AdminPlatformAdminsPasswordPh] = new("Content", Page: "PlatformAdmins", DefaultValue: "At least 8 characters"),
        [Content.AdminPlatformAdminsModalReset] = new("Content", Page: "PlatformAdmins", DefaultValue: "Reset Password"),
        [Content.AdminPlatformAdminsResetHint] = new("Content", Page: "PlatformAdmins", DefaultValue: "Set a new password for {0} (at least 8 characters). The old token becomes invalid immediately."),
        [Content.AdminPlatformAdminsNewPassword] = new("Content", Page: "PlatformAdmins", DefaultValue: "New password"),
        [Content.AdminPlatformAdminsResetButton] = new("Content", Page: "PlatformAdmins", DefaultValue: "Reset"),
        [Content.AdminPlatformAdminsResetPassword] = new("Content", Page: "PlatformAdmins", DefaultValue: "Reset Password"),
        [Content.AdminPlatformAdminsDisableTitle] = new("Content", Page: "PlatformAdmins", DefaultValue: "Disable Platform Admin"),
        [Content.AdminPlatformAdminsDisableMsg] = new("Content", Page: "PlatformAdmins", DefaultValue: "Confirm disabling '{0}'? The account will be unable to sign in immediately."),
        [Content.AdminPlatformAdminsDisableBtn] = new("Content", Page: "PlatformAdmins", DefaultValue: "Disable"),
        [Content.AdminPlatformAdminsEnableTitle] = new("Content", Page: "PlatformAdmins", DefaultValue: "Enable Platform Admin"),
        [Content.AdminPlatformAdminsEnableMsg] = new("Content", Page: "PlatformAdmins", DefaultValue: "Confirm re-enabling '{0}'?"),
        [Content.AdminPlatformAdminsEnableBtn] = new("Content", Page: "PlatformAdmins", DefaultValue: "Enable"),
        [Content.AdminPlatformAdminsColStatus] = new("Content", Page: "PlatformAdmins", DefaultValue: "Status"),
        [Content.AdminPlatformAdminsColCreatedTime] = new("Content", Page: "PlatformAdmins", DefaultValue: "Created"),
        [Content.AdminPlatformAdminsColAction] = new("Content", Page: "PlatformAdmins", DefaultValue: "Action"),
        [Content.AdminPlatformAdminsColActor] = new("Content", Page: "PlatformAdmins", DefaultValue: "Actor"),
        [Content.AdminPlatformAdminsColResult] = new("Content", Page: "PlatformAdmins", DefaultValue: "Result"),
        [Content.AdminPlatformAdminsColTime] = new("Content", Page: "PlatformAdmins", DefaultValue: "Time"),
        [Content.AdminPlatformAdminsCreateRequired] = new("Content", Page: "PlatformAdmins", DefaultValue: "Username and initial password are both required."),
        [Content.AdminPlatformAdminsInitPasswordTooShort] = new("Content", Page: "PlatformAdmins", DefaultValue: "Initial password must be at least 8 characters."),
        [Content.AdminPlatformAdminsCreated] = new("Content", Page: "PlatformAdmins", DefaultValue: "Platform admin {0} created."),
        [Content.AdminPlatformAdminsCreateFailed] = new("Content", Page: "PlatformAdmins", DefaultValue: "Creation failed (HTTP {0})."),
        [Content.AdminPlatformAdminsNewPasswordTooShort] = new("Content", Page: "PlatformAdmins", DefaultValue: "New password must be at least 8 characters."),
        [Content.AdminPlatformAdminsResetDone] = new("Content", Page: "PlatformAdmins", DefaultValue: "Password for {0} has been reset; the old token is invalidated."),
        [Content.AdminPlatformAdminsResetFailed] = new("Content", Page: "PlatformAdmins", DefaultValue: "Reset failed (HTTP {0})."),
        [Content.AdminPlatformAdminsDisabled] = new("Content", Page: "PlatformAdmins", DefaultValue: "{0} has been disabled."),
        [Content.AdminPlatformAdminsDisableFailed] = new("Content", Page: "PlatformAdmins", DefaultValue: "Disable failed (HTTP {0})."),
        [Content.AdminPlatformAdminsEnabled] = new("Content", Page: "PlatformAdmins", DefaultValue: "{0} has been enabled."),
        [Content.AdminPlatformAdminsEnableFailed] = new("Content", Page: "PlatformAdmins", DefaultValue: "Enable failed (HTTP {0})."),
        [Content.AdminPlatformAdminsActAdd] = new("Content", Page: "PlatformAdmins", DefaultValue: "Admin added"),
        [Content.AdminPlatformAdminsActDisable] = new("Content", Page: "PlatformAdmins", DefaultValue: "Admin disabled"),
        [Content.AdminPlatformAdminsActEnable] = new("Content", Page: "PlatformAdmins", DefaultValue: "Admin enabled"),
        [Content.AdminPlatformAdminsActReset] = new("Content", Page: "PlatformAdmins", DefaultValue: "Password reset"),
        [Content.AdminTenantsDesc] = new("Content", Page: "Tenants", DefaultValue: "Manage multi-tenancy: create tenants, configure isolation scope and default quotas — the platform operations entry."),
        [Content.AdminTenantsListTitle] = new("Content", Page: "Tenants", DefaultValue: "Tenant list"),
        [Content.AdminTenantsSearchPlaceholder] = new("Content", Page: "Tenants", DefaultValue: "Search tenant code / name…"),
        [Content.AdminTenantsEmptyTitle] = new("Content", Page: "Tenants", DefaultValue: "No tenants yet"),
        [Content.AdminTenantsEmptyText] = new("Content", Page: "Tenants", DefaultValue: "Create a tenant to isolate data and quota scope."),
        [Content.AdminTenantsLoadingText] = new("Content", Page: "Tenants", DefaultValue: "Loading tenants…"),
        [Content.AdminTenantsNew] = new("Content", Page: "Tenants", DefaultValue: "New tenant"),
        [Content.AdminTenantsStatTotal] = new("Content", Page: "Tenants", DefaultValue: "Total tenants"),
        [Content.AdminTenantsStatTotalSub] = new("Content", Page: "Tenants", DefaultValue: "Platform-level"),
        [Content.AdminTenantsStatEnabled] = new("Content", Page: "Tenants", DefaultValue: "Enabled"),
        [Content.AdminTenantsStatEnabledSub] = new("Content", Page: "Tenants", DefaultValue: "In use"),
        [Content.AdminTenantsStatDisabled] = new("Content", Page: "Tenants", DefaultValue: "Disabled"),
        [Content.AdminTenantsStatDisabledSub] = new("Content", Page: "Tenants", DefaultValue: "Not enabled"),
        [Content.AdminTenantsEdit] = new("Content", Page: "Tenants", DefaultValue: "Edit"),
        [Content.AdminTenantsDisable] = new("Content", Page: "Tenants", DefaultValue: "Disable"),
        [Content.AdminTenantsEnable] = new("Content", Page: "Tenants", DefaultValue: "Enable"),
        [Content.AdminTenantsEnableTitle] = new("Content", Page: "Tenants", DefaultValue: "Enable tenant"),
        [Content.AdminTenantsDisableTitle] = new("Content", Page: "Tenants", DefaultValue: "Disable tenant"),
        [Content.AdminTenantsSettings] = new("Content", Page: "Tenants", DefaultValue: "Settings"),
        [Content.AdminTenantsCreateTitle] = new("Content", Page: "Tenants", DefaultValue: "New tenant"),
        [Content.AdminTenantsCodeLabel] = new("Content", Page: "Tenants", DefaultValue: "Tenant code"),
        [Content.AdminTenantsCodePlaceholder] = new("Content", Page: "Tenants", DefaultValue: "e.g. t-acme"),
        [Content.AdminTenantsNameLabel] = new("Content", Page: "Tenants", DefaultValue: "Tenant name"),
        [Content.AdminTenantsNamePlaceholder] = new("Content", Page: "Tenants", DefaultValue: "e.g. Acme Retail"),
        [Content.AdminTenantsAdminUserLabel] = new("Content", Page: "Tenants", DefaultValue: "First tenant admin username"),
        [Content.AdminTenantsAdminUserPlaceholder] = new("Content", Page: "Tenants", DefaultValue: "e.g. acme-admin"),
        [Content.AdminTenantsAdminDisplayNameLabel] = new("Content", Page: "Tenants", DefaultValue: "Admin display name"),
        [Content.AdminTenantsAdminDisplayNamePlaceholder] = new("Content", Page: "Tenants", DefaultValue: "e.g. Acme Admin"),
        [Content.AdminTenantsAdminEmailLabel] = new("Content", Page: "Tenants", DefaultValue: "Admin email"),
        [Content.AdminTenantsAdminPasswordLabel] = new("Content", Page: "Tenants", DefaultValue: "Admin initial password"),
        [Content.AdminTenantsAdminPasswordHint] = new("Content", Page: "Tenants", DefaultValue: "At least 8 characters; recommend changing immediately after first login."),
        [Content.AdminTenantsLanguagesLabel] = new("Content", Page: "Tenants", DefaultValue: "Available tenant languages"),
        [Content.AdminTenantsLanguagesHint] = new("Content", Page: "Tenants", DefaultValue: "Users can only switch among enabled languages."),
        [Content.AdminTenantsDefaultLangLabel] = new("Content", Page: "Tenants", DefaultValue: "Default display language"),
        [Content.AdminTenantsEditTitle] = new("Content", Page: "Tenants", DefaultValue: "Edit tenant · {0}"),
        [Content.AdminTenantsEditLanguagesLabel] = new("Content", Page: "Tenants", DefaultValue: "Supported tenant languages"),
        [Content.AdminTenantsEditLanguagesHint] = new("Content", Page: "Tenants", DefaultValue: "Language scope comes from the platform multilingual center."),
        [Content.AdminTenantsEditDefaultLangLabel] = new("Content", Page: "Tenants", DefaultValue: "Default language"),
        [Content.AdminTenantsSaveEdit] = new("Content", Page: "Tenants", DefaultValue: "Save changes"),
        [Content.AdminTenantsSettingTitle] = new("Content", Page: "Tenants", DefaultValue: "Tenant settings · {0}"),
        [Content.AdminTenantsSettingEmptyTitle] = new("Content", Page: "Tenants", DefaultValue: "No settings"),
        [Content.AdminTenantsSettingEmptyText] = new("Content", Page: "Tenants", DefaultValue: "Add a config item (Key/Value/DataType) for this tenant."),
        [Content.AdminTenantsSettingKeyLabel] = new("Content", Page: "Tenants", DefaultValue: "Config key"),
        [Content.AdminTenantsSettingKeyPlaceholder] = new("Content", Page: "Tenants", DefaultValue: "e.g. DefaultQuota"),
        [Content.AdminTenantsSettingValueLabel] = new("Content", Page: "Tenants", DefaultValue: "Config value"),
        [Content.AdminTenantsSettingValuePlaceholder] = new("Content", Page: "Tenants", DefaultValue: "e.g. 1000"),
        [Content.AdminTenantsSettingTypeLabel] = new("Content", Page: "Tenants", DefaultValue: "Data type"),
        [Content.AdminTenantsClose] = new("Content", Page: "Tenants", DefaultValue: "Close"),
        [Content.AdminTenantsSaveSetting] = new("Content", Page: "Tenants", DefaultValue: "Save config"),
        [Content.AdminTenantsCodeRequired] = new("Content", Page: "Tenants", DefaultValue: "Tenant code is required."),
        [Content.AdminTenantsAdminUserRequired] = new("Content", Page: "Tenants", DefaultValue: "First tenant admin username is required."),
        [Content.AdminTenantsPasswordTooShort] = new("Content", Page: "Tenants", DefaultValue: "Admin initial password must be at least 8 characters."),
        [Content.AdminTenantsLangRequired] = new("Content", Page: "Tenants", DefaultValue: "Select at least one tenant language."),
        [Content.AdminTenantsCodeExists] = new("Content", Page: "Tenants", DefaultValue: "Tenant code already exists."),
        [Content.AdminTenantsSaveFailed] = new("Content", Page: "Tenants", DefaultValue: "Save failed."),
        [Content.AdminTenantsCreated] = new("Content", Page: "Tenants", DefaultValue: "Tenant and first admin created."),
        [Content.AdminTenantsEditLangRequired] = new("Content", Page: "Tenants", DefaultValue: "Select at least one language."),
        [Content.AdminTenantsUpdated] = new("Content", Page: "Tenants", DefaultValue: "Tenant basic info and language config updated."),
        [Content.AdminTenantsMissingId] = new("Content", Page: "Tenants", DefaultValue: "This record is missing an id."),
        [Content.AdminTenantsOpFailed] = new("Content", Page: "Tenants", DefaultValue: "Operation failed."),
        [Content.AdminTenantsEnabled] = new("Content", Page: "Tenants", DefaultValue: "Tenant enabled."),
        [Content.AdminTenantsDisabled] = new("Content", Page: "Tenants", DefaultValue: "Tenant disabled."),
        [Content.AdminTenantsSettingMissingId] = new("Content", Page: "Tenants", DefaultValue: "This record is missing an id; cannot open settings."),
        [Content.AdminTenantsSettingKeyRequired] = new("Content", Page: "Tenants", DefaultValue: "Config key is required."),
        [Content.AdminTenantsSettingKeyExists] = new("Content", Page: "Tenants", DefaultValue: "Config key already exists."),
        [Content.AdminTenantsSettingSaved] = new("Content", Page: "Tenants", DefaultValue: "Tenant config saved."),
        [Content.AdminTenantsConfirmMsg] = new("Content", Page: "Tenants", DefaultValue: "Confirm {0} tenant '{1}'? This affects its data access and isolation scope."),

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
