using System.Collections.Concurrent;

namespace SuperBuilder_AI.Services.BI;

public interface IAskConversationService
{
    AskConversationTurn Resolve(long tenantId, long userId, string? conversationId, string input);
    void Record(string conversationId, long tenantId, long userId, string question, bool awaitingClarification);
}

public sealed record AskConversationTurn(string ConversationId, string StandaloneQuestion, bool AppliedClarification, string? OriginalQuestion);

/// <summary>保存短期待澄清状态；只保存问题文本，不保存查询结果或敏感数据。</summary>
public sealed class AskConversationService : IAskConversationService
{
    private sealed record Pending(long TenantId, long UserId, string Question, DateTime ExpiresAt);
    private readonly ConcurrentDictionary<string, Pending> _pending = new(StringComparer.Ordinal);
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    public AskConversationTurn Resolve(long tenantId, long userId, string? conversationId, string input)
    {
        var id = string.IsNullOrWhiteSpace(conversationId) ? Guid.NewGuid().ToString("N") : conversationId.Trim();
        if (_pending.TryGetValue(id, out var pending))
        {
            if (pending.ExpiresAt <= DateTime.UtcNow || pending.TenantId != tenantId || pending.UserId != userId)
            {
                _pending.TryRemove(id, out _);
            }
            else
            {
                // 使用明确分段提示，让 QueryUnderstanding 把本轮视为澄清，而非独立分析问题。
                var composed = $"原始查询：{pending.Question}\n用户补充说明：{input}\n请根据补充说明消除歧义，并重写为完整、可独立执行的查询后处理。";
                return new AskConversationTurn(id, composed, true, pending.Question);
            }
        }
        return new AskConversationTurn(id, input.Trim(), false, null);
    }

    public void Record(string conversationId, long tenantId, long userId, string question, bool awaitingClarification)
    {
        if (awaitingClarification)
            _pending[conversationId] = new Pending(tenantId, userId, question, DateTime.UtcNow.Add(Lifetime));
        else
            _pending.TryRemove(conversationId, out _);
    }
}
