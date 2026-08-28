using SuperBuilder_AI.Interfaces.BI.Entity;
using SuperBuilder_AI.Models.BI.Entity;

namespace SuperBuilder_AI.Services.BI.Entity;

/// <summary>
/// 业务语义映射服务：自然语言 → 业务实体。
///
/// 设计约束（Golden 安全）：
/// 1. 完全确定性——按词元重叠打分，不调用 LLM，不受限流/非确定性影响；
/// 2. 只读 SuperBuilder Metadata DB，不连接动态业务数据库；
/// 3. 不修改任何运行时查询链路，仅提供业务语义层的可观测能力。
/// </summary>
public sealed class BusinessSemanticMappingService(IBusinessEntityService entities)
    : IBusinessSemanticMappingService
{
    public async Task<BusinessSemanticResolutionResult> ResolveAsync(
        long tenantId,
        long dataSourceId,
        string query,
        int topPerDomain = 3,
        CancellationToken cancellationToken = default)
    {
        var result = new BusinessSemanticResolutionResult { Query = query };
        if (string.IsNullOrWhiteSpace(query)) return result;

        var all = await entities.ListAsync(tenantId, cancellationToken);
        if (all.Count == 0) return result;

        var queryTokens = Tokenize(query);
        if (queryTokens.Count == 0) return result;

        var scored = new List<BusinessSemanticMatch>();
        foreach (var entity in all)
        {
            var signals = string.Join(' ',
                entity.Name,
                entity.DisplayName,
                entity.SemanticText,
                entity.BusinessDomain,
                entity.Description,
                string.Join(' ', entity.Keys.Select(x => x.Name)),
                string.Join(' ', entity.Attributes.Select(x => x.Name)),
                string.Join(' ', entity.Metrics.Select(x => x.Name)));

            var score = Score(queryTokens, signals);
            if (score <= 0) continue;

            scored.Add(new BusinessSemanticMatch
            {
                EntityId = entity.Id,
                BusinessKey = entity.BusinessKey,
                Name = entity.Name,
                DisplayName = entity.DisplayName,
                BusinessDomain = entity.BusinessDomain,
                Score = score
            });
        }

        if (scored.Count == 0) return result;

        // 按业务域分组；每个域内按分数降序取 topPerDomain 条。
        var take = topPerDomain <= 0 ? 3 : topPerDomain;
        var groups = scored
            .GroupBy(x => string.IsNullOrWhiteSpace(x.BusinessDomain) ? "未分类" : x.BusinessDomain!)
            .OrderByDescending(g => g.Max(x => x.Score))
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new BusinessSemanticDomainGroup
            {
                Domain = g.Key,
                Matches = g.OrderByDescending(x => x.Score)
                           .ThenBy(x => x.Name, StringComparer.Ordinal)
                           .Take(take)
                           .ToList()
            })
            .ToList();

        result.Domains = groups;
        return result;
    }

    /// <summary>
    /// 确定性打分：查询词元在业务信号文本中的命中比例。
    /// 命中比率 * 100，保留 4 位小数，避免浮点噪声影响排序稳定性。
    /// </summary>
    private static double Score(HashSet<string> queryTokens, string signals)
    {
        var signalTokens = Tokenize(signals);
        if (signalTokens.Count == 0) return 0;

        var hits = queryTokens.Count(t => signalTokens.Contains(t));
        if (hits == 0) return 0;

        return Math.Round((double)hits / queryTokens.Count * 100, 4);
    }

    /// <summary>
    /// 轻量分词：按非字母数字切分，兼容中英文。
    /// 中文无空格，额外按 2-gram 切分以提升中文召回。
    /// </summary>
    private static HashSet<string> Tokenize(string? text)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text)) return set;

        var buffer = new System.Text.StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch)) buffer.Append(ch);
            else if (buffer.Length > 0) { Flush(set, buffer); buffer.Clear(); }
        }
        if (buffer.Length > 0) Flush(set, buffer);

        return set;
    }

    private static void Flush(HashSet<string> set, System.Text.StringBuilder buffer)
    {
        var token = buffer.ToString();
        set.Add(token);

        // 中文 2-gram：提升"入库明细"这类无空格短语的召回。
        if (token.Length >= 2 && HasCjk(token))
        {
            for (var i = 0; i < token.Length - 1; i++)
                set.Add(token.Substring(i, 2));
        }
    }

    private static bool HasCjk(string token) =>
        token.Any(ch => ch >= '\u4e00' && ch <= '\u9fff');
}
