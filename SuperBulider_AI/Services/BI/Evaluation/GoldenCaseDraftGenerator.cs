using SuperBuilder_AI.Models.BI.Evaluation;

namespace SuperBuilder_AI.Services.BI.Evaluation;

/// <summary>
/// Phase 2.6 C.7.5
/// 将 Scenario Gap 转换为可人工确认的 Golden Case 草稿。
/// 不生成 Expected Ground Truth。
/// </summary>
public sealed class GoldenCaseDraftGenerator
{
    private static readonly IReadOnlyDictionary<string, string> QuestionTemplates =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Intent"] = "请验证一个需要明确识别业务意图的查询：{业务问题}",
            ["Metric"] = "请查询{指标}。重点验证指标语义与聚合方式。",
            ["Dimension"] = "请按{维度}分析{指标}。",
            ["Filter"] = "请查询{指标}，并筛选{条件}。",
            ["Table"] = "请查询{业务对象}，重点验证业务对象对应表的解析。",
            ["Join"] = "请查询{业务对象A}与{业务对象B}的关联数据，重点验证 Join。",
            ["Order"] = "请查询{指标}并按{排序字段}排序。",
            ["Aggregate"] = "请统计{指标}，重点验证聚合语义。",
            ["Distinct"] = "请查询去重后的{业务对象或字段}。",
            ["Limit"] = "请查询{指标}，仅返回前{N}条结果。",
            ["Ranking"] = "请查询{指标}排名，重点验证排名语义。",
            ["DetailRanking"] = "请查询明细排名，重点验证 Detail Ranking。",
            ["AggregateRanking"] = "请查询聚合指标排名，重点验证 Aggregate Ranking。"
        };

    public GoldenCaseDraftScorecard Generate(GoldenScenarioGapScorecard gaps)
    {
        ArgumentNullException.ThrowIfNull(gaps);

        var drafts = gaps.Gaps.Select((gap, index) => new GoldenCaseDraft
        {
            DraftId = $"GAP-{gap.Dimension.ToUpperInvariant()}-{index + 1:000}",
            Dimension = gap.Dimension,
            ScenarioType = gap.ScenarioType,
            Priority = gap.Priority,
            QuestionTemplate = QuestionTemplates.TryGetValue(gap.Dimension, out var template)
                ? template
                : $"请补充一个覆盖 {gap.Dimension} 能力的真实业务查询。",
            SuggestedTags = gap.SuggestedTags.ToList(),
            RequiresHumanConfirmation = true
        }).ToList();

        return new GoldenCaseDraftScorecard
        {
            Dataset = gaps.Dataset,
            Version = gaps.Version,
            DraftCount = drafts.Count,
            Drafts = drafts
        };
    }
}
