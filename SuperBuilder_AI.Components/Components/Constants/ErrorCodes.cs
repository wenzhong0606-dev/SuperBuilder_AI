namespace SuperBuilder_AI.Components.Components.Constants;

/// <summary>
/// 前端错误码单一事实来源：与后端 <c>SuperBuilder_AI.Api.Errors.ErrorCodes</c> 保持一致的字符串常量。
/// UI 与调用方应基于 <c>code</c> 而非 <c>message</c> 文本判断错误类型，避免依赖中文错误串（M9-06 统一前后端错误码）。
/// </summary>
/// <remarks>
/// 后端 <c>ApiClientBase.ParseApiError</c> 已解析 <c>code</c>，<c>SendAsync</c>/<c>GetJsonAsync</c> 已透传 <c>Code</c>；
/// 调用点返回元组追加 <c>Code</c> 元素后即可在 UI 层基于本表做结构化分支（如 <c>ErrorCodes.TenantIsolated</c>）。
/// </remarks>
public static class ErrorCodes
{
    // 通用 / 网关层
    public const string BadRequest = "SB_BAD_REQUEST";
    public const string Unauthorized = "SB_UNAUTHORIZED";
    public const string Forbidden = "SB_FORBIDDEN";
    public const string NotFound = "SB_NOT_FOUND";
    public const string Conflict = "SB_CONFLICT";
    public const string Unsupported = "SB_UNSUPPORTED";
    public const string Internal = "SB_INTERNAL";
    public const string ServiceUnavailable = "SB_SERVICE_UNAVAILABLE";
    public const string TooManyRequests = "SB_TOO_MANY_REQUESTS";

    // 鉴权
    public const string AuthInvalidCredential = "SB_AUTH_001";

    // BI 自然语言问数
    public const string BiNoQueryTable = "SB_BI_001";
    public const string BiNoQueryableField = "SB_BI_002";
    public const string BiSemanticUnsupported = "SB_BI_003";
    public const string BiMetricAmbiguous = "SB_BI_004";
    public const string BiConfidenceLow = "SB_BI_005";
    public const string BiDataSourceUnreachable = "SB_BI_006";

    // 应用工厂 / 智能体
    public const string AppDslInvalid = "SB_APP_001";
    public const string AgentNoContent = "SB_AGENT_001";
    public const string AppBindingNotSupported = "SB_APP_002";
    public const string AppQueryFailed = "SB_APP_003";
    public const string AppSnapshotNotFound = "SB_APP_004";
    public const string AppSnapshotForbidden = "SB_APP_005";
    public const string AppNotFound = "SB_APP_NOT_FOUND";
    public const string AppNotPublished = "SB_APP_NOT_PUBLISHED";
    public const string AppDraftChanged = "SB_APP_DRAFT_CHANGED";
    public const string AppIdempotencyConflict = "SB_APP_IDEMPOTENCY_CONFLICT";
    public const string AppDataSourceUnauthorized = "SB_APP_DATASOURCE_UNAUTHORIZED";
    public const string AppQueryBlocked = "SB_APP_QUERY_BLOCKED";
    public const string AppQueryError = "SB_APP_QUERY_ERROR";
    public const string AppForbidden = "SB_APP_FORBIDDEN";

    // 平台（多租户 / 配额 / 审计）
    public const string QuotaExceeded = "SB_PFM_001";
    public const string TenantIsolated = "SB_PFM_002";
    public const string DataSourceForbidden = "SB_AUTHZ_001";
    public const string RowPolicyForbidden = "SB_AUTHZ_002";
    public const string QueryPlanSecurityRejected = "SB_SECURITY_001";
    public const string QueryPlanCostGoverned = "SB_COST_001";

    /// <summary>
    /// 拒绝原因码（契约 §3.3）：用于结构化区分拒绝类型，而非解析中文 <c>Message</c> 文本。
    /// 对应后端 <c>ErrorDecisions</c>。
    /// </summary>
    public static class Decision
    {
        /// <summary>权限不足（缺 app:* 操作权限 / 跨用户访问）。</summary>
        public const string PermissionDenied = "PermissionDenied";

        /// <summary>绑定数据源未授权/已停用/已撤权。</summary>
        public const string DataSourceUnauthorized = "DataSourceUnauthorized";

        /// <summary>RLS / Security Gate 策略拒绝。</summary>
        public const string PolicyBlocked = "PolicyBlocked";

        /// <summary>绑定不完整 / 实体不确定 / 计划无法解析 / 多字段排序 / between / JOIN 等不支持场景。</summary>
        public const string RequiresClarification = "RequiresClarification";
    }
}
