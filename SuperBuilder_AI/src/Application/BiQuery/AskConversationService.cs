using System.Collections.Concurrent;
using SuperBuilder_AI.Models.BI;

namespace SuperBuilder_AI.Services.BI;

public interface IAskConversationService
{
    AskConversationTurn Resolve(long tenantId, long userId, string? conversationId, string input, AskResolveContext? context = null);
    void Record(string conversationId, long tenantId, long userId, string question, bool awaitingClarification, AskResolveContext? context = null);
}

/// <summary>
/// 解析上下文（M6-03）：携带本轮授权数据源与（来自管线的）待确认槽位/候选实体，
/// 供待澄清会话富化存储与结构化澄清返回。全部可选，向后兼容旧调用方。
/// </summary>
public sealed class AskResolveContext
{
    public IReadOnlyCollection<long>? AuthorizedDataSourceIds { get; init; }
    public string? PendingSlot { get; init; }
    public IReadOnlyCollection<string>? CandidateEntities { get; init; }
}

public sealed record AskConversationTurn(string ConversationId, string StandaloneQuestion, bool AppliedClarification, string? OriginalQuestion)
{
    /// <summary>单轮行为分类（M6-03）。</summary>
    public AskBehavior Behavior { get; init; } = AskBehavior.NewQuestion;
    /// <summary>结构化澄清详情（M6-03）；仅澄清/确认/纠正类行为下非空。</summary>
    public ClarificationDetail? Clarification { get; init; }
}

/// <summary>保存短期待澄清状态；只保存问题文本与结构化槽位，不保存查询结果或敏感数据。</summary>
public sealed class AskConversationService : IAskConversationService
{
    /// <summary>同一会话连续澄清轮数阈值；超过即标记 LoopDetected，避免无限返回同一种 Medium。</summary>
    private const int LoopThreshold = 3;

    private sealed record Pending(
        long TenantId,
        long UserId,
        string Question,
        DateTime ExpiresAt,
        List<string> CandidateEntities,
        string? Limit,
        string? Order,
        string? Filter,
        List<long> AuthorizedDataSourceIds,
        string? PendingSlot,
        int ClarifyCount);

    private readonly ConcurrentDictionary<string, Pending> _pending = new(StringComparer.Ordinal);
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    public AskConversationTurn Resolve(long tenantId, long userId, string? conversationId, string input, AskResolveContext? context = null)
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

        // 取消/清空：立即结束当前澄清会话（无论是否存在 pending）。
        if (IsCancel(input))
        {
            _pending.TryRemove(id, out _);
            return new AskConversationTurn(id, input, false, null) { Behavior = AskBehavior.Cancel };
        }

        if (_pending.TryGetValue(id, out var pending))
        {
            if (pending.ExpiresAt <= DateTime.UtcNow || pending.TenantId != tenantId || pending.UserId != userId)
            {
                _pending.TryRemove(id, out _);
            }
            else
            {
                var looped = pending.ClarifyCount >= LoopThreshold;

                // 3b：别名确认（X 就是 Y / X 指的是 Y 等）→ Confirmation。
                if (TryParseAliasConfirmation(input, out var alias, out var canonical)
                    && !string.IsNullOrWhiteSpace(alias)
                    && !string.IsNullOrWhiteSpace(canonical))
                {
                    var rewritten = RewriteWithAlias(pending.Question, alias, canonical);
                    var aliasComposed =
                        $"原始查询：{pending.Question}\n" +
                        $"用户补充说明：{input}\n" +
                        $"用户确认「{alias}」即「{canonical}」，请按改写后的查询处理：{rewritten}";
                    var entities = MergeCandidates(pending.CandidateEntities, alias);
                    return new AskConversationTurn(id, aliasComposed, true, pending.Question)
                    {
                        Behavior = AskBehavior.Confirmation,
                        Clarification = BuildClarification(pending, entities,
                            $"实体别名确认：{alias} → {canonical}", looped),
                    };
                }

                // 3c：纠正（改成/改为/不对/应该是 等）→ Correction。
                if (IsCorrection(input))
                {
                    var correctionComposed =
                        $"原始查询：{pending.Question}\n用户纠正：{input}\n请根据纠正重写完整、可独立执行的查询后处理。";
                    return new AskConversationTurn(id, correctionComposed, true, pending.Question)
                    {
                        Behavior = AskBehavior.Correction,
                        Clarification = BuildClarification(pending, pending.CandidateEntities, pending.PendingSlot, looped),
                    };
                }

                // 3d：其余补充说明 → Clarification。
                var composed = $"原始查询：{pending.Question}\n用户补充说明：{input}\n请根据补充说明消除歧义，并重写为完整、可独立执行的查询后处理。";
                return new AskConversationTurn(id, composed, true, pending.Question)
                {
                    Behavior = AskBehavior.Clarification,
                    Clarification = BuildClarification(pending, pending.CandidateEntities, pending.PendingSlot, looped),
                };
            }
        }

        // 无待澄清上下文 → 首问（含普通细化式输入）。
        return new AskConversationTurn(id, input, false, null) { Behavior = AskBehavior.NewQuestion };
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

    /// <summary>识别纠正意图（M6-03 Correction）。仅作用于已存在待澄清会话的补充轮次。</summary>
    private static bool IsCorrection(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length > 60) return false;
        return input.Contains("改成", StringComparison.OrdinalIgnoreCase)
            || input.Contains("改为", StringComparison.OrdinalIgnoreCase)
            || input.Contains("纠错", StringComparison.OrdinalIgnoreCase)
            || input.Contains("修正为", StringComparison.OrdinalIgnoreCase)
            || input.Contains("更新为", StringComparison.OrdinalIgnoreCase)
            || input.Contains("不对", StringComparison.OrdinalIgnoreCase)
            || input.Contains("应该是", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>识别取消/清空意图（M6-03 Cancel）。仅对短句生效，避免误伤含「结束」等词的普通问题。</summary>
    private static bool IsCancel(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length > 12) return false;
        return input.Contains("取消", StringComparison.OrdinalIgnoreCase)
            || input.Contains("清空", StringComparison.OrdinalIgnoreCase)
            || input.Contains("结束", StringComparison.OrdinalIgnoreCase)
            || input.Contains("不用了", StringComparison.OrdinalIgnoreCase)
            || input.Contains("算了", StringComparison.OrdinalIgnoreCase)
            || input.Contains("终止", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> MergeCandidates(List<string> existing, string? extra)
    {
        var list = new List<string>(existing);
        if (!string.IsNullOrWhiteSpace(extra) && !list.Contains(extra, StringComparer.OrdinalIgnoreCase))
            list.Add(extra);
        return list;
    }

    private static ClarificationDetail BuildClarification(
        Pending pending, List<string> candidateEntities, string? pendingSlot, bool looped)
        => new ClarificationDetail
        {
            OriginalQuestion = pending.Question,
            PendingSlot = pendingSlot ?? pending.PendingSlot,
            CandidateEntities = candidateEntities,
            CandidateMetrics = pending.PendingSlot is null ? new List<string>() : new List<string>(),
            CandidateDimensions = new List<string>(),
            CandidateTime = new List<string>(),
            AuthorizedDataSourceIds = pending.AuthorizedDataSourceIds,
            RepeatCount = pending.ClarifyCount,
            LoopDetected = looped,
        };

    public void Record(string conversationId, long tenantId, long userId, string question, bool awaitingClarification, AskResolveContext? context = null)
    {
        if (!awaitingClarification)
        {
            _pending.TryRemove(conversationId, out _);
            return;
        }

        // 保留上一轮的澄清计数，避免每次 Record 重置导致循环检测失效。
        var prevCount = _pending.TryGetValue(conversationId, out var existing) ? existing.ClarifyCount : 0;
        var candidateEntities = context?.CandidateEntities is not null
            ? new List<string>(context.CandidateEntities)
            : (existing?.CandidateEntities ?? new List<string>());

        _pending[conversationId] = new Pending(
            tenantId,
            userId,
            question,
            DateTime.UtcNow.Add(Lifetime),
            candidateEntities,
            null,
            null,
            null,
            context?.AuthorizedDataSourceIds is not null
                ? new List<long>(context.AuthorizedDataSourceIds)
                : (existing?.AuthorizedDataSourceIds ?? new List<long>()),
            context?.PendingSlot ?? existing?.PendingSlot,
            prevCount + 1);
    }
}
