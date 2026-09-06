using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Services.BI;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>
/// M6-03 澄清与语义学习（核心 A 增量）：Ask 单轮行为分类、结构化澄清富状态、循环检测。
/// 使用真实 <see cref="AskConversationService"/>（内存 ConcurrentDictionary，无 LLM / 无 DB，确定性）。
/// </summary>
public sealed class AskConversationServiceBehaviorTests
{
    private static AskConversationService NewService() => new();

    [Fact]
    public void Resolve_NoPending_Returns_NewQuestion_WithoutClarification()
    {
        var svc = NewService();
        var turn = svc.Resolve(3, 5, null, "本月销售额");

        Assert.Equal(AskBehavior.NewQuestion, turn.Behavior);
        Assert.Null(turn.Clarification);
        Assert.False(turn.AppliedClarification);
    }

    [Fact]
    public void Resolve_PendingSupplement_Returns_Clarification_With_OriginalQuestion()
    {
        var svc = NewService();
        svc.Record("c1", 3, 5, "最近的入库凭证", awaitingClarification: true);

        var turn = svc.Resolve(3, 5, "c1", "只看华东地区");

        Assert.Equal(AskBehavior.Clarification, turn.Behavior);
        Assert.NotNull(turn.Clarification);
        Assert.Equal("最近的入库凭证", turn.Clarification!.OriginalQuestion);
        Assert.Equal(1, turn.Clarification.RepeatCount);
        Assert.False(turn.Clarification.LoopDetected);
    }

    [Fact]
    public void Resolve_AliasConfirmation_Returns_Confirmation_With_CandidateEntity()
    {
        var svc = NewService();
        svc.Record("c1", 3, 5, "最近的入库单", awaitingClarification: true);

        var turn = svc.Resolve(3, 5, "c1", "入库单就是入库凭证");

        Assert.Equal(AskBehavior.Confirmation, turn.Behavior);
        Assert.NotNull(turn.Clarification);
        Assert.Contains("入库单", turn.Clarification!.CandidateEntities);
        Assert.Contains("入库单", turn.Clarification.PendingSlot ?? "");
        Assert.Contains("入库凭证", turn.Clarification.PendingSlot ?? "");
    }

    [Fact]
    public void Resolve_Correction_Returns_Correction()
    {
        var svc = NewService();
        svc.Record("c1", 3, 5, "销售额", awaitingClarification: true);

        var turn = svc.Resolve(3, 5, "c1", "改成按月统计");

        Assert.Equal(AskBehavior.Correction, turn.Behavior);
    }

    [Fact]
    public void Resolve_Cancel_ClearsPending_And_NextTurnIs_NewQuestion()
    {
        var svc = NewService();
        svc.Record("c1", 3, 5, "x", awaitingClarification: true);

        var cancel = svc.Resolve(3, 5, "c1", "取消");
        Assert.Equal(AskBehavior.Cancel, cancel.Behavior);

        var next = svc.Resolve(3, 5, "c1", "新的问题");
        Assert.Equal(AskBehavior.NewQuestion, next.Behavior);
    }

    [Fact]
    public void Resolve_Cancel_WithoutPending_Returns_Cancel()
    {
        var svc = NewService();
        var turn = svc.Resolve(3, 5, null, "结束");
        Assert.Equal(AskBehavior.Cancel, turn.Behavior);
    }

    [Fact]
    public void LoopDetection_AfterThreshold_Marks_LoopDetected()
    {
        var svc = NewService();
        svc.Record("c1", 3, 5, "原问题", awaitingClarification: true); // ClarifyCount = 1

        // Round 1
        var r1 = svc.Resolve(3, 5, "c1", "补充1");
        Assert.False(r1.Clarification!.LoopDetected);
        svc.Record("c1", 3, 5, "原问题", awaitingClarification: true); // ClarifyCount = 2

        // Round 2
        var r2 = svc.Resolve(3, 5, "c1", "补充2");
        Assert.False(r2.Clarification!.LoopDetected);
        svc.Record("c1", 3, 5, "原问题", awaitingClarification: true); // ClarifyCount = 3

        // Round 3：达到阈值，标记循环检测，避免无限返回同一种 Medium。
        var r3 = svc.Resolve(3, 5, "c1", "补充3");
        Assert.True(r3.Clarification!.LoopDetected);
        Assert.Equal(3, r3.Clarification.RepeatCount);
    }

    [Fact]
    public void Record_Context_AuthorizedDataSourceIds_Surfaced_In_Clarification()
    {
        var svc = NewService();
        svc.Record("c1", 3, 5, "q", awaitingClarification: true,
            new AskResolveContext { AuthorizedDataSourceIds = new[] { 7L, 8L } });

        var turn = svc.Resolve(3, 5, "c1", "只看华东地区");

        Assert.Contains(7L, turn.Clarification!.AuthorizedDataSourceIds);
        Assert.Contains(8L, turn.Clarification.AuthorizedDataSourceIds);
    }

    [Fact]
    public void Record_Context_PendingSlot_Surfaced_In_Clarification()
    {
        var svc = NewService();
        svc.Record("c1", 3, 5, "q", awaitingClarification: true,
            new AskResolveContext { PendingSlot = "业务实体：入库单 / 入库凭证 歧义" });

        var turn = svc.Resolve(3, 5, "c1", "用入库凭证");

        Assert.Equal("业务实体：入库单 / 入库凭证 歧义", turn.Clarification!.PendingSlot);
    }

    [Fact]
    public void Resolve_CrossTenant_Pending_NotVisible()
    {
        var svc = NewService();
        svc.Record("c1", 3, 5, "tenantA question", awaitingClarification: true);

        // 另一租户/用户回传同一 id → 不应命中 tenantA 的待澄清会话。
        var turn = svc.Resolve(9, 9, "c1", "只看华东地区");
        Assert.Equal(AskBehavior.NewQuestion, turn.Behavior);
    }
}
