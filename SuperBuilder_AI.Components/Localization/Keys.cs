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

        // M3-05 递延项：后端统一错误码镜像（与后端 ResourceKeys.Error.SB_* 一一对应，键集合须由 ResourceKeyRegistryTests 校验一致）。
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

        // ComponentGallery（组件库巡展）
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

        // M3-05 递延项：后端统一错误码离线回退默认值（zh-CN 须等于 LocalizationSeedService.ZhCnDefaults，en-US 须等于 ResourceKeys.Catalog.DefaultValue）。
        [Error.SB_BAD_REQUEST] = new("请求参数不合法，请检查输入后重试。", "The request parameters are invalid. Please check your input and try again."),
        [Error.SB_UNAUTHORIZED] = new("鉴权失败，请重新登录后再试。", "Authentication failed. Please sign in again."),
        [Error.SB_FORBIDDEN] = new("权限不足，当前账号无权执行该操作。", "Insufficient permissions. Your account is not authorized for this operation."),
        [Error.SB_NOT_FOUND] = new("请求的资源不存在或已被删除。", "The requested resource does not exist or has been deleted."),
        [Error.SB_UNSUPPORTED] = new("当前操作不被支持。", "This operation is not supported."),
        [Error.SB_INTERNAL] = new("服务暂时不可用，请稍后重试；如持续出现，可凭错误码联系管理员。", "The service is temporarily unavailable. Please retry later; if it persists, contact the administrator with the error code."),
        [Error.SB_SERVICE_UNAVAILABLE] = new("平台尚未就绪（数据库不可达或尚未完成初始化），请稍后重试或联系管理员。", "The platform is not ready (database unreachable or initialization incomplete). Please retry later or contact the administrator."),
        [Error.SB_TOO_MANY_REQUESTS] = new("请求过于频繁，请稍后再试。", "Too many requests. Please try again later."),
        [Error.SB_AUTH_001] = new("用户名或租户不存在，或账号已被禁用。", "The username or tenant does not exist, or the account is disabled."),
        [Error.SB_BI_001] = new("未能从问题中识别出可查询的数据表，请换一种表述或指定具体业务对象（如「订单」「库存」）。", "Could not identify a queryable table from your question. Try rephrasing or naming a specific business object (e.g. 'orders', 'inventory')."),
        [Error.SB_BI_002] = new("未能从问题中识别可分析的字段，请补充指标或维度（如「销售额」「按地区」）。", "Could not identify an analyzable field. Please add a metric or dimension (e.g. 'sales amount', 'by region')."),
        [Error.SB_BI_003] = new("当前业务语义暂不支持该分析（如聚合方式不受支持），请调整问法。", "This analysis is not supported by the current business semantics (e.g. the aggregation is unsupported). Please rephrase."),
        [Error.SB_BI_004] = new("问题中存在多个可能匹配的度量字段，请明确指定（如「订单金额」而非「金额」）。", "Multiple metric fields may match. Please specify clearly (e.g. 'order amount' instead of 'amount')."),
        [Error.SB_BI_005] = new("模型对该查询的把握不足，请补充更明确的指标、维度或筛选条件后重试。", "The model's confidence in this query is low. Please add clearer metrics, dimensions, or filters and retry."),
        [Error.SB_BI_006] = new("数据源暂时不可达，请稍后重试或联系管理员检查连接。", "The data source is temporarily unreachable. Please retry later or contact the administrator to check the connection."),
        [Error.SB_APP_001] = new("应用定义（DSL）不合法，请检查组件配置后重试。", "The application definition (DSL) is invalid. Please check the component configuration and retry."),
        [Error.SB_AGENT_001] = new("智能体未返回有效内容，请重新描述任务。", "The agent returned no valid content. Please re-describe the task."),
        [Error.SB_PFM_001] = new("当前租户配额已用尽，请升级套餐或联系管理员。", "The current tenant quota is exhausted. Please upgrade the plan or contact the administrator."),
        [Error.SB_PFM_002] = new("操作越过了租户边界，已被安全策略拒绝。", "The operation crossed the tenant boundary and was rejected by the security policy."),
        [Error.SB_AUTHZ_001] = new("当前账号无权访问所选数据源。", "Your account is not authorized to access the selected data source."),
        [Error.SB_AUTHZ_002] = new("当前账号没有满足行级数据策略的访问范围。", "Your account has no access scope satisfying the row-level data policy."),
        [Error.SB_SECURITY_001] = new("查询计划未通过最终安全校验，已在执行前阻断。", "The query plan failed the final security validation and was blocked before execution."),

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

        // M3-05 重内容页正文批次（续）：ModelAccounts / SemanticLabel / ComponentGallery / ThemeEditor 补充 / MetadataEntity
        [Content.ModelAccountsBound] = new("已绑定", "Bound"),
        [Content.ModelAccountsUnbound] = new("未绑定", "Not bound"),
        [Content.ModelAccountsDefault] = new("默认", "Default"),
        [Content.ModelAccountsSetDefault] = new("设为默认", "Set as default"),
        [Content.ModelAccountsBindPanel] = new("绑定自有 API Key", "Bind your own API Key"),
        [Content.ModelAccountsFieldModel] = new("模型", "Model"),
        [Content.ModelAccountsFieldApiKey] = new("API Key", "API Key"),
        [Content.ModelAccountsApiKeyPlaceholder] = new("sk-••••••", "sk-••••••"),
        [Content.ModelAccountsFieldNote] = new("备注", "Note"),
        [Content.ModelAccountsNotePlaceholder] = new("如：生产环境专用", "e.g. Production only"),
        [Content.ModelAccountsSaveBind] = new("保存绑定", "Save binding"),
        [Content.ModelAccountsSetDefaultToast] = new("已将 {0} 设为默认模型", "Set {0} as default model"),
        [Content.ModelAccountsBindSubmitted] = new("绑定已提交（ILLMProvider + UserModelBinding 计划于 P13 实现，密钥将以掩码存储）。", "Binding submitted (ILLMProvider + UserModelBinding planned for P13; key will be stored masked)."),

        [Content.SemanticLabelLoading] = new("正在加载标签详情…", "Loading label details…"),
        [Content.SemanticLabelPanelDetail] = new("详情", "Details"),
        [Content.SemanticLabelNotFoundTitle] = new("未找到该标签", "Label not found"),
        [Content.SemanticLabelNotFoundText] = new("标签可能已被删除，或后端详情端点尚未接入。", "The label may have been deleted, or the backend detail endpoint is not yet connected."),
        [Content.SemanticLabelInfoPanel] = new("标签信息", "Label information"),
        [Content.SemanticLabelBack] = new("返回列表", "Back to list"),

        [Content.ComponentGallerySecButtons] = new("按钮", "Buttons"),
        [Content.ComponentGallerySecBadges] = new("徽标", "Badges"),
        [Content.ComponentGallerySecForms] = new("表单控件", "Form controls"),
        [Content.ComponentGallerySecProgress] = new("进度条", "Progress bar"),
        [Content.ComponentGallerySecSegmented] = new("分段控件", "Segmented control"),
        [Content.ComponentGallerySecAlerts] = new("告警", "Alerts"),
        [Content.ComponentGallerySecTabs] = new("标签页 SbTabs", "Tabs SbTabs"),
        [Content.ComponentGallerySecDataTable] = new("数据表格 SbDataTable", "Data table SbDataTable"),
        [Content.ComponentGallerySecModal] = new("弹窗 SbModal / SbConfirm", "Modal SbModal / SbConfirm"),
        [Content.ComponentGallerySecToast] = new("轻提示 Toast", "Toast"),
        [Content.ComponentGallerySecGuard] = new("守卫 Guard", "Guard"),
        [Content.ComponentGalleryBtnPrimary] = new("主要", "Primary"),
        [Content.ComponentGalleryBtnSecondary] = new("次要", "Secondary"),
        [Content.ComponentGalleryBtnGhost] = new("幽灵", "Ghost"),
        [Content.ComponentGalleryBtnDanger] = new("危险", "Danger"),
        [Content.ComponentGalleryBtnDisabled] = new("禁用", "Disabled"),
        [Content.ComponentGalleryBadgePrimary] = new("主要", "Primary"),
        [Content.ComponentGalleryBadgePublished] = new("已发布", "Published"),
        [Content.ComponentGalleryBadgeDraft] = new("草稿", "Draft"),
        [Content.ComponentGalleryBadgeFailed] = new("失败", "Failed"),
        [Content.ComponentGalleryBadgeGlobal] = new("全局", "Global"),
        [Content.ComponentGalleryBadgeDefault] = new("默认", "Default"),
        [Content.ComponentGalleryFormTextbox] = new("文本框", "Text box"),
        [Content.ComponentGalleryFormDropdown] = new("下拉", "Dropdown"),
        [Content.ComponentGalleryOptA] = new("选项 A", "Option A"),
        [Content.ComponentGalleryOptB] = new("选项 B", "Option B"),
        [Content.ComponentGalleryProgressUsed] = new("已用 {0}", "Used {0}"),
        [Content.ComponentGallerySegDay] = new("日", "Day"),
        [Content.ComponentGallerySegWeek] = new("周", "Week"),
        [Content.ComponentGallerySegMonth] = new("月", "Month"),
        [Content.ComponentGalleryAlertInfo] = new("信息提示", "Information"),
        [Content.ComponentGalleryAlertWarning] = new("注意提示", "Warning"),
        [Content.ComponentGalleryStatQuestions] = new("今日问数", "Questions today"),
        [Content.ComponentGalleryStatHitRate] = new("命中率", "Hit rate"),
        [Content.ComponentGalleryStatPending] = new("待复核", "Pending review"),
        [Content.ComponentGalleryStatActiveTenants] = new("活跃租户", "Active tenants"),
        [Content.ComponentGallerySampleTable] = new("示例表格", "Example table"),
        [Content.ComponentGalleryColCode] = new("编码", "Code"),
        [Content.ComponentGalleryColName] = new("名称", "Name"),
        [Content.ComponentGalleryColStatus] = new("状态", "Status"),
        [Content.ComponentGalleryColTenant] = new("租户", "Tenant"),
        [Content.ComponentGalleryCardAskTitle] = new("Ask BI", "Ask BI"),
        [Content.ComponentGalleryCardAskDesc] = new("自然语言问数的旗舰入口。", "The flagship entry for natural-language analytics."),
        [Content.ComponentGalleryCardDashTitle] = new("仪表盘", "Dashboards"),
        [Content.ComponentGalleryCardDashDesc] = new("固化可复用的可视化看板。", "Reusable visualization dashboards."),
        [Content.ComponentGalleryCardAppTitle] = new("应用工厂", "App Factory"),
        [Content.ComponentGalleryCardAppDesc] = new("把会话沉淀为 BI 应用。", "Turn conversations into BI apps."),
        [Content.ComponentGalleryTabOverview] = new("概览", "Overview"),
        [Content.ComponentGalleryTabDetail] = new("明细", "Details"),
        [Content.ComponentGalleryTabSettings] = new("设置", "Settings"),
        [Content.ComponentGalleryDataEmpty] = new("无数据", "No data"),
        [Content.ComponentGalleryModalTitle] = new("示例弹窗", "Example modal"),
        [Content.ComponentGalleryOpenModal] = new("打开弹窗", "Open modal"),
        [Content.ComponentGalleryModalBody] = new("弹窗用于承载表单或详情，支持遮罩点击关闭与自定义底部操作。", "Modals host forms or details, supporting backdrop-click close and custom footer actions."),
        [Content.ComponentGalleryModalOk] = new("知道了", "Got it"),
        [Content.ComponentGalleryConfirmTitle] = new("删除确认", "Delete confirmation"),
        [Content.ComponentGalleryConfirmMsg] = new("此操作不可撤销，确定要删除该记录吗？", "This action cannot be undone. Are you sure you want to delete this record?"),
        [Content.ComponentGalleryConfirmText] = new("删除", "Delete"),
        [Content.ComponentGalleryToastInfo] = new("信息提示", "Information"),
        [Content.ComponentGalleryToastSuccess] = new("操作成功", "Operation succeeded"),
        [Content.ComponentGalleryToastWarning] = new("请注意", "Please note"),
        [Content.ComponentGalleryToastError] = new("出错了", "Something went wrong"),
        [Content.ComponentGalleryToastDeleted] = new("已确认删除（演示）。", "Delete confirmed (demo)."),
        [Content.ComponentGalleryGuardDesc] = new("AuthGuard 等待会话自举完成后渲染子内容；PermissionGuard 按权限码门控（当前未强制，见 NavMenuItems.EnforcePermissions）。", "AuthGuard renders children after session bootstrap; PermissionGuard gates by permission code (not enforced yet; see NavMenuItems.EnforcePermissions)."),
        [Content.ComponentGalleryGuardHasPerm] = new("你拥有 demo:write 权限", "You have demo:write permission"),

        [Content.ThemeEditorSampleMetric] = new("示例指标", "Sample metric"),
        [Content.ThemeEditorBtnPrimary] = new("主要按钮", "Primary button"),
        [Content.ThemeEditorBtnSecondary] = new("次要", "Secondary"),
        [Content.ThemeEditorBadgePublished] = new("已发布", "Published"),
        [Content.ThemeEditorBadgeDraft] = new("草稿", "Draft"),
        [Content.ThemeEditorSavedToast] = new("主题已保存（持久化接入 api/themes，P11.3 收口）。", "Theme saved (persistence to api/themes, closing in P11.3)."),

        [Content.MetadataEntityLoading] = new("正在读取元数据实体…", "Reading metadata entity…"),
        [Content.MetadataEntityIntro] = new("以下内容来自当前租户的实际元数据记录。", "The following content comes from the current tenant's actual metadata records."),
        [Content.MetadataEntityNotFound] = new("未找到对应数据（{0}）。", "No corresponding data found ({0})."),
        // M3-05 递延项（Task #83）：分析页正文键 RCL 离线回退（zh-CN / en-US）
        [Content.AskTitle] = new("Ask BI 智能问数（旗舰）", "Ask BI — Natural-language Analytics (Flagship)"),
        [Content.AskRestoringSession] = new("正在恢复登录会话…", "Restoring sign-in session…"),
        [Content.AskLoginHint] = new("请先", "Please "),
        [Content.AskLoginLink] = new("登录", "sign in"),
        [Content.AskLoginHint2] = new("后再问数。", " to ask a question."),
        [Content.AskDataScope] = new("可访问数据范围", "Accessible data scope"),
        [Content.AskDataLoading] = new("加载中…", "Loading…"),
        [Content.AskQuestionLabel] = new("自然语言问题", "Natural-language question"),
        [Content.AskQuestionPlaceholder] = new("例如：上个月各区域销售额对比", "e.g. Compare regional sales last month"),
        [Content.AskButtonAsk] = new("提问", "Ask"),
        [Content.AskButtonClear] = new("清空对话", "Clear chat"),
        [Content.AskHistoryRestored] = new("已恢复 {0} 轮历史", "Restored {0} prior turn(s)"),
        [Content.AskNoConversation] = new("还没有对话。试着问一个业务问题，例如「本月各品类销售额 Top 10」。", "No conversation yet. Try asking a business question, e.g. \"Top 10 product categories this month\"."),
        [Content.AskDataSourceListEmpty] = new("数据源列表为空", "No data sources available"),
        [Content.AskNoDataSources] = new("当前账号暂无可用数据源", "Your account has no data sources available yet"),
        [Content.AskDataSourceLoadFailed] = new("数据源加载失败：{0}", "Failed to load data sources: {0}"),
        [Content.AskEnterQuestion] = new("请输入问题。", "Please enter a question."),
        [Content.AskNoAuthorizedDataSource] = new("当前账号尚未获授权任何数据源，请联系租户管理员。", "Your account is not authorized for any data source. Please contact your tenant administrator."),
        [Content.AskEnterRefineInstruction] = new("请输入细化指令。", "Please enter a refinement instruction."),
        [Content.AskRefineBasedOnResult] = new("已基于本轮结果重新查询（多轮语义调整）。", "Re-queried based on this turn's result (multi-turn semantic refinement)."),
        [Content.AskRefineException] = new("细化异常：{0}", "Refinement error: {0}"),
        [Content.AskFailed] = new("问数失败。", "Query failed."),
        [Content.AskNoValidResult] = new("问数未返回有效结果。", "The query returned no valid result."),
        [Content.AskPublishedAppNamePrefix] = new("问数应用：", "Ask app: "),
        [Content.AskChartTitleDefault] = new("结果图", "Result chart"),
        [Content.AskTableTitleDefault] = new("明细数据", "Detail data"),
        [Content.AskAiSummaryTitle] = new("AI 解读", "AI insight"),
        [Content.AskPublishSuccess] = new("已发布为应用：{0}", "Published as app: {0}"),
        [Content.AskPublishFailed] = new("发布失败：{0}", "Publish failed: {0}"),
        [Content.AskPublishException] = new("发布异常：{0}", "Publish error: {0}"),
        [Content.DashboardsTitle] = new("仪表盘", "Dashboards"),
        [Content.DashboardsDesc] = new("将 Ask 分析结果固化为可复用的可视化看板，支持团队共享与定时刷新。", "Turn Ask analysis results into reusable visual dashboards, with team sharing and scheduled refresh."),
        [Content.DashboardsSearchPlaceholder] = new("搜索名称 / 编码…", "Search by name / code…"),
        [Content.DashboardsEmptyTitle] = new("暂无仪表盘", "No dashboards yet"),
        [Content.DashboardsEmptyText] = new("将一次问数结果保存为仪表盘，即可在团队内复用这套分析视角与图表。", "Save an Ask result as a dashboard to reuse this analysis perspective and charts across your team."),
        [Content.DashboardsLoading] = new("正在加载仪表盘…", "Loading dashboards…"),
        [Content.DashboardsNew] = new("新建仪表盘", "New dashboard"),
        [Content.DashboardsStatTotal] = new("仪表盘总数", "Total dashboards"),
        [Content.DashboardsStatTotalSub] = new("含全局模板", "Includes global templates"),
        [Content.DashboardsStatPublished] = new("已发布", "Published"),
        [Content.DashboardsStatPublishedSub] = new("可对外共享", "Shareable externally"),
        [Content.DashboardsStatDraft] = new("草稿", "Draft"),
        [Content.DashboardsStatDraftSub] = new("编辑中", "In editing"),
        [Content.DashboardsPublishFromAsk] = new("从 Ask 发布", "Publish from Ask"),
        [Content.DashboardsRowDetail] = new("详情", "Details"),
        [Content.DashboardsRowDelete] = new("删除", "Delete"),
        [Content.DashboardsDeleteConfirmMsg] = new("删除后不可恢复，确认要删除该仪表盘吗？", "This cannot be undone. Are you sure you want to delete this dashboard?"),
        [Content.DashboardsDeleteConfirmText] = new("删除", "Delete"),
        [Content.DashboardsFieldStatus] = new("状态", "Status"),
        [Content.DashboardsStatusDraft] = new("草稿", "Draft"),
        [Content.DashboardsStatusPublished] = new("已发布", "Published"),
        [Content.DashboardsFieldDslJson] = new("DslJson", "DslJson"),
        [Content.DashboardsFieldDslJsonHint] = new("声明式仪表盘定义（DashboardDsl JSON）。保存将校验后创建。", "Declarative dashboard definition (DashboardDsl JSON). It will be validated before creation."),
        [Content.DashboardsModalCancel] = new("取消", "Cancel"),
        [Content.DashboardsModalCreate] = new("创建", "Create"),
        [Content.DashboardsMissingIdOpen] = new("该记录缺少 id，无法打开详情。", "This record is missing an id and cannot be opened."),
        [Content.DashboardsMissingIdDelete] = new("该记录缺少 id，无法删除。", "This record is missing an id and cannot be deleted."),
        [Content.DashboardsDeleteSuccess] = new("仪表盘已删除。", "Dashboard deleted."),
        [Content.DashboardsDeleteFailed] = new("删除失败。", "Deletion failed."),
        [Content.DashboardsBlueprintUnavailable] = new("未能获取蓝图骨架，请手动填写 DslJson。", "Could not fetch the blueprint skeleton. Please fill in the DslJson manually."),
        [Content.DashboardsDslJsonEmpty] = new("DslJson 不能为空。", "DslJson cannot be empty."),
        [Content.DashboardsCreateSuccess] = new("仪表盘已创建。", "Dashboard created."),
        [Content.DashboardsCreateFailed] = new("创建失败（HTTP {0}）。", "Creation failed (HTTP {0})."),
        [Content.DashboardsLoadFailed] = new("加载失败。", "Failed to load."),
        [Content.AppsTitle] = new("应用工厂", "App Factory"),
        [Content.AppsDesc] = new("将问数会话沉淀为可复用的 BI 应用：可视化编辑器、DSL 蓝图与一键发布。", "Distill ask sessions into reusable BI apps: visual editor, DSL blueprint, and one-click publishing."),
        [Content.AppsSearchPlaceholder] = new("搜索应用编码 / 名称…", "Search by app code / name…"),
        [Content.AppsEmptyTitle] = new("还没有应用", "No apps yet"),
        [Content.AppsEmptyText] = new("应用是问数能力的封装：把一次典型分析固化为卡片、图表与筛选器，分享给团队即可复用。", "An app wraps ask capability: freeze a typical analysis into cards, charts, and filters to share and reuse with your team."),
        [Content.AppsLoading] = new("正在加载应用…", "Loading apps…"),
        [Content.AppsPanelTitle] = new("应用清单", "App list"),
        [Content.AppsNew] = new("新建应用", "New app"),
        [Content.AppsFromAsk] = new("从 Ask 发布", "Publish from Ask"),
        [Content.AppsStatTotal] = new("应用总数", "Total apps"),
        [Content.AppsStatTotalSub] = new("含全局模板", "Includes global templates"),
        [Content.AppsStatGlobal] = new("全局模板", "Global template"),
        [Content.AppsStatGlobalSub] = new("平台内置", "Built-in platform"),
        [Content.AppsStatTenant] = new("租户应用", "Tenant apps"),
        [Content.AppsStatTenantSub] = new("本租户自建", "Built by this tenant"),
        [Content.AppsNoDescription] = new("（暂无描述）", "(No description)"),
        [Content.AppsBadgeGlobal] = new("全局模板", "Global template"),
        [Content.AppsBadgeTenant] = new("租户", "Tenant"),
        [Content.AppsEdit] = new("编辑", "Edit"),
        [Content.AppsDelete] = new("删除", "Delete"),
        [Content.AppsDeleteConfirmTitle] = new("删除应用", "Delete app"),
        [Content.AppsDeleteConfirmMsg] = new("删除后不可恢复，确认要删除该应用吗？", "This cannot be undone. Are you sure you want to delete this app?"),
        [Content.AppsDeleteConfirmText] = new("删除", "Delete"),
        [Content.AppsGenDescLabel] = new("应用描述", "App description"),
        [Content.AppsGenDescHint] = new("用一句话描述你想要的 BI 应用，AI 将生成 DSL。", "Describe the BI app you want in one sentence; the AI will generate the DSL."),
        [Content.AppsGenDescPlaceholder] = new("例如：销售看板，按地区展示销售额与订单量", "e.g. Sales dashboard showing sales and order volume by region"),
        [Content.AppsGenCodeLabel] = new("应用编码", "App code"),
        [Content.AppsGenCodeHint] = new("留空由系统生成（如 my-app-1733）。", "Leave blank to let the system generate one (e.g. my-app-1733)."),
        [Content.AppsGenCodePlaceholder] = new("可选", "Optional"),
        [Content.AppsGenCancel] = new("取消", "Cancel"),
        [Content.AppsGenSubmit] = new("生成并创建", "Generate & create"),
        [Content.AppsDslIntro] = new("应用 {0} 的声明式定义（AppDsl JSON）。保存将重新校验并覆盖。", "Declarative definition of app {0} (AppDsl JSON). Saving re-validates and overwrites."),
        [Content.AppsDslGlobalWarning] = new("该应用为全局模板，不可修改。", "This app is a global template and cannot be modified."),
        [Content.AppsDslFieldLabel] = new("DslJson", "DslJson"),
        [Content.AppsDslSave] = new("保存", "Save"),
        [Content.AppsMissingCodeOpen] = new("该记录缺少 code，无法打开详情。", "This record is missing a code and cannot be opened."),
        [Content.AppsMissingCodeEdit] = new("该记录缺少 code，无法编辑。", "This record is missing a code and cannot be edited."),
        [Content.AppsMissingCodeDelete] = new("该记录缺少 code，无法删除。", "This record is missing a code and cannot be deleted."),
        [Content.AppsGenDescRequired] = new("应用描述必填。", "App description is required."),
        [Content.AppsGenSuccess] = new("应用已通过 AI 生成并创建。", "App generated by AI and created."),
        [Content.AppsGenFailed] = new("生成失败（HTTP {0}）。", "Generation failed (HTTP {0})."),
        [Content.AppsReadFailed] = new("读取应用失败。", "Failed to read the app."),
        [Content.AppsDslJsonEmpty] = new("DslJson 不能为空。", "DslJson cannot be empty."),
        [Content.AppsDslSaved] = new("应用 DSL 已保存。", "App DSL saved."),
        [Content.AppsDslSaveFailed] = new("保存失败（HTTP {0}）。", "Save failed (HTTP {0})."),
        [Content.AppsDeleteSuccess] = new("应用已删除。", "App deleted."),
        [Content.AppsDeleteFailed] = new("删除失败。", "Delete failed."),
        [Content.AppsLoadFailed] = new("加载失败。", "Failed to load."),
        [Content.AppDetailDesc] = new("应用详情：DSL 结构、页面与组件清单。", "App details: DSL structure, pages, and component manifest."),
        [Content.AppDetailBack] = new("返回列表", "Back to list"),
        [Content.AppDetailRefresh] = new("刷新", "Refresh"),
        [Content.AppDetailDelete] = new("删除", "Delete"),
        [Content.AppDetailLoading] = new("正在加载应用…", "Loading app…"),
        [Content.AppDetailTabOverview] = new("概览", "Overview"),
        [Content.AppDetailPanelBasic] = new("基本信息", "Basic info"),
        [Content.AppDetailTabComponents] = new("页面与组件", "Pages & components"),
        [Content.AppDetailPanelComponents] = new("组件清单", "Component manifest"),
        [Content.AppDetailEmptyComponentsTitle] = new("未解析到组件", "No components resolved"),
        [Content.AppDetailEmptyComponentsText] = new("该应用的 DSL 中暂无可解析的组件定义。", "The app's DSL contains no resolvable component definitions."),
        [Content.AppDetailTabDsl] = new("DSL", "DSL"),
        [Content.AppDetailPanelDsl] = new("App DSL（只读）", "App DSL (read-only)"),
        [Content.AppDetailDeleteConfirmTitle] = new("删除应用", "Delete app"),
        [Content.AppDetailDeleteConfirmMsg] = new("确认删除应用「{0}」？该操作不可撤销。", "Delete app \"{0}\"? This action cannot be undone."),
        [Content.AppDetailDeleteConfirmText] = new("删除", "Delete"),
        [Content.AppDetailTitleDefault] = new("应用详情", "App details"),
        [Content.AppDetailDeleteSuccess] = new("应用已删除。", "App deleted."),
        [Content.AppDetailDeleteFailed] = new("删除失败（HTTP {0}）。", "Delete failed (HTTP {0})."),
        [Content.AppDetailLoadFailed] = new("加载失败（{0}）。", "Failed to load (HTTP {0})."),
        [Content.AskTurnMe] = new("我", "Me"),
        [Content.AskTurnAiSummary] = new("AI 解读", "AI insight"),
        [Content.AskTurnMeta] = new("耗时 {0} ms · 建议图表 {1} 个", "Elapsed {0} ms · {1} suggested chart(s)"),
        [Content.AskTurnChartDefault] = new("图表 {0}", "Chart {0}"),
        [Content.AskTurnViewLabel] = new("视图：", "View: "),
        [Content.AskTurnBar] = new("柱状", "Bar"),
        [Content.AskTurnLine] = new("折线", "Line"),
        [Content.AskTurnPie] = new("饼图", "Pie"),
        [Content.AskTurnHideLegend] = new("隐藏图例", "Hide legend"),
        [Content.AskTurnShowLegend] = new("显示图例", "Show legend"),
        [Content.AskTurnCyclePalette] = new("换配色", "Cycle palette"),
        [Content.AskTurnStacked] = new("堆叠", "Stacked"),
        [Content.AskTurnUnstacked] = new("取消堆叠", "Unstacked"),
        [Content.AskTurnArea] = new("面积", "Area"),
        [Content.AskTurnUnarea] = new("取消面积", "Unarea"),
        [Content.AskTurnMultiAxis] = new("多轴", "Multi-axis"),
        [Content.AskTurnUnmulti] = new("取消多轴", "Unmulti"),
        [Content.AskTurnRowsTotal] = new("共 {0} 行", "Total {0} row(s)"),
        [Content.AskTurnDrill] = new("下钻：", "Drill down: "),
        [Content.AskTurnClearDrill] = new("清除下钻", "Clear drill"),
        [Content.AskTurnExportCsv] = new("导出 CSV", "Export CSV"),
        [Content.AskTurnExportExcel] = new("导出 Excel", "Export Excel"),
        [Content.AskTurnQueryFailed] = new("查询失败：{0}", "Query failed: {0}"),
        [Content.AskTurnViewSql] = new("查看生成的 SQL", "View generated SQL"),
        [Content.AskTurnHideCompare] = new("隐藏对比", "Hide compare"),
        [Content.AskTurnShowCompare] = new("对比原结果", "Compare with original"),
        [Content.AskTurnOriginalResult] = new("原结果 · {0}", "Original · {0}"),
        [Content.AskTurnRefinedResult] = new("细化结果 · {0}", "Refined · {0}"),
        [Content.AskTurnRefinePlaceholder] = new("例如：改成柱状图 / 隐藏图例 / 配色换成绿色", "e.g. change to bar / hide legend / switch palette to green"),
        [Content.AskTurnApplyAdjust] = new("应用调整", "Apply adjustment"),
        [Content.AskTurnSemanticPlaceholder] = new("基于本轮结果继续追问，如：只看华东地区 / 改成按月统计", "Follow up on this result, e.g. only East China / group by month"),
        [Content.AskTurnSemanticRefine] = new("语义细化", "Semantic refine"),
        [Content.AskTurnRefining] = new("查询中…", "Querying…"),
        [Content.AskTurnPublish] = new("发布为应用", "Publish as app"),
        [Content.AskTurnPublishing] = new("发布中…", "Publishing…"),
        [Content.AskTurnRefineNoCmd] = new("请输入调整指令。", "Please enter an adjustment instruction."),
        [Content.AskTurnRefineNoViz] = new("当前轮无可视化结果。", "This turn has no visualization result."),
        [Content.AskTurnRefineUnrecognized] = new("未能识别指令（支持：改成柱状/折线/饼图、显示/隐藏图例、配色换绿/橙/红/灰/蓝）。", "Unrecognized instruction (supported: bar/line/pie, show/hide legend, palette green/orange/red/gray/blue)."),
        [Content.AskTurnRefineApplied] = new("已应用视图调整（纯前端，未发起后端请求）。", "View adjustment applied (front-end only, no backend request)."),
        [Content.CommonRefresh] = new("刷新", "Refresh"),
        [Content.CommonCancel] = new("取消", "Cancel"),
        [Content.CommonCreate] = new("创建", "Create"),
        [Content.CommonSave] = new("保存", "Save"),
        [Content.CommonAdd] = new("添加", "Add"),
        [Content.CommonUsername] = new("用户名", "Username"),
        [Content.CommonDisplayName] = new("显示名", "Display Name"),
        [Content.CommonEmail] = new("邮箱", "Email"),
        [Content.CommonName] = new("名称", "Name"),
        [Content.CommonDescription] = new("描述", "Description"),
        [Content.CommonRole] = new("角色", "Role"),
        [Content.CommonUser] = new("用户", "User"),
        [Content.CommonPermission] = new("权限", "Permission"),
        [Content.CommonPermissions] = new("权限点", "Permissions"),
        [Content.CommonRoleCode] = new("角色编码", "Role Code"),
        [Content.CommonSelectPlaceholder] = new("— 请选择 —", "— Select —"),
        [Content.CommonOptional] = new("可选", "Optional"),
        [Content.AdminIdentityTitle] = new("身份与权限", "Identity & Permissions"),
        [Content.AdminIdentityDesc] = new("管理用户、角色与权限分配，基于租户作用域的 RBAC。", "Manage users, roles and permission assignment with tenant-scoped RBAC."),
        [Content.AdminIdentityCreateUser] = new("新建用户", "New User"),
        [Content.AdminIdentityCreateRole] = new("新建角色", "New Role"),
        [Content.AdminIdentityLoading] = new("正在加载身份与权限…", "Loading identity & permissions…"),
        [Content.AdminIdentityStatUsers] = new("用户", "Users"),
        [Content.AdminIdentityStatUsersSub] = new("当前租户", "Current tenant"),
        [Content.AdminIdentityStatRoles] = new("角色", "Roles"),
        [Content.AdminIdentityStatRolesSub] = new("RBAC", "RBAC"),
        [Content.AdminIdentityStatPerms] = new("权限点", "Permissions"),
        [Content.AdminIdentityStatPermsSub] = new("可授予", "Grantable"),
        [Content.AdminIdentityPanelUsers] = new("用户", "Users"),
        [Content.AdminIdentityEmptyUsers] = new("暂无用户", "No users yet"),
        [Content.AdminIdentityEmptyUsersText] = new("用户是租户内可登录与操作的账号主体。", "Users are the accounts that can log in and operate within the tenant."),
        [Content.AdminIdentityNoMatchUsers] = new("无匹配用户", "No matching users"),
        [Content.AdminIdentityAssignRole] = new("分配角色", "Assign Role"),
        [Content.AdminIdentityPanelRoles] = new("角色", "Roles"),
        [Content.AdminIdentityEmptyRoles] = new("暂无角色", "No roles yet"),
        [Content.AdminIdentityEmptyRolesText] = new("角色聚合一组权限，可批量授予用户。", "A role aggregates a set of permissions that can be granted to users in bulk."),
        [Content.AdminIdentityNoMatchRoles] = new("无匹配角色", "No matching roles"),
        [Content.AdminIdentityEditPerms] = new("编辑权限", "Edit Permissions"),
        [Content.AdminIdentityUsernamePh] = new("登录账号", "Login account"),
        [Content.AdminIdentityInitialRole] = new("初始角色", "Initial Role"),
        [Content.AdminIdentityNoRoles] = new("暂无可选角色", "No roles available"),
        [Content.AdminIdentityRoleCodePh] = new("如 order.manager", "e.g. order.manager"),
        [Content.AdminIdentityNoPerms] = new("暂无可选权限", "No permissions available"),
        [Content.AdminIdentitySelectRole] = new("— 选择角色 —", "— Select role —"),
        [Content.AdminIdentityModalEditPerms] = new("编辑角色权限", "Edit Role Permissions"),
        [Content.AdminIdentityConfirmSavePermsTitle] = new("保存角色权限", "Save Role Permissions"),
        [Content.AdminIdentityConfirmSavePermsMsg] = new("确认覆盖该角色的权限集？变更对拥有此角色的用户立即生效，且需重新登录后在前端生效。", "Overwrite this role's permission set? Changes take effect immediately for users with this role, and require re-login to apply on the frontend."),
        [Content.AdminIdentityConfirmAssignTitle] = new("指派角色", "Assign Role"),
        [Content.AdminIdentityConfirmAssignMsg] = new("确认为用户「{0}」指派角色「{1}」？", "Confirm assigning role '{1}' to user '{0}'?"),
        [Content.AdminIdentityAssignHint] = new("为用户 {0} 指派角色（角色可为全局或本租户）。", "Assign roles to user {0} (roles can be global or tenant-scoped)."),
        [Content.AdminIdentityPermHint] = new("角色 {0}（{1}）的权限集（全量替换）。", "Permission set for role {0} ({1}) (full replacement)."),
        [Content.AdminIdentityUsernameRequired] = new("用户名必填。", "Username is required."),
        [Content.AdminIdentityUserCreated] = new("用户 {0} 已创建。", "User {0} created."),
        [Content.AdminIdentityCreateFailed] = new("创建失败（HTTP {0}）。", "Creation failed (HTTP {0})."),
        [Content.AdminIdentityRoleCodeRequired] = new("角色编码必填。", "Role code is required."),
        [Content.AdminIdentityRoleCreated] = new("角色 {0} 已创建。", "Role {0} created."),
        [Content.AdminIdentitySelectRoleRequired] = new("请选择角色。", "Please select a role."),
        [Content.AdminIdentityAssigned] = new("已为 {0} 指派角色 {1}。", "Assigned role {1} to {0}."),
        [Content.AdminIdentityAssignFailed] = new("指派失败（HTTP {0}）。", "Assignment failed (HTTP {0})."),
        [Content.AdminIdentityPermsUpdated] = new("角色 {0} 权限已更新。", "Permissions for role {0} updated."),
        [Content.AdminIdentitySaveFailed] = new("保存失败（HTTP {0}）。", "Save failed (HTTP {0})."),

    };
}
