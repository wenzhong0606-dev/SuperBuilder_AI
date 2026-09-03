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
        input = input?.Trim() ?? string.Empty;

        // 3a：conversationId 为空时，恢复最近一次 awaiting 轮次，
        // 避免前端未回传 id 导致第二轮永远被当作新问题。
        string id;
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            var recovered = FindRecentPending(tenantId, userId);
            id = recovered ?? Guid.NewGuid().ToString("N");
        }
        else
        {
            id = conversationId.Trim();
        }

        if (_pending.TryGetValue(id, out var pending))
        {
            if (pending.ExpiresAt <= DateTime.UtcNow || pending.TenantId != tenantId || pending.UserId != userId)
            {
                _pending.TryRemove(id, out _);
            }
            else
            {
                // 3b：别名确认（X 就是 Y / X 指的是 Y 等）
                if (TryParseAliasConfirmation(input, out var alias, out var canonical)
                    && !string.IsNullOrWhiteSpace(alias)
                    && !string.IsNullOrWhiteSpace(canonical))
                {
                    var rewritten = RewriteWithAlias(pending.Question, alias, canonical);
                    // 同时保留原始补充说明，确保既有对话合成契约（含原始输入）不被破坏。
                    var aliasComposed =
                        $"原始查询：{pending.Question}\n" +
                        $"用户补充说明：{input}\n" +
                        $"用户确认「{alias}」即「{canonical}」，请按改写后的查询处理：{rewritten}";
                    return new AskConversationTurn(id, aliasComposed, true, pending.Question);
                }

                // 使用明确分段提示，让 QueryUnderstanding 把本轮视为澄清，而非独立分析问题。
                var composed = $"原始查询：{pending.Question}\n用户补充说明：{input}\n请根据补充说明消除歧义，并重写为完整、可独立执行的查询后处理。";
                return new AskConversationTurn(id, composed, true, pending.Question);
            }
        }
        return new AskConversationTurn(id, input, false, null);
    }

    /// <summary>
    /// 在 awaiting 轮次中，恢复最近一次（ExpiresAt 最大）属于该租户/用户的待澄清会话。
    /// 仅当调用方未提供 conversationId 时使用，用于容忍前端未回传 id 的场景。
    /// </summary>
    private string? FindRecentPending(long tenantId, long userId)
    {
        string? bestKey = null;
        var bestExpiry = DateTime.MinValue;

        foreach (var kvp in _pending)
        {
            var p = kvp.Value;
            if (p.TenantId == tenantId
                && p.UserId == userId
                && p.ExpiresAt > DateTime.UtcNow
                && p.ExpiresAt > bestExpiry)
            {
                bestExpiry = p.ExpiresAt;
                bestKey = kvp.Key;
            }
        }

        return bestKey;
    }

    /// <summary>
    /// 识别「X 就是 Y」「X 指的是 Y」「X 等同于 Y」等实体别名确认。
    /// 命中时返回 true，并输出别名（alias）与规范名（canonical）。
    /// </summary>
    private static bool TryParseAliasConfirmation(
        string input,
        out string alias,
        out string canonical)
    {
        alias = string.Empty;
        canonical = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var s = input.Trim();

        // 去掉口语前缀，聚焦「X 即 Y」核心结构。
        s = s
            .Replace("我说的", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("你说的", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("用户说的", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("系统说的", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        // 别名确认通常是短句，超过阈值不再尝试解析，避免误判普通补充说明。
        if (s.Length > 40)
        {
            return false;
        }

        var separators = new[]
        {
            "就是", "指的是", "等同于", "称作", "叫做", "即"
        };

        foreach (var sep in separators)
        {
            var idx = s.IndexOf(sep, StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
            {
                var left = s.Substring(0, idx).Trim();
                var right = s.Substring(idx + sep.Length).Trim();

                if (left.Length > 0 && left.Length <= 20
                    && right.Length > 0 && right.Length <= 20)
                {
                    alias = left;
                    canonical = right;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 将原始问题中的别名（alias）替换为规范名（canonical），生成可直接理解的改写查询。
    /// 若原始问题中不含 alias，则退化为追加括号说明，避免丢失信息。
    /// </summary>
    private static string RewriteWithAlias(
        string question,
        string alias,
        string canonical)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return canonical;
        }

        var idx = question.IndexOf(
            alias,
            StringComparison.OrdinalIgnoreCase);

        return idx >= 0
            ? question.Remove(idx, alias.Length).Insert(idx, canonical)
            : $"{question}（{alias}即{canonical}）";
    }

    public void Record(string conversationId, long tenantId, long userId, string question, bool awaitingClarification)
    {
        if (awaitingClarification)
            _pending[conversationId] = new Pending(tenantId, userId, question, DateTime.UtcNow.Add(Lifetime));
        else
            _pending.TryRemove(conversationId, out _);
    }
}
