using System;

namespace SuperBuilder_AI.Models.Audit;

/// <summary>
/// 审计日志实体（P10.3 Enterprise SaaS 治理面）。
/// 记录「谁(UserId/Actor) 在何时(Timestamp) 对什么(EntityType/EntityId) 做了什么(Action) 结果如何(Result)」。
/// 不承载任何业务/BI 语义，独立于查询链路与 Golden 契约，不影响 BI 行为契约。
/// </summary>
public class AuditLog
{
    public long Id { get; set; }

    /// <summary>所属租户（0=平台级/全局审计）。</summary>
    public long TenantId { get; set; }

    /// <summary>操作者用户 Id（系统/调度事件为 null）。</summary>
    public long? UserId { get; set; }

    /// <summary>操作者标识（用户名 / "system" / "scheduler" / "http"）。冗余存储便于查询。</summary>
    public string Actor { get; set; } = "system";

    /// <summary>动作/事件类型，如 "user.create"、"role.assign"、"http.request"。</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>受影响的实体类型，如 "User"、"Role"、"Dashboard"。</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>受影响的实体 Id（字符串以兼容 long/code/guid 等多种 Id 形式）。</summary>
    public string? EntityId { get; set; }

    /// <summary>变更前快照（JSON 结构化，可选）。</summary>
    public string? BeforeJson { get; set; }

    /// <summary>变更后快照（JSON 结构化，可选）。</summary>
    public string? AfterJson { get; set; }

    /// <summary>结果：success / failure。</summary>
    public string Result { get; set; } = "success";

    /// <summary>结果说明/错误信息（可选）。</summary>
    public string? Message { get; set; }

    /// <summary>
    /// 治理操作目标租户 Id（管理面，M2-02）。
    /// 当平台治理主体操作具体租户 B（启用/停用/设置等）时记录该租户 Id；
    /// 普通租户业务操作或平台级操作记为 null。独立于 EffectiveTenant，杜绝「管理目标」伪装成「生效租户」。
    /// </summary>
    public long? ManagementTargetTenantId { get; set; }

    /// <summary>
    /// 请求关联 Id（M2-02）：取自 <c>X-Correlation-Id</c> 或 <c>HttpContext.TraceIdentifier</c>，
    /// 用于把一次请求内多条审计（访问日志 + 业务审计）串联检索。
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>事件发生时间（UTC）。</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>行写入时间（UTC）。</summary>
    public DateTime CreatedTime { get; set; }
}
