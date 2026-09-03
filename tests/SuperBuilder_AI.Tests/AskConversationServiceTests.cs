using SuperBuilder_AI.Services.BI;
using Xunit;

namespace SuperBuilder_AI.Tests;

public sealed class AskConversationServiceTests
{
    [Fact]
    public void Pending_clarification_is_composed_with_original_question()
    {
        var service = new AskConversationService();
        var first = service.Resolve(7, 11, null, "给我最近的十张入库单");
        service.Record(first.ConversationId, 7, 11, first.StandaloneQuestion, true);

        var second = service.Resolve(7, 11, first.ConversationId, "入库单就是入库凭证");

        Assert.True(second.AppliedClarification);
        Assert.Contains("给我最近的十张入库单", second.StandaloneQuestion);
        Assert.Contains("入库单就是入库凭证", second.StandaloneQuestion);
    }

    [Fact]
    public void Conversation_cannot_cross_tenant_or_user_boundary()
    {
        var service = new AskConversationService();
        var first = service.Resolve(7, 11, null, "原问题");
        service.Record(first.ConversationId, 7, 11, first.StandaloneQuestion, true);

        var otherTenant = service.Resolve(8, 11, first.ConversationId, "补充");
        var otherUser = service.Resolve(7, 12, first.ConversationId, "补充");

        Assert.False(otherTenant.AppliedClarification);
        Assert.False(otherUser.AppliedClarification);
    }

    [Fact]
    public void Completed_conversation_drops_pending_state()
    {
        var service = new AskConversationService();
        var first = service.Resolve(7, 11, null, "原问题");
        service.Record(first.ConversationId, 7, 11, first.StandaloneQuestion, true);
        service.Record(first.ConversationId, 7, 11, first.StandaloneQuestion, false);

        var next = service.Resolve(7, 11, first.ConversationId, "全新问题");

        Assert.False(next.AppliedClarification);
        Assert.Equal("全新问题", next.StandaloneQuestion);
    }

    [Fact]
    public void Alias_confirmation_rewrites_original_question_with_canonical_term()
    {
        var service = new AskConversationService();
        var first = service.Resolve(7, 11, null, "给我最近的十张入库单");
        service.Record(first.ConversationId, 7, 11, first.StandaloneQuestion, true);

        var second = service.Resolve(7, 11, first.ConversationId, "入库单就是入库凭证");

        Assert.True(second.AppliedClarification);
        // 别名改写：原始问题中的「入库单」应被替换为规范名「入库凭证」。
        Assert.Contains("给我最近的十张入库凭证", second.StandaloneQuestion);
        Assert.Contains("「入库单」即「入库凭证」", second.StandaloneQuestion);
        // 兼容既有对话合成契约：原始输入仍保留。
        Assert.Contains("入库单就是入库凭证", second.StandaloneQuestion);
    }

    [Fact]
    public void Empty_conversation_id_recovers_recent_pending_turn()
    {
        var service = new AskConversationService();
        var first = service.Resolve(7, 11, null, "给我最近的十张入库单");
        service.Record(first.ConversationId, 7, 11, first.StandaloneQuestion, true);

        // 第二轮回传空 conversationId，应仍能恢复上一轮 awaiting 状态，
        // 而非当作全新问题（这是「永远当新问句」死循环的根因之一）。
        var second = service.Resolve(7, 11, null, "入库单就是入库凭证");

        Assert.True(second.AppliedClarification);
        Assert.Contains("给我最近的十张入库单", second.StandaloneQuestion);
    }
}
