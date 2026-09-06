namespace SuperBuilder_AI.Api.Errors;

/// <summary>
/// 统一错误码（按领域分组，便于前端映射友好提示与运维检索）。
/// 约定：<c>SB_&lt;领域&gt;_&lt;序号&gt;</c>；HTTP 状态码由
/// <see cref="UnifiedExceptionMiddleware"/> 按异常类型/消息映射决定。
/// 仅定义常量与友好文案，不改动任何业务抛点，故不影响 Golden 行为契约。
/// </summary>
public static class ErrorCodes
{
    // 通用 / 网关层
    public const string BadRequest = "SB_BAD_REQUEST";
    public const string Unauthorized = "SB_UNAUTHORIZED";
    public const string Forbidden = "SB_FORBIDDEN";
    public const string NotFound = "SB_NOT_FOUND";
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

    // 平台（多租户 / 配额 / 审计）
    public const string QuotaExceeded = "SB_PFM_001";
    public const string TenantIsolated = "SB_PFM_002";
    public const string DataSourceForbidden = "SB_AUTHZ_001";
    public const string RowPolicyForbidden = "SB_AUTHZ_002";
    public const string QueryPlanSecurityRejected = "SB_SECURITY_001";
    public const string QueryPlanCostGoverned = "SB_COST_001";

    /// <summary>友好提示文案（无匹配时回退到通用提示）。</summary>
    private static readonly Dictionary<string, string> Friendly = new()
    {
        [BadRequest] = "请求参数不合法，请检查输入后重试。",
        [Unauthorized] = "鉴权失败，请重新登录后再试。",
        [Forbidden] = "权限不足，当前账号无权执行该操作。",
        [NotFound] = "请求的资源不存在或已被删除。",
        [Unsupported] = "当前操作不被支持。",
        [Internal] = "服务暂时不可用，请稍后重试；如持续出现，可凭错误码联系管理员。",
        [ServiceUnavailable] = "平台尚未就绪（数据库不可达或尚未完成初始化），请稍后重试或联系管理员。",
        [TooManyRequests] = "请求过于频繁，请稍后再试。",
        [AuthInvalidCredential] = "用户名或租户不存在，或账号已被禁用。",
        [BiNoQueryTable] = "未能从问题中识别出可查询的数据表，请换一种表述或指定具体业务对象（如「订单」「库存」）。",
        [BiNoQueryableField] = "未能从问题中识别可分析的字段，请补充指标或维度（如「销售额」「按地区」）。",
        [BiSemanticUnsupported] = "当前业务语义暂不支持该分析（如聚合方式不受支持），请调整问法。",
        [BiMetricAmbiguous] = "问题中存在多个可能匹配的度量字段，请明确指定（如「订单金额」而非「金额」）。",
        [BiConfidenceLow] = "模型对该查询的把握不足，请补充更明确的指标、维度或筛选条件后重试。",
        [BiDataSourceUnreachable] = "数据源暂时不可达，请稍后重试或联系管理员检查连接。",
        [AppDslInvalid] = "应用定义（DSL）不合法，请检查组件配置后重试。",
        [AgentNoContent] = "智能体未返回有效内容，请重新描述任务。",
        [QuotaExceeded] = "当前租户配额已用尽，请升级套餐或联系管理员。",
        [TenantIsolated] = "操作越过了租户边界，已被安全策略拒绝。",
        [DataSourceForbidden] = "当前账号无权访问所选数据源。",
        [RowPolicyForbidden] = "当前账号没有满足行级数据策略的访问范围。",
        [QueryPlanSecurityRejected] = "查询计划未通过最终安全校验，已在执行前阻断。",
        [QueryPlanCostGoverned] = "查询成本超过治理阈值，已在执行前拒绝或降级执行。",
    };

    /// <summary>取错误码对应的友好中文提示；缺省回退到通用内部错误提示。</summary>
    public static string Message(string code) =>
        Friendly.TryGetValue(code, out var m) ? m : Friendly[Internal];
}
