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

    // M7-11 应用运行时（Ask 结果 → 可运行应用）
    /// <summary>绑定不完整或查询模式不支持（如 JOIN / between / 多字段排序 / Distinct / 实体无法解析），统一 422。</summary>
    public const string AppBindingNotSupported = "SB_APP_002";
    /// <summary>应用运行时查询执行失败（脱敏后返回，不暴露内部异常）。</summary>
    public const string AppQueryFailed = "SB_APP_003";
    /// <summary>查询快照不存在或已过期（turnId 无效）。</summary>
    public const string AppSnapshotNotFound = "SB_APP_004";
    /// <summary>查询快照归属不符（跨用户/跨租户访问他人查询上下文）。</summary>
    public const string AppSnapshotForbidden = "SB_APP_005";

    // M7-11 应用运行时（契约 §3.2 缺口补码；既有 SB_APP_001..005 继续保留，避免前端/测试回归）
    /// <summary>应用不存在（code 无效）。用于 get/update/delete/publish/rollback/versions/copy 的 404。</summary>
    public const string AppNotFound = "SB_APP_NOT_FOUND";
    /// <summary>应用未发布却被运行（render）。</summary>
    public const string AppNotPublished = "SB_APP_NOT_PUBLISHED";
    /// <summary>发布/编辑并发冲突：期望草稿版本与当前不符。</summary>
    public const string AppDraftChanged = "SB_APP_DRAFT_CHANGED";
    /// <summary>幂等键冲突：同键但请求摘要/期望版本不一致（创建或发布）。</summary>
    public const string AppIdempotencyConflict = "SB_APP_IDEMPOTENCY_CONFLICT";
    /// <summary>绑定数据源未授权/已停用/已撤权（运行时）。</summary>
    public const string AppDataSourceUnauthorized = "SB_APP_DATASOURCE_UNAUTHORIZED";
    /// <summary>RLS / Security Gate 策略拒绝（运行时）。</summary>
    public const string AppQueryBlocked = "SB_APP_QUERY_BLOCKED";
    /// <summary>应用运行时查询执行失败（脱敏，不回吐内部异常）。</summary>
    public const string AppQueryError = "SB_APP_QUERY_ERROR";
    /// <summary>应用操作缺 app:* 权限（区别于通用 SB_FORBIDDEN，专用于 AppBuilder 端点）。</summary>
    public const string AppForbidden = "SB_APP_FORBIDDEN";

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
        [Conflict] = "资源已存在或状态冲突，请刷新后重试。",
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
        [AppBindingNotSupported] = "当前查询无法转为可发布的应用绑定（如含多表关联、多字段排序、去重或无法解析的实体），请简化查询后重试。",
        [AppQueryFailed] = "应用取数执行失败，请稍后重试或联系管理员。",
        [AppSnapshotNotFound] = "查询引用已失效或不存在，请重新发起查询后再生成应用。",
        [AppSnapshotForbidden] = "无权访问该查询上下文，仅创建者本人可将其生成为应用。",
        [QuotaExceeded] = "当前租户配额已用尽，请升级套餐或联系管理员。",
        [TenantIsolated] = "操作越过了租户边界，已被安全策略拒绝。",
        [DataSourceForbidden] = "当前账号无权访问所选数据源。",
        [RowPolicyForbidden] = "当前账号没有满足行级数据策略的访问范围。",
        [QueryPlanSecurityRejected] = "查询计划未通过最终安全校验，已在执行前阻断。",
        [QueryPlanCostGoverned] = "查询成本超过治理阈值，已在执行前拒绝或降级执行。",
        [AppNotFound] = "应用不存在或已被删除，请确认应用编码后重试。",
        [AppNotPublished] = "应用尚未发布，无法运行（请先发布）。",
        [AppDraftChanged] = "应用草稿已被他人或并发编辑修改，请刷新后重试。",
        [AppIdempotencyConflict] = "相同请求已处理过，但本次内容与既有记录不一致，请检查后重试。",
        [AppDataSourceUnauthorized] = "当前账号无权访问该应用绑定的数据源，或数据源已停用。",
        [AppQueryBlocked] = "查询被行级安全或安全网关策略拒绝。",
        [AppQueryError] = "应用取数执行失败，请稍后重试或联系管理员。",
        [AppForbidden] = "权限不足，当前账号无权执行该应用操作（需相应 app:* 权限）。",
    };

    /// <summary>取错误码对应的友好中文提示；缺省回退到通用内部错误提示。</summary>
    public static string Message(string code) =>
        Friendly.TryGetValue(code, out var m) ? m : Friendly[Internal];
}

/// <summary>
/// 拒绝原因码（契约 §3.3）：用于 <see cref="ApiError.Decision"/>，使调用方以结构化方式区分拒绝类型，
/// 而<strong>不靠解析中文 <see cref="ErrorCodes.Message"/> 文本</see>。
/// </summary>
public static class ErrorDecisions
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
