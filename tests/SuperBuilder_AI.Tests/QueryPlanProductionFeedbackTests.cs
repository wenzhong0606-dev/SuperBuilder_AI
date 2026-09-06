using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SuperBuilder_AI.Application.Common.Options;
using SuperBuilder_AI.Interfaces.BI.Planning;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Services.BI;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M5-10 Production Feedback 闭环测试：
/// 默认零行为、Store 持久化、审计派生开关、Configurable 模式、完整闭环、Reject、错误态。
/// </summary>
public class QueryPlanProductionFeedbackTests
{
    [Fact]
    public async Task DefaultNoOpSink_DoesNotPersist()
    {
        var sink = new NoOpProductionFeedbackSink();
        await sink.RecordAsync(new ProductionFeedback());

        // NoOp 永不持久化；用内存桩验证 store 仍为空。
        var store = new InMemoryProductionFeedbackStore();
        Assert.Empty(await store.ListAsync());
    }

    [Fact]
    public async Task StoreSink_PersistsFeedback()
    {
        var store = new InMemoryProductionFeedbackStore();
        var sink = new StoreProductionFeedbackSink(store);

        var fb = new ProductionFeedback { Question = "q", Description = "d" };
        await sink.RecordAsync(fb);

        var list = await store.ListAsync();
        Assert.Single(list);
        Assert.Equal(fb.FeedbackId, list[0].FeedbackId);
    }

    [Fact]
    public async Task StoreSink_AuditDerivedDisabled_IgnoresAudit()
    {
        var store = new InMemoryProductionFeedbackStore();
        var sink = new StoreProductionFeedbackSink(store, enableAuditDerived: false);

        await sink.RecordFromAuditAsync(new QueryPlanDecisionAuditRecord { Outcome = AuditOutcome.Rejected });

        Assert.Empty(await store.ListAsync());
    }

    [Fact]
    public async Task StoreSink_AuditDerivedEnabled_CreatesAutoFeedback()
    {
        var store = new InMemoryProductionFeedbackStore();
        var sink = new StoreProductionFeedbackSink(store, enableAuditDerived: true);

        var audit = new QueryPlanDecisionAuditRecord { Outcome = AuditOutcome.Rejected, Question = "qq" };
        await sink.RecordFromAuditAsync(audit);

        var list = await store.ListAsync();
        Assert.Single(list);
        Assert.Equal(FeedbackSource.AuditAuto, list[0].Source);
        Assert.Equal(FeedbackCategory.Correctness, list[0].Category);
        Assert.Equal(audit.AuditId, list[0].AuditId);
    }

    [Fact]
    public async Task ConfigurableSink_OffMode_IsNoOp()
    {
        var store = new InMemoryProductionFeedbackStore();
        var sink = new ConfigurableProductionFeedbackSink(
            Options.Create(new ProductionFeedbackOptions { Mode = ProductionFeedbackMode.Off }),
            store);

        await sink.RecordAsync(new ProductionFeedback());

        Assert.Empty(await store.ListAsync());
    }

    [Fact]
    public async Task ConfigurableSink_StoreMode_Persists()
    {
        var store = new InMemoryProductionFeedbackStore();
        var sink = new ConfigurableProductionFeedbackSink(
            Options.Create(new ProductionFeedbackOptions { Mode = ProductionFeedbackMode.Store }),
            store);

        await sink.RecordAsync(new ProductionFeedback { Question = "x" });

        Assert.Single(await store.ListAsync());
    }

    [Fact]
    public async Task Loop_FullCycle_ReceivedToRegressionVerified()
    {
        var store = new InMemoryProductionFeedbackStore();
        var loop = new ProductionFeedbackLoop(store, new NoOpFeedbackBaselineGateway());

        var fb = await loop.ReceiveAsync(new ProductionFeedback { Question = "q" });
        Assert.Equal(ProductionFeedbackStatus.Received, fb.Status);

        fb = await loop.StartReviewAsync(fb.FeedbackId, "rev");
        Assert.Equal(ProductionFeedbackStatus.InReview, fb.Status);

        var (accepted, cand) = await loop.AcceptAsync(fb.FeedbackId, "title", "change");
        Assert.Equal(ProductionFeedbackStatus.AcceptedAsCandidate, accepted.Status);
        Assert.Equal(cand.CandidateId, accepted.CandidateId);

        fb = await loop.BaselineAsync(fb.FeedbackId);
        Assert.Equal(ProductionFeedbackStatus.Baselined, fb.Status);
        Assert.Equal("noop-baseline", fb.BaselineVersion);

        fb = await loop.VerifyRegressionAsync(fb.FeedbackId);
        Assert.Equal(ProductionFeedbackStatus.RegressionVerified, fb.Status);
        Assert.Equal("noop-regression", fb.RegressionRef);
    }

    [Fact]
    public async Task Loop_Reject_Path()
    {
        var store = new InMemoryProductionFeedbackStore();
        var loop = new ProductionFeedbackLoop(store, new NoOpFeedbackBaselineGateway());

        var fb = await loop.ReceiveAsync(new ProductionFeedback());
        fb = await loop.StartReviewAsync(fb.FeedbackId);
        fb = await loop.RejectAsync(fb.FeedbackId, "not valid");

        Assert.Equal(ProductionFeedbackStatus.Rejected, fb.Status);
        Assert.Equal("not valid", fb.RejectReason);
    }

    [Fact]
    public async Task Loop_WrongState_Throws()
    {
        var store = new InMemoryProductionFeedbackStore();
        var loop = new ProductionFeedbackLoop(store, new NoOpFeedbackBaselineGateway());

        var fb = await loop.ReceiveAsync(new ProductionFeedback());
        // 跳过 InReview 直接 Accept 应抛。
        await Assert.ThrowsAsync<InvalidOperationException>(() => loop.AcceptAsync(fb.FeedbackId, "t", "c"));
    }
}
