using System;
using System.Threading;
using System.Threading.Tasks;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

/// <summary>
/// 生产反馈闭环编排器（M5-10）：Feedback → Candidate → Review → Baseline → Regression。
///
/// 通过注入的 IProductionFeedbackStore（默认内存桩）与 IFeedbackBaselineGateway（默认 NoOp）
/// 驱动状态推进；默认不接管线、不落盘、对 Golden 免疫。
/// 任一推进步骤要求前置状态正确，否则抛 InvalidOperationException。
/// </summary>
public sealed class ProductionFeedbackLoop
{
    private readonly IProductionFeedbackStore _store;
    private readonly IFeedbackBaselineGateway _gateway;

    public ProductionFeedbackLoop(IProductionFeedbackStore store, IFeedbackBaselineGateway gateway)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    }

    /// <summary>接收一条新反馈（闭环起点）。</summary>
    public async Task<ProductionFeedback> ReceiveAsync(ProductionFeedback feedback, CancellationToken ct = default)
    {
        if (feedback is null) throw new ArgumentNullException(nameof(feedback));
        feedback.Status = ProductionFeedbackStatus.Received;
        return await _store.SaveAsync(feedback, ct);
    }

    /// <summary>进入评审。</summary>
    public async Task<ProductionFeedback> StartReviewAsync(Guid feedbackId, string? reviewer = null, CancellationToken ct = default)
    {
        var f = await RequireAsync(feedbackId, ProductionFeedbackStatus.Received, ct);
        f.Status = ProductionFeedbackStatus.InReview;
        f.Reviewer = reviewer;
        f.ReviewedAt = DateTimeOffset.UtcNow;
        return await _store.SaveAsync(f, ct);
    }

    /// <summary>评审通过并生成候选改进项。</summary>
    public async Task<(ProductionFeedback feedback, FeedbackDrivenCandidate candidate)> AcceptAsync(
        Guid feedbackId, string title, string proposedChange, CancellationToken ct = default)
    {
        var f = await RequireAsync(feedbackId, ProductionFeedbackStatus.InReview, ct);
        var candidate = new FeedbackDrivenCandidate
        {
            FeedbackId = f.FeedbackId,
            SourceAuditId = f.AuditId,
            Title = title,
            ProposedChange = proposedChange
        };
        f.Status = ProductionFeedbackStatus.AcceptedAsCandidate;
        f.CandidateId = candidate.CandidateId;
        f.ReviewNote = title;
        var saved = await _store.SaveAsync(f, ct);
        return (saved, candidate);
    }

    /// <summary>将候选纳入基线（通过网关，默认 NoOp）。</summary>
    public async Task<ProductionFeedback> BaselineAsync(Guid feedbackId, CancellationToken ct = default)
    {
        var f = await RequireAsync(feedbackId, ProductionFeedbackStatus.AcceptedAsCandidate, ct);
        var version = await _gateway.RegisterBaselineAsync(
            new FeedbackDrivenCandidate { CandidateId = f.CandidateId ?? Guid.NewGuid(), FeedbackId = f.FeedbackId },
            ct);
        f.Status = ProductionFeedbackStatus.Baselined;
        f.BaselineVersion = version;
        return await _store.SaveAsync(f, ct);
    }

    /// <summary>触发并校验回归（通过网关，默认 NoOp）。</summary>
    public async Task<ProductionFeedback> VerifyRegressionAsync(Guid feedbackId, CancellationToken ct = default)
    {
        var f = await RequireAsync(feedbackId, ProductionFeedbackStatus.Baselined, ct);
        var regressionRef = await _gateway.VerifyRegressionAsync(f.BaselineVersion ?? string.Empty, ct);
        f.Status = ProductionFeedbackStatus.RegressionVerified;
        f.RegressionRef = regressionRef;
        return await _store.SaveAsync(f, ct);
    }

    /// <summary>拒绝反馈，终止闭环。</summary>
    public async Task<ProductionFeedback> RejectAsync(Guid feedbackId, string reason, CancellationToken ct = default)
    {
        var f = await RequireAsync(feedbackId, ProductionFeedbackStatus.InReview, ct);
        f.Status = ProductionFeedbackStatus.Rejected;
        f.RejectReason = reason;
        return await _store.SaveAsync(f, ct);
    }

    private async Task<ProductionFeedback> RequireAsync(Guid feedbackId, ProductionFeedbackStatus expected, CancellationToken ct)
    {
        var f = await _store.GetAsync(feedbackId, ct);
        if (f is null) throw new InvalidOperationException($"Production feedback {feedbackId} not found.");
        if (f.Status != expected)
            throw new InvalidOperationException($"Feedback {feedbackId} status is {f.Status}, expected {expected}.");
        return f;
    }
}
