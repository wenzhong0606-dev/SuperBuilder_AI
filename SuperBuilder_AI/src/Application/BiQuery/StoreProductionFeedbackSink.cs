using System;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 写入存储的 Sink（M5-10）。将反馈持久化至 IProductionFeedbackStore；
/// 当允许审计派生反馈时，可将 M5-09 审计记录转化为自动反馈条目。
/// </summary>
public sealed class StoreProductionFeedbackSink : IProductionFeedbackSink
{
    private readonly IProductionFeedbackStore _store;
    private readonly bool _enableAuditDerived;

    public StoreProductionFeedbackSink(IProductionFeedbackStore store, bool enableAuditDerived = false)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _enableAuditDerived = enableAuditDerived;
    }

    public Task RecordAsync(ProductionFeedback feedback, CancellationToken ct = default)
    {
        if (feedback is null) throw new ArgumentNullException(nameof(feedback));
        return _store.SaveAsync(feedback, ct);
    }

    public Task RecordFromAuditAsync(QueryPlanDecisionAuditRecord audit, CancellationToken ct = default)
    {
        if (!_enableAuditDerived) return Task.CompletedTask;
        if (audit is null) throw new ArgumentNullException(nameof(audit));

        var feedback = new ProductionFeedback
        {
            AuditId = audit.AuditId,
            CorrelationId = audit.CorrelationId,
            Question = audit.Question,
            Source = FeedbackSource.AuditAuto,
            Category = DeriveCategory(audit),
            Description = $"Auto feedback from audit {audit.AuditId} (outcome={audit.Outcome})."
        };
        return _store.SaveAsync(feedback, ct);
    }

    private static FeedbackCategory DeriveCategory(QueryPlanDecisionAuditRecord audit) => audit.Outcome switch
    {
        AuditOutcome.Rejected => FeedbackCategory.Correctness,
        AuditOutcome.EarlyResponse => FeedbackCategory.Usability,
        AuditOutcome.RequiresApproval => FeedbackCategory.Usability,
        AuditOutcome.AskClarification => FeedbackCategory.Usability,
        _ => FeedbackCategory.Other
    };
}
