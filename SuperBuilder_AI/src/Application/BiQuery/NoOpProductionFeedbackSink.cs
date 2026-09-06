using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 默认零行为 Sink（M5-10）。所有采集调用直接忽略，不分配存储、不产生副作用。
/// </summary>
public sealed class NoOpProductionFeedbackSink : IProductionFeedbackSink
{
    public Task RecordAsync(ProductionFeedback feedback, CancellationToken ct = default) => Task.CompletedTask;

    public Task RecordFromAuditAsync(QueryPlanDecisionAuditRecord audit, CancellationToken ct = default) => Task.CompletedTask;
}
