using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SuperBuilder_AI.Application.Common.Options;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 可配置 Sink（M5-10）。按 ProductionFeedbackOptions.Mode 在 NoOp 与 Store 间切换；
/// 默认 Mode=Off → 与历史逐字节等价。
/// </summary>
public sealed class ConfigurableProductionFeedbackSink : IProductionFeedbackSink
{
    private readonly IProductionFeedbackSink _inner;

    public ConfigurableProductionFeedbackSink(
        IOptions<ProductionFeedbackOptions> options,
        IProductionFeedbackStore store)
    {
        var opts = options?.Value ?? new ProductionFeedbackOptions();
        _inner = opts.Mode == ProductionFeedbackMode.Store
            ? new StoreProductionFeedbackSink(store, opts.EnableAuditDerivedFeedback)
            : new NoOpProductionFeedbackSink();
    }

    public Task RecordAsync(ProductionFeedback feedback, CancellationToken ct = default) => _inner.RecordAsync(feedback, ct);

    public Task RecordFromAuditAsync(QueryPlanDecisionAuditRecord audit, CancellationToken ct = default) => _inner.RecordFromAuditAsync(audit, ct);
}
