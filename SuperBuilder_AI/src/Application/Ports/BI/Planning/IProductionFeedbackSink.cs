using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Interfaces.BI.Planning;

/// <summary>
/// 生产反馈采集入口（M5-10）。默认实现 NoOp（零行为）；
/// 配置 Mode=Store 时可将反馈（含来自 M5-09 审计记录的自动反馈）写入存储。
/// </summary>
public interface IProductionFeedbackSink
{
    /// <summary>记录一条显式反馈。</summary>
    Task RecordAsync(ProductionFeedback feedback, CancellationToken ct = default);

    /// <summary>从 M5-09 审计记录自动派生并记录反馈（是否启用由实现决定）。</summary>
    Task RecordFromAuditAsync(QueryPlanDecisionAuditRecord audit, CancellationToken ct = default);
}
