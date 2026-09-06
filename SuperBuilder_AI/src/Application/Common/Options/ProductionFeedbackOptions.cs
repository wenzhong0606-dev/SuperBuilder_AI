namespace SuperBuilder_AI.Application.Common.Options;

/// <summary>
/// 生产反馈闭环配置（M5-10）。配置节 <c>ProductionFeedback</c>。
/// 默认 Mode=Off，与历史行为逐字节等价；对 Golden 免疫、零 schema 变更。
/// </summary>
public sealed class ProductionFeedbackOptions
{
    /// <summary>配置节名称。</summary>
    public const string SectionName = "ProductionFeedback";

    /// <summary>
    /// 反馈闭环启用模式。
    /// Off（默认）：Sink 为 NoOp，不采集、不落盘、不接管线——与历史行为逐字节等价。
    /// Store：将反馈（含审计自动反馈）写入 IProductionFeedbackStore。
    /// </summary>
    public ProductionFeedbackMode Mode { get; set; } = ProductionFeedbackMode.Off;

    /// <summary>
    /// 是否允许从 M5-09 审计记录自动生成反馈（Source=AuditAuto）。
    /// 仅当 Mode=Store 时生效；默认 false。
    /// </summary>
    public bool EnableAuditDerivedFeedback { get; set; } = false;
}

/// <summary>反馈闭环启用模式（M5-10）。</summary>
public enum ProductionFeedbackMode
{
    /// <summary>关闭（默认）：零行为变更。</summary>
    Off = 0,

    /// <summary>采集并写入存储。</summary>
    Store = 1
}
