using SuperBulider_AI.Models.AI;

namespace SuperBulider_AI.Services.AI.QueryUnderstanding;

/// <summary>
/// QueryIntent 确定性规范化器。
/// 负责将自然语言理解阶段产生的 QueryIntent 转换为后续 QueryPlan 可以稳定消费的结构。
/// </summary>
public class QueryIntentNormalizer
{
    public QueryIntent Normalize(QueryIntent intent)
    {
        if (intent == null)
            throw new ArgumentNullException(nameof(intent));

        NormalizeCollection(intent);
        NormalizeLimit(intent);
        NormalizeRanking(intent);
        NormalizeAggregation(intent);
        NormalizeYearFilters(intent);

        return intent;
    }

    private static void NormalizeCollection(QueryIntent intent)
    {
        if (intent.Filters == null)
            intent.Filters = new List<QueryFilter>();
    }

    private static void NormalizeLimit(QueryIntent intent)
    {
        if (intent.Limit < 0)
            intent.Limit = 0;
    }

    private static void NormalizeRanking(QueryIntent intent)
    {
        if (intent.Limit <= 0)
            return;

        if (intent.IntentType != "Ranking")
            return;

        if (intent.Metrics != null && intent.Metrics.Count > 0)
        {
            foreach (var metric in intent.Metrics)
            {
                if (string.IsNullOrWhiteSpace(metric.Aggregation))
                    metric.Aggregation = "COUNT";
            }
        }
    }

    private static void NormalizeAggregation(QueryIntent intent)
    {
        if (intent.Metrics == null)
            return;

        foreach (var metric in intent.Metrics)
        {
            if (string.IsNullOrWhiteSpace(metric.Aggregation))
                metric.Aggregation = "COUNT";
        }
    }

    /// <summary>
    /// 将“2025年”这类确定性的年度时间语义规范化为日期下界过滤。
    /// 例如：2025年入库数量 -> 入库日期 >= 2025-01-01。
    /// 不覆盖已有明确日期范围、比较符或具体日期的 Filter。
    /// </summary>
    private static void NormalizeYearFilters(QueryIntent intent)
    {
        if (intent.Filters == null || intent.Filters.Count == 0)
            return;

        foreach (var filter in intent.Filters)
        {
            if (filter == null)
                continue;

            var value = filter.Value?.Trim();
            if (string.IsNullOrWhiteSpace(value))
                continue;

            if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^\d{4}年?$"))
                continue;

            var yearText = value.TrimEnd('年');
            if (!int.TryParse(yearText, out var year) || year < 1900 || year > 9999)
                continue;

            // 仅规范化“年度”表达，不改变已经明确表达的操作符。
            if (!string.IsNullOrWhiteSpace(filter.Operator) &&
                !string.Equals(filter.Operator, "=", StringComparison.OrdinalIgnoreCase))
                continue;

            filter.Operator = ">=";
            filter.Value = $"{year:D4}-01-01";
        }
    }
}
