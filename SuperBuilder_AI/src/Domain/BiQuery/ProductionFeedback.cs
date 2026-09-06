namespace SuperBuilder_AI.Models.BI;

/// <summary>
/// 生产反馈闭环聚合（M5-10 Production Feedback）。
///
/// 闭环：Feedback → Candidate → Review → Baseline → Regression。
///
/// 设计约束：
/// - 纯数据聚合（不持有行为），便于序列化与跨服务传递；
/// - 通过 <see cref="AuditId"/> / <see cref="CorrelationId"/> 关联 M5-09 审计记录；
/// - 闭环状态由 <see cref="ProductionFeedbackStatus"/> 描述，推进由 ProductionFeedbackLoop 编排；
/// - 默认不启用（配置 Mode=Off），对 Golden 契约免疫、零 schema 变更。
/// </summary>
public sealed class ProductionFeedback
{
    /// <summary>反馈唯一标识。</summary>
    public Guid FeedbackId { get; set; } = Guid.NewGuid();

    /// <summary>关联 M5-09 审计记录标识（可选）。</summary>
    public Guid? AuditId { get; set; }

    /// <summary>请求级关联标识（同 M5-09 CorrelationId）。</summary>
    public string? CorrelationId { get; set; }

    /// <summary>触发反馈的问题（来自审计记录的 Question 或用户填写）。</summary>
    public string? Question { get; set; }

    /// <summary>反馈类别。</summary>
    public FeedbackCategory Category { get; set; } = FeedbackCategory.Other;

    /// <summary>反馈描述。</summary>
    public string? Description { get; set; }

    /// <summary>反馈来源：审计自动生成 / 用户手动提交。</summary>
    public FeedbackSource Source { get; set; } = FeedbackSource.UserManual;

    /// <summary>提交时间（UTC）。</summary>
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>当前闭环状态。</summary>
    public ProductionFeedbackStatus Status { get; set; } = ProductionFeedbackStatus.Received;

    // ── Review ──
    public string? Reviewer { get; set; }
    public string? ReviewNote { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }

    // ── Candidate ──
    public Guid? CandidateId { get; set; }

    // ── Baseline ──
    public string? BaselineVersion { get; set; }

    // ── Regression ──
    public string? RegressionRef { get; set; }

    /// <summary>拒绝原因（Status=Rejected 时填充）。</summary>
    public string? RejectReason { get; set; }
}

/// <summary>反馈类别（M5-10）。</summary>
public enum FeedbackCategory
{
    /// <summary>其他/未分类。</summary>
    Other = 0,

    /// <summary>结果正确性（如拒绝了本应执行的查询）。</summary>
    Correctness = 1,

    /// <summary>性能/成本（如大扫描、无界查询）。</summary>
    Performance = 2,

    /// <summary>安全（如越权字段、脱敏遗漏）。</summary>
    Security = 3,

    /// <summary>可用性（如澄清、审批过多、交互割裂）。</summary>
    Usability = 4
}

/// <summary>反馈闭环状态（M5-10）。</summary>
public enum ProductionFeedbackStatus
{
    /// <summary>已接收（闭环起点）。</summary>
    Received = 0,

    /// <summary>评审中。</summary>
    InReview = 1,

    /// <summary>已接受并生成候选改进项。</summary>
    AcceptedAsCandidate = 2,

    /// <summary>候选已纳入基线。</summary>
    Baselined = 3,

    /// <summary>回归校验通过。</summary>
    RegressionVerified = 4,

    /// <summary>已拒绝，闭环终止。</summary>
    Rejected = 5
}

/// <summary>反馈来源（M5-10）。</summary>
public enum FeedbackSource
{
    /// <summary>用户手动提交。</summary>
    UserManual = 0,

    /// <summary>由 M5-09 审计记录自动派生。</summary>
    AuditAuto = 1
}

/// <summary>
/// 反馈驱动候选改进项（M5-10）。由 Accepted 状态的反馈生成，描述一个可纳入基线的改进提案。
/// </summary>
public sealed class FeedbackDrivenCandidate
{
    /// <summary>候选标识。</summary>
    public Guid CandidateId { get; set; } = Guid.NewGuid();

    /// <summary>关联反馈标识。</summary>
    public Guid FeedbackId { get; set; }

    /// <summary>来源审计记录标识（可选）。</summary>
    public Guid? SourceAuditId { get; set; }

    /// <summary>候选标题。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>提议的改动描述。</summary>
    public string ProposedChange { get; set; } = string.Empty;

    /// <summary>创建时间（UTC）。</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
